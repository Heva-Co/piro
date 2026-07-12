import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useForm, FormProvider } from "react-hook-form";
import { Settings, Wrench } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { useCreateCheck } from "@/hooks/useChecks";
import { useService } from "@/hooks/useServices";
import { checkTypesApi, integrationsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { HttpConfig, DnsConfig, TcpConfig, PingConfig, SslConfig, HeartbeatConfig, GcpCloudRunJobConfig } from "@/features/checks/components";
import { CheckGeneralSettingsFields, type CheckGeneralFormValues } from "@/features/checks/components/CheckGeneralSettingsFields";
import { CHECK_TYPE_LABELS, CHECK_TYPE_DEFAULTS } from "@/constants/checks";
import { slugify } from "@/utils/slugify";

type CheckType = string;

function buildConfig(type: CheckType, config: Record<string, unknown>): Record<string, unknown> {
  if (type === "HTTP") {
    const headers = (config.headers as { key: string; value: string }[]) ?? [];
    return {
      url: config.url,
      method: config.method ?? "GET",
      timeout: config.timeout ?? 5000,
      expectedStatusCodes: Array.isArray(config.expectedStatusCodes)
        ? config.expectedStatusCodes
        : String(config.expectedStatusCodes ?? "200").split(",").map((s) => parseInt(s.trim(), 10)).filter((n) => !isNaN(n)),
      followRedirects: config.followRedirects ?? true,
      body: config.body || undefined,
      headers: Object.fromEntries(headers.filter((h) => h.key).map((h) => [h.key, h.value])),
    };
  }
  if (type === "DNS") {
    const nameServers = ((config.nameServers as string[]) ?? []).filter(Boolean);
    return {
      host: config.host,
      recordType: config.recordType ?? "A",
      expectedValue: (config.expectedValue as string) || undefined,
      nameServers: nameServers.length > 0 ? nameServers : undefined,
      degradedAfter: config.degradedAfter || undefined,
      downAfter: config.downAfter || undefined,
    };
  }
  return config;
}

export default function CheckFormPage() {
  const { slug: serviceSlug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { data: service } = useService(serviceSlug!);
  const createCheck = useCreateCheck(serviceSlug!);

  const { data: checkTypes = [] } = useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });
  const { data: integrations = [] } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATIONS,
    queryFn: integrationsApi.list,
  });

  const [checkSlug, setCheckSlug] = useState("");
  const [slugManual, setSlugManual] = useState(false);
  const [type, setType] = useState<CheckType>("HTTP");
  const [config, setConfig] = useState<Record<string, unknown>>(CHECK_TYPE_DEFAULTS.HTTP);
  const [submitError, setSubmitError] = useState("");

  const methods = useForm<CheckGeneralFormValues>({
    defaultValues: {
      name: "",
      description: "",
      cron: "* * * * *",
      showCustomCron: false,
      isActive: true,
      isMultiRegion: false,
    },
  });

  const watchedName = methods.watch("name");
  if (!slugManual && watchedName !== undefined) {
    const auto = slugify(watchedName);
    if (auto !== checkSlug) setCheckSlug(auto);
  }

  function handleTypeChange(t: CheckType) {
    setType(t);
    setConfig(CHECK_TYPE_DEFAULTS[t] ?? {});
  }

  async function handleSubmit(values: CheckGeneralFormValues) {
    setSubmitError("");
    try {
      const integrationId = config.integrationId ? Number(config.integrationId) : undefined;
      const { integrationId: _removed, ...typeConfig } = config;
      void _removed;
      const check = await createCheck.mutateAsync({
        slug: checkSlug,
        name: values.name,
        description: values.description || undefined,
        type,
        cron: values.cron,
        typeDataJson: JSON.stringify(buildConfig(type, typeConfig)),
        isActive: values.isActive,
        isMultiRegion: values.isMultiRegion,
        integrationId,
      });
      navigate(ROUTES.CHECKS.DETAIL(serviceSlug!, check.slug));
    } catch (err: unknown) {
      setSubmitError(err instanceof Error ? err.message : "Failed to create check.");
    }
  }

  const typeSelect = (
    <Select value={type} onValueChange={(v) => v && handleTypeChange(v)}>
      <SelectTrigger className="w-full">
        <SelectValue>{(v: string) => CHECK_TYPE_LABELS[v] ?? v}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {checkTypes.map((t) => (
          <SelectItem key={t.type} value={t.type}>{CHECK_TYPE_LABELS[t.type] ?? t.type}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  );

  const slugField = (
    <>
      <label className="text-sm font-semibold">Slug <span className="text-destructive">*</span></label>
      <Input
        value={checkSlug}
        onChange={(e) => {
          setSlugManual(true);
          setCheckSlug(e.target.value);
        }}
        placeholder="health"
        className="font-mono"
      />
      <p className="text-xs text-muted-foreground">Unique identifier within this service</p>
    </>
  );

  return (
    <>
      <FormProvider {...methods}>
        <form onSubmit={methods.handleSubmit(handleSubmit)}>
          {/* Breadcrumb */}
          <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
            <button type="button" onClick={() => navigate(ROUTES.SERVICES.LIST)} className="hover:text-foreground transition-colors">
              Services
            </button>
            <span>/</span>
            <button type="button" onClick={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))} className="hover:text-foreground transition-colors">
              {service?.name ?? serviceSlug}
            </button>
            <span>/</span>
            <span className="text-foreground font-medium">New Check</span>
          </nav>

          {submitError && (
            <div className="mb-4 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
              {submitError}
            </div>
          )}

          <SectionAccordion
            title="General Settings"
            description="Basic information about this check"
            icon={<Settings size={16} className="text-muted-foreground" />}
            defaultOpen
          >
            <CheckGeneralSettingsFields typeNode={typeSelect} slugNode={slugField} />
          </SectionAccordion>

          <SectionAccordion
            title="Configuration"
            description={`Settings for the ${CHECK_TYPE_LABELS[type] ?? type} check`}
            icon={<Wrench size={16} className="text-muted-foreground" />}
            defaultOpen
          >
            {type === "HTTP" && <HttpConfig config={config} onChange={setConfig} />}
            {type === "DNS" && <DnsConfig config={config} onChange={setConfig} />}
            {type === "TCP" && <TcpConfig config={config} onChange={setConfig} />}
            {type === "Ping" && <PingConfig config={config} onChange={setConfig} />}
            {type === "SSL" && <SslConfig config={config} onChange={setConfig} />}
            {type === "Heartbeat" && <HeartbeatConfig config={config} onChange={setConfig} />}
            {type === "GCP_CloudRunJob" && (
              <GcpCloudRunJobConfig config={config} onChange={setConfig} integrations={integrations} />
            )}
          </SectionAccordion>

          {/* ── Footer actions ── */}
          <div className="flex items-center justify-between mt-6">
            <button type="button"
              onClick={() => navigate(ROUTES.SERVICES.DETAIL(serviceSlug!))}
              className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={createCheck.isPending}
              className="flex items-center gap-2 rounded-lg bg-foreground text-background px-5 py-2 text-sm font-semibold hover:opacity-90 disabled:opacity-50 transition-opacity">
              <Settings size={14} />
              {createCheck.isPending ? "Creating…" : "Create Check"}
            </button>
          </div>
        </form>
      </FormProvider>
    </>
  );
}
