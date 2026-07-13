# RFC 0001 — Integración con Prometheus Alertmanager (webhook entrante)

Estado: propuesta
Autor: Arael Espinosa (borrador asistido)
Fecha: 2026-07-13

## 1. Problema

Piro chequea servicios operando en capa 5-7 (HTTP, TCP, DNS, Ping, SSL, GRPC) desde afuera hacia adentro. Esto tiene dos límites estructurales:

1. **Alcanzabilidad de red.** Un check necesita que el target sea alcanzable desde el worker de Piro (embebido o remoto). Recursos que viven en una red completamente privada (pods de k8s sin Ingress, servicios internos sin egress hacia afuera) no se pueden chequear sin exponerlos — lo cual muchas veces no es aceptable por seguridad.
2. **Ceguera de métricas internas.** Aunque un servicio sea alcanzable y devuelva `200 OK`, ese código de estado no dice nada sobre condiciones internas: disco lleno, saturación de memoria, cola de trabajo creciendo, error rate elevado en endpoints específicos, etc. Un check HTTP no puede ver eso sin que el propio servicio lo exponga de forma ad-hoc — que es exactamente lo que Prometheus ya resuelve con exporters y reglas de alerta.

## 2. No-objetivo (por ahora)

Esta propuesta **no** cubre que Piro haga `pull` activo de PromQL contra un Prometheus/Thanos/Mimir. Esa dirección quedó descartada para esta fase porque no resuelve el caso motivador (red privada sin conectividad hacia Piro) y añade una superficie de configuración por-check (endpoint, query, credenciales) que no es necesaria si la fuente de verdad de alerting ya es Alertmanager. Puede evaluarse como RFC separado si aparece un caso de uso donde Piro sí tiene línea de vista hacia el Prometheus.

## 3. Principio de diseño

**No reinventar el pipeline de alerting de Prometheus.** Alertmanager ya resuelve routing, grouping, deduplicación, silencing e inhibición de alertas generadas por reglas PromQL. Piro no debe reimplementar evaluación de reglas ni reglas de agrupamiento — debe limitarse a:

