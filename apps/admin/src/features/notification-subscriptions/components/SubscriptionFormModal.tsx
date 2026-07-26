import { useEffect, useMemo } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery } from "@tanstack/react-query";
import { MultiSelect } from "@/components/ui/multi-select";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { QUERY_KEYS } from "@/constants/api";
import { notificationSubscriptionsApi } from "@/lib/actions/notification-subscriptions";
import type {
  NotificationSubscription,
  UpsertNotificationSubscriptionRequest,
} from "@/lib/actions/notification-subscriptions";
import { integrationsApi, integrationTypesApi } from "@/lib/actions/integrations";
import { usersApi } from "@/lib/api";
import type { components } from "@/lib/api-types";
import TagSelectorEditor from "./TagSelectorEditor";

type TagSelector = components["schemas"]["TagSelector"];

// Capabilities that make an integration a valid notification destination (outbound). Inbound types
// (e.g. GoogleCloud, GcpCloudMonitoringWebhook — CreatesAlerts) must not appear as a destination.
const OUTBOUND_CAPABILITIES = ["SendsPersonalNotification", "SendsChannelNotification"];

// Destination is a single encoded value ("person:3" / "integration:<guid>"); the target kind is
// derived from it — a person is Personal, a notification integration posts to a Group, a third-party
// platform is an Integration subscriber (RFC 0009 §4.4).
const schema = z.object({
  name: z.string().min(1, "Name is required").max(200, "Max 200 characters"),
  destination: z.string().min(1, "Pick a destination"),
  events: z.array(z.string()).min(1, "Pick at least one event"),
  minSeverity: z.enum(["Warning", "Critical"]),
  target: z.string().max(256).optional(),
  enabled: z.boolean(),
  // The tag selector's shape is validated server-side; here it's an opaque nullable object the
  // TagSelectorEditor produces and consumes. null means "no tag filter".
  filter: z.custom<TagSelector | null>().nullable(),
});

type FormValues = z.infer<typeof schema>;

// Mirrors IntegrationManifest.HandlesEvent on the backend (issue #212): "*" matches everything,
// "alert:*" matches any "alert:…" wire name, and an exact pattern matches itself.
function handlesEvent(patterns: string[], wireName: string): boolean {
  return patterns.some(
    (p) => p === "*" || p === wireName || (p.endsWith(":*") && wireName.startsWith(p.slice(0, -1))),
  );
}

interface Props {
  existing: NotificationSubscription | null;
  saving: boolean;
  error: string | null;
  onCancel: () => void;
  onSubmit: (request: UpsertNotificationSubscriptionRequest) => void;
}

