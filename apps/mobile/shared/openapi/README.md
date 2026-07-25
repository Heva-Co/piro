# Generated API models

The mobile wire models are generated from the backend's **OpenAPI document** — the Kotlin analogue of
`apps/admin`'s `pnpm run generate:api-types` (which produces `src/lib/api-types.ts`). This keeps the
client models from silently drifting from the API contract, which is exactly what bit the alert models
before (`acknowledgedAt` is an `int64`, not a string).

## Files

| Path | What |
|---|---|
| `piro-openapi.v1.json` | Committed snapshot of `GET /openapi/v1.json` — the source of truth for generation. |
| `../generated/src/commonMain/kotlin/co/heva/piro/shared/model/generated/` | Generated `@Serializable` models (committed, compiled into `commonMain`). |

## Regenerating

```bash
# 1. Refresh the spec snapshot from a running API (ASPNETCORE_ENVIRONMENT=Development dotnet run):
curl -s http://localhost:5117/openapi/v1.json -o apps/mobile/shared/openapi/piro-openapi.v1.json

# 2. Regenerate the Kotlin models:
cd apps/mobile
./gradlew :shared:openApiGenerate --no-configuration-cache
```

Configuration lives in `shared/build.gradle.kts` (`openApiGenerate { … }`):

- **`generatorName = "kotlin"`**, `serializationLibrary = kotlinx_serialization` → `@Serializable` data classes.
- **`dateLibrary = "string"`** → `date-time` fields become `String` (KMP-safe; no `java.time`, which
  doesn't exist in `commonMain`).
- **`globalProperties.models = "…"`** → only the models the app actually consumes are generated. The full
  199-schema surface isn't: many unused schemas map `number`/`uri` to `java.*` types that don't exist in
  `commonMain`. **When the app starts calling a new endpoint, add its DTO(s) to that list and regenerate.**

## Migration status

Generated models live in `co.heva.piro.shared.model.generated`. Hand-written models in
`co.heva.piro.shared.model` (which now match the contract) are migrated onto the generated ones
incrementally — the same partial-migration approach `apps/admin` documents. Auth/OIDC and alert schemas
are already generated and ready to adopt; `RegisterDeviceRequest`/`DeviceDto`/refresh aren't in the
OpenAPI document yet, so those stay hand-written until the backend documents them.
