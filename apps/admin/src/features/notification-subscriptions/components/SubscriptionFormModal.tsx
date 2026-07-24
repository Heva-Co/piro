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
});

type FormValues = z.infer<typeof schema>;

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
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", destination: "", events: [], minSeverity: "Warning", target: "", enabled: true },
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
    });
  }, [existing, reset]);

  const eventOptions = useMemo(
    () => (catalogQuery.data ?? []).map((e) => ({ value: e.name, label: e.name })),
    [catalogQuery.data],
  );

  const destination = watch("destination");
  const [destKind] = destination ? destination.split(":") : [""];

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