function SubscriptionFormModal(props: Props) {
  const { existing, saving, error, onCancel, onSubmit } = props;

  const {
    register,
    handleSubmit,
    control,
    watch,
    reset,
    setValue,
    getValues,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", destination: "", events: [], minSeverity: "Warning", target: "", enabled: true, filter: null },
  });

  const catalogQuery = useQuery({
    queryKey: QUERY_KEYS.NOTIFICATION_EVENT_CATALOG,
    queryFn: () => notificationSubscriptionsApi.eventCatalog(),
  });
  const usersQuery = useQuery({ queryKey: QUERY_KEYS.USERS, queryFn: () => usersApi.list() });
  const integrationsQuery = useQuery({ queryKey: QUERY_KEYS.INTEGRATIONS, queryFn: () => integrationsApi.list() });
  const typesQuery = useQuery({ queryKey: QUERY_KEYS.INTEGRATION_TYPES, queryFn: () => integrationTypesApi.list() });

  // Only integration types that declare an outbound capability can be a destination.
  const outboundTypes = useMemo(() => {
    const set = new Set<string>();
    for (const t of typesQuery.data ?? []) {
      if (t.capabilities.some((c) => OUTBOUND_CAPABILITIES.includes(c))) set.add(t.type);
    }
    return set;
  }, [typesQuery.data]);

  const notifiableIntegrations = useMemo(
    () => (integrationsQuery.data ?? []).filter((i) => outboundTypes.has(String(i.type))),
    [integrationsQuery.data, outboundTypes],
  );

  useEffect(() => {
    if (!existing) return;
    reset({
      name: existing.name,
      destination:
        existing.userId != null
          ? `person:${existing.userId}`
          : existing.integrationId != null
            ? `integration:${existing.integrationId}`
            : "",
      events: existing.events,
      minSeverity: existing.minSeverity,
      target: existing.target ?? "",
      enabled: existing.enabled,
      filter: existing.filter ?? null,
    });
  }, [existing, reset]);

  const destination = watch("destination");
  const [destKind, destId] = destination ? destination.split(":") : ["", ""];

  // The event wire-name patterns the selected destination handles, mirroring the backend guard
  // (IntegrationManifest.HandlesEvent, issue #212). Only integration (Channel) destinations are
  // scoped — a Personal destination has no per-integration event set, so it sees the full catalog.
  const supportedPatterns = useMemo<string[] | null>(() => {
    if (destKind !== "integration") return null;
    const integration = (integrationsQuery.data ?? []).find((i) => i.id === destId);
    if (!integration) return null;
    const type = (typesQuery.data ?? []).find((t) => t.type === String(integration.type));
    return type?.supportedEvents ?? [];
  }, [destKind, destId, integrationsQuery.data, typesQuery.data]);

  // The scoped event menu: the full catalog, filtered to what the destination supports when it is an
  // integration. Same wildcard semantics as the backend ("*", "alert:*", exact match).
  const eventOptions = useMemo(() => {
    const all = (catalogQuery.data ?? []).map((e) => ({ value: e.name, label: e.name }));
    if (supportedPatterns === null) return all;
    return all.filter((o) => handlesEvent(supportedPatterns, o.value));
  }, [catalogQuery.data, supportedPatterns]);

  // When the destination changes to an integration that doesn't support some already-picked events,
  // drop them so the form can't submit a selection the backend guard would reject (issue #212).
  useEffect(() => {
    if (supportedPatterns === null) return;
    const current = getValues("events");
    const kept = current.filter((e) => handlesEvent(supportedPatterns, e));
    if (kept.length !== current.length) setValue("events", kept, { shouldValidate: true });
  }, [supportedPatterns, getValues, setValue]);

  // Human label for the currently selected destination — SelectValue would otherwise show the raw
  // encoded value ("person:3").
  function destinationLabel(value: string): string | null {
    if (!value) return null;
    const [kind, id] = value.split(":");
    if (kind === "person") {
      const u = (usersQuery.data ?? []).find((x) => String(x.id) === id);
      return u ? `${u.name || u.email} (person)` : null;
    }
    const i = notifiableIntegrations.find((x) => x.id === id);
    return i ? `${i.name} (${i.type})` : null;
  }

  function submit(values: FormValues) {
    const [kind, id] = values.destination.split(":");
    const isPerson = kind === "person";
    onSubmit({
      name: values.name.trim(),
      events: values.events,
      minSeverity: values.minSeverity,
      targetKind: isPerson ? "Personal" : "Channel",
      userId: isPerson ? Number(id) : null,
      integrationId: isPerson ? null : id,
      target: isPerson ? null : values.target?.trim() || null,
      enabled: values.enabled,
      filter: values.filter ?? null,
    });
  }

  const shownError = error;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
      <div className="bg-card border border-border rounded-xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
        <div className="border-b border-border px-6 py-4">
          <h2 className="text-lg font-semibold">{existing ? "Edit subscription" : "New subscription"}</h2>
          <p className="text-sm text-muted-foreground">Route catalog events to a person or a channel.</p>
        </div>

        <form onSubmit={handleSubmit(submit)}>
          <div className="flex flex-col gap-4 px-6 py-4">
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Name</label>
              <Input {...register("name")} placeholder="Prod alerts → Ops" aria-invalid={!!errors.name} />
              {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
            </div>

            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Destination</label>
              <Controller
                name="destination"
                control={control}
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Select a person or integration…">
                        {destinationLabel(field.value)}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {(usersQuery.data ?? []).map((u) => (
                        <SelectItem key={`person:${u.id}`} value={`person:${u.id}`}>
                          {u.name || u.email} (person)
                        </SelectItem>
                      ))}
                      {notifiableIntegrations.map((i) => (
                        <SelectItem key={`integration:${i.id}`} value={`integration:${i.id}`}>
                          {i.name} ({i.type})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.destination && <p className="text-xs text-destructive">{errors.destination.message}</p>}
              {destKind === "integration" && (
                <p className="text-xs text-muted-foreground">
                  Delivered to the integration's channel.
                </p>
              )}
            </div>

            {destKind === "integration" && (
              <div className="flex flex-col gap-1">
                <label className="text-sm font-medium">Target (optional)</label>
                <Input {...register("target")} placeholder="channel / room / topic" />
                <p className="text-xs text-muted-foreground">Leave blank if the integration self-addresses.</p>
              </div>
            )}

            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Events</label>
              <Controller
                name="events"
                control={control}
                render={({ field }) => (
                  <MultiSelect options={eventOptions} value={field.value} onChange={field.onChange} placeholder="Select events…" />
                )}
              />
              {errors.events && <p className="text-xs text-destructive">{errors.events.message}</p>}
            </div>

            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Minimum severity</label>
              <Controller
                name="minSeverity"
                control={control}
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Warning">Warning</SelectItem>
                      <SelectItem value="Critical">Critical</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              <p className="text-xs text-muted-foreground">Alert events below this severity are not sent.</p>
            </div>

            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium">Tag filter (optional)</label>
              <Controller
                name="filter"
                control={control}
                render={({ field }) => (
                  <TagSelectorEditor value={field.value ?? null} onChange={field.onChange} />
                )}
              />
              <p className="text-xs text-muted-foreground">
                Only fire for alerts whose service has these tags (e.g. <span className="font-mono">env equals production</span>).
              </p>
            </div>

            <div className="flex items-center justify-between">
              <label className="text-sm font-medium">Enabled</label>
              <Controller
                name="enabled"
                control={control}
                render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} />}
              />
            </div>

            {shownError && <p className="text-xs text-destructive">{shownError}</p>}
          </div>

          <div className="border-t border-border px-6 py-4 flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>Cancel</Button>
            <Button type="submit" disabled={saving}>
              {saving ? "Saving…" : existing ? "Save" : "Create"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default SubscriptionFormModal;