- Recibir las notificaciones que Alertmanager ya decidió enviar (vía su [webhook receiver estándar](https://prometheus.io/docs/alerting/latest/configuration/#webhook_config)).
- Traducirlas a `Alert` — la misma entidad que hoy generan los checks internos de Piro al cruzar `FailureThreshold` — **no** a `Incident` directamente.

La promoción de una `Alert` a `Incident` (lo que la hace visible en el status page público y dispara `INotificationDispatcher`) sigue siendo, como hoy con las alertas internas, una decisión humana explícita desde el admin panel (`POST /api/v1/alerts/{id}/incident`). El webhook de Alertmanager no debe saltarse ese paso: crea la señal, no el incidente. Esto mantiene un único pipeline de "señal → triage humano → incidente público" sin una ruta paralela que lo esquive, y es coherente con que hoy tampoco existe creación automática de `Incident` a partir de un check fallido (ver §3 de la investigación previa).

Esto hace la solución agnóstica de origen: cualquier cosa que Alertmanager pueda recibir (Prometheus, Node Exporter, cAdvisor, kube-state-metrics, exporters de GCP/AWS, reglas custom) llega a Piro por el mismo camino, sin que Piro necesite entender Prometheus en absoluto — solo el contrato JSON de Alertmanager.

## 4. Diseño

### 4.0 Relación con Heartbeat ([issue #1](https://github.com/Heva-Co/piro/issues/1))

`CheckType.Heartbeat` ya está planeado (v0.4) y aún sin implementar — es el mismo patrón de fondo: un check **pasivo**, que no ejecuta Piro sino que recibe una señal push de afuera. La diferencia es la semántica de esa señal:

| | Heartbeat | `Webhook` (esta RFC) |
|---|---|---|
| Señal | "sigo viva" (sin payload de estado) | evento explícito `firing`/`resolved` con severity |
| Transición a DOWN | por **ausencia** de señal dentro de un intervalo + grace period | por **presencia** de un evento `firing` |
| Requiere evaluador de timeout | sí, imprescindible (nadie avisa cuando algo deja de pasar) | no en el caso base — pero conviene sumarlo igual (ver Riesgos, §8) para detectar si Alertmanager mismo dejó de mandar señales |

Recomendación: cuando se implemente Heartbeat, extraer el mecanismo de "evaluador de última-señal-recibida" (el hosted service que menciona el issue) como pieza reusable, y que `Webhook` lo consuma también para su caso de "silencio de Alertmanager" en vez de construir un segundo evaluador de timeout en paralelo. Los dos `CheckType` son casos particulares de un mismo concepto de "check pasivo con endpoint de ingesta" — vale evaluar en implementación si conviene una base común (ej. una tabla/campo `LastSignalAt` compartida) en vez de dos features aisladas.

### 4.1 Flujo

```
Prometheus (reglas de alerta)
   → Alertmanager (routing, grouping, silencing)
      → POST webhook  →  Piro.Api  [nuevo endpoint]
                              ↓
                    valida firma/secreto por Integration
                              ↓
                    mapea alerta → Check "externo" (por label) → AlertConfig asociado
                              ↓
                    crea/resuelve Alert vía AlertLifecycleService (fingerprint = dedup)
                              ↓
                    (sin intervención humana, aquí termina el camino automático)
                              ↓
                    admin revisa la Alert y decide promoverla a Incident (manual, como hoy)
                              ↓
                    esa promoción dispara INotificationDispatcher existente
```

Piro nunca inicia conexión hacia la red del cluster — Alertmanager es quien empuja. Esto resuelve el caso de "worker privado sin visibilidad" porque no se requiere ninguna conectividad Piro→cluster, solo cluster→Piro (que ya suele existir para egress saliente).

**Importante**: no hay notificación automática solo por recibir el webhook. `INotificationDispatcher` se dispara igual que hoy, únicamente cuando alguien promueve la `Alert` a `Incident` desde el admin (o, en fase posterior, si el equipo configura auto-promoción — ver §6 Fase 3). Esto evita que un flapping de Alertmanager spamee canales de notificación sin criterio humano de por medio, algo que sí sería reinventar mal el trabajo que Alertmanager ya hace con `group_wait`/`repeat_interval` pero sin su contexto de severidad real.

### 4.2 El problema del `Check` obligatorio

`Alert.CheckId` es `int` no-nullable con FK `OnDelete(Cascade)` — no existe hoy ningún camino para crear una `Alert` sin un `Check` real detrás. `AlertLifecycleService.RecordOccurrenceAsync` asume que ya corrió una evaluación de `AlertConfig` sobre un `Check` concreto. Una alerta que llega desde Alertmanager no tiene ese `Check` — el chequeo lo hizo Prometheus, no Piro.

En vez de relajar ese FK (rompería la invariante `Alert → Check → Service` en la que se apoya el resto del sistema, incluida la resolución de `TriggeringCheckId` en incidentes), la propuesta es introducir un **`CheckType.Webhook`**: un Check que no se ejecuta nunca vía `ICheckExecutor` (no hay `RoutingCheckJobDispatcher` que lo dispare — no tiene `Cron`, o tiene uno inactivo) y cuyo único rol es ser el ancla estructural para las `Alert`s que llegan por webhook. Se crea uno de este tipo por cada combinación `Service` × fuente externa que se quiera monitorear vía Alertmanager (típicamente uno por Service, salvo que se quiera separar por tipo de alerta).

Esto es coherente con el modelo existente: `Check.CurrentStatus` se sigue asignando directamente (aquí, por el webhook en vez de un executor), y participa igual en la agregación de `Service.CurrentStatus` vía `ServiceStatusService` sin tocar esa lógica.

### 4.3 Nuevo tipo de integración

Extender `IntegrationType` con `AlertmanagerWebhook`. Una `Integration` de este tipo guarda en `ConfigJson`:

- Un secreto HMAC (o token bearer) generado por Piro al crear la integración, mostrado una sola vez en el admin panel — mismo patrón UX que `ApiKeyService` (se persiste solo el hash).
- Un mapeo de labels de Alertmanager → el `Check` de tipo `Webhook` correspondiente. Mínimo viable: la alerta debe incluir un label `piro_check_slug` (configurado en la regla de Prometheus/en el `route` de Alertmanager) que Piro resuelve contra un identificador estable del Check. Evita adivinar mapeos por nombre de alerta.

Esto reusa el `Integration` existente en vez de crear una tabla nueva — coherente con que hoy `Check.IntegrationId` ya es "un blob de config asociado a una entidad de monitoreo", solo que en dirección inversa (saliente hoy, entrante para este caso).

### 4.4 Nuevo endpoint

`POST /api/v1/webhooks/alertmanager/{integrationId}`

- **Autenticación**: header `Authorization: Bearer <secret>` validado contra el hash guardado en la `Integration`, replicando `ApiKeyService.ValidateAsync` (comparación de hash SHA-256, no el secreto en texto plano). Alternativa más robusta si se quiere evitar secretos en texto plano viajando por header: firma HMAC-SHA256 del body sobre un secreto compartido (patrón estándar de webhooks tipo Stripe/GitHub) — recomendado si en el futuro se soporta multi-tenant con secretos rotables sin downtime; para v1, bearer token simple es suficiente y consistente con lo que ya existe en el código.
- **Payload**: el formato JSON estándar que Alertmanager envía a un `webhook_config` (`version`, `status`, `alerts[]`, cada alerta con `labels`, `annotations`, `startsAt`, `endsAt`, `generatorURL`). No requiere transformación en Prometheus/Alertmanager — es el payload nativo, sin plugins.
- **Idempotencia**: Alertmanager reenvía notificaciones periódicamente mientras la alerta esté activa (`repeat_interval`) y en resolución (`resolved`). `AlertLifecycleService.RecordOccurrenceAsync` ya está diseñado para esto — dedupea por `MessageFingerprint` e incrementa `OccurrenceCount` en vez de crear una `Alert` nueva. El fingerprint de Alertmanager (determinístico por conjunto de labels) se usa como insumo de ese `MessageFingerprint`.

### 4.5 Traducción a modelo de dominio

- Alerta con `status: "firing"` → resolver el `Check` (`CheckType.Webhook`) vía label `piro_check_slug`, resolver su `AlertConfig` (uno por Check, como ya rige hoy), y llamar `AlertLifecycleService.RecordOccurrenceAsync` igual que lo haría `AlertEvaluationService` tras una ejecución interna — crea la `Alert` si no hay una activa con el mismo fingerprint, o incrementa `OccurrenceCount` si ya existe. `Check.CurrentStatus` se actualiza al severity mapeado (`critical→DOWN`, `warning→DEGRADED`, default `DEGRADED` si no está presente), participando de la agregación normal de `ServiceStatusService`.
- Alerta con `status: "resolved"` → resolver el mismo `Check`/fingerprint y llamar `AlertLifecycleService.ResolveActiveAlertAsync` — mismo call site que usa la resolución automática interna, sin reimplementar nada.
- **La `Alert` queda con `IncidentId = null`.** No se crea ni se toca ningún `Incident` en este flujo. Sigue siendo trabajo humano: alguien la revisa en el admin (`AlertsOverviewController`) y decide `POST /api/v1/alerts/{id}/incident` si amerita comunicación pública — exactamente el mismo camino que ya existe para alertas generadas por checks internos.
- `INotificationDispatcher` no se dispara por la sola llegada del webhook — solo cuando (y si) esa `Alert` se promueve a `Incident`, igual que hoy.

### 4.6 Qué NO cambia

- `ICheckExecutor` — no se toca; el `Check` de tipo `Webhook` simplemente no tiene executor registrado (mismo comportamiento que ya existe hoy para un `CheckType` sin executor: `LocalCheckJobDispatcher` ya maneja ese caso con `DataType = MONITOR_OUTAGE`, aunque aquí nunca se dispara porque no tiene `Cron` activo).
- `AlertConfig`, `AlertLifecycleService`, `AlertEvaluationService` — se reusan sin modificar su lógica; el webhook es una fuente adicional de "ocurrencias", no un pipeline paralelo.
- `IncidentAppService`, `INotificationDispatcher` — no se tocan; la promoción a incidente sigue siendo 100% manual como hoy.
- Esquema de `CheckDataPoint` — no aplica, este flujo no genera series de tiempo de check.

## 5. Alcance de datos / esquema

Cambios de esquema necesarios (a validar en implementación):

- `IntegrationType`: agregar valor `AlertmanagerWebhook`.
- `CheckType`: agregar valor `Webhook` (Check que nunca ejecuta, solo ancla `Alert`s entrantes).
- `Check`: ningún cambio de columnas — un Check `Webhook` usa `TypeDataJson` para guardar el label de correlación (`piro_check_slug` esperado) y puede dejar `Cron` en un valor que nunca dispare, o añadir un flag simple si se prefiere explicitud (`IsPassive`, a evaluar en implementación) en vez de inferirlo del `CheckType`.
- Ningún cambio a `Alert`, `AlertConfig`, `Incident`, `Service`, `CheckDataPoint` — se reusan tal cual.

## 6. Plan de fases

1. **Fase 1 — Ingesta básica**: `CheckType.Webhook`, endpoint + autenticación bearer + creación/resolución de `Alert` vía `AlertLifecycleService` por fingerprint, mapeo por label `piro_check_slug` obligatorio. Sin promoción automática a `Incident`. Sin UI de configuración todavía — `Integration` y `Check` se crean vía API/seed.
2. **Fase 2 — Admin UX**: pantalla en `apps/admin` para crear la integración, generar y mostrar el secreto una vez, crear el/los `Check` de tipo `Webhook` con su `AlertConfig`, copiar la URL del webhook y un snippet de configuración de Alertmanager listo para pegar. Las `Alert`s entrantes aparecen en el `AlertsOverviewController`/vista de alertas existente, sin distinción especial más que su `Check.Type`.
3. **Fase 3 — Auto-promoción opcional**: permitir configurar, por integración, un mapeo `severity→auto-promover a Incident` (ej. `critical` siempre crea+publica un `Incident` sin esperar al admin) para equipos que confían en sus reglas de Prometheus y quieren el status page reaccionando sin intervención humana. Esto es un flag explícito y opt-in, no el comportamiento default.
4. **Fase 4 (opcional, fuera de esta RFC)**: evaluar si conviene sumar la dirección de pull PromQL como `CheckType` adicional, para casos donde sí hay línea de vista Piro→Prometheus y se quiere un check de "métrica > umbral" sin pasar por una regla de Alertmanager.

## 7. Alternativas consideradas

- **Prometheus remote_write hacia un receiver en Piro**: descartado — implica que Piro entienda el protocolo remote_write (protobuf + snappy) y almacene series de tiempo, que es reinventar lo que Prometheus/Thanos ya hacen. Innecesario si el objetivo es "actuar en consecuencia", no "graficar métricas".
- **Piro como exporter de Prometheus** (`/metrics` en Piro.Api para que un Prometheus externo scrapee a Piro): es un problema distinto (observabilidad de Piro mismo, no de los servicios que Piro monitorea) y no resuelve el caso motivador. Podría ser un RFC separado si se quiere alimentar el status page con métricas propias de la plataforma.
- **PromQL pull por check** (ver §2): descartado para esta fase por network topology y porque duplica trabajo que Alertmanager ya hace.

## 8. Riesgos

- **Fiabilidad del mapeo por label**: si un equipo no configura `piro_check_slug` en sus reglas, la alerta no puede resolverse a un `Check` — debe registrarse un log/métrica interna de "alertas Alertmanager sin check_slug" para detectarlo en setup, y documentarlo claramente en la wiki.
- **Tormenta de reenvíos**: Alertmanager reenvía notificaciones activas periódicamente (`repeat_interval`, default 4h pero configurable a minutos) — sin la dedup por `MessageFingerprint` que ya provee `AlertLifecycleService`, cada reenvío crearía una `Alert` nueva en vez de incrementar `OccurrenceCount` de la existente, inflando el ruido en la vista de alertas del admin.
- **Un Check `Webhook` sin alertas nunca se ve "verde" en el mismo sentido que un check activo**: como no ejecuta nada, su `CurrentStatus` queda en el último valor recibido indefinidamente si Alertmanager deja de mandar `resolved` (ej. si Alertmanager se cae). A diferencia de un check normal, no hay heartbeat propio que lo detecte — vale la pena, en implementación, considerar un TTL o un check de "última señal recibida hace más de X" para no confiar en silencio como señal de salud.
- **Secreto expuesto en logs**: si se usa bearer token, asegurar que no quede logueado por el middleware HTTP de request logging — usar el mismo cuidado que ya se aplica a headers de `ApiKey`.
