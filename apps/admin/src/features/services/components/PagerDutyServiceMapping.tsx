import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  integrationOAuthApi,
  serviceIntegrationMappingApi,
  type Integration,
} from "@/lib/actions/integrations";

interface Props {
  serviceId: number;
  /** A connected PagerDuty integration to map this service against. */
  integration: Integration;
}

/**
 * Maps this Piro service to a PagerDuty service for one connected PagerDuty integration (RFC 0004
 * §4.5). Discovers the account's PagerDuty services live, lets the admin pick one, and persists the
 * pairing (the backend resolves/provisions the routing key). Deliberately PagerDuty-specific and
 * self-contained so it can become the first "action provider" when the generic RFC 0012 framework
 * is built, without a rewrite.
 */
export function PagerDutyServiceMapping(props: Props) {
  const { serviceId, integration } = props;
  const qc = useQueryClient();

  const { data: discovered, isLoading: discovering, error: discoverError } = useQuery({
    queryKey: ["integration-discover", integration.id],
    queryFn: () => integrationOAuthApi.discover(integration.id),
    staleTime: 60_000,
  });

  const { data: mappings } = useQuery({
    queryKey: ["service-integration-mappings", serviceId],
    queryFn: () => serviceIntegrationMappingApi.list(serviceId),
  });

  const currentMapping = mappings?.find((m) => m.integrationId === integration.id);

  const upsert = useMutation({
    mutationFn: (remoteId: string) =>
      serviceIntegrationMappingApi.upsert(serviceId, { integrationId: integration.id, remoteId }),
    onSuccess: () => {
      toast.success("PagerDuty service mapped.");
      qc.invalidateQueries({ queryKey: ["service-integration-mappings", serviceId] });
    },
    onError: (err: unknown) => {
      const message =
        err && typeof err === "object" && "message" in err ? String(err.message) : "Failed to map.";
      toast.error(message);
    },
  });

  const remove = useMutation({
    mutationFn: () => serviceIntegrationMappingApi.remove(serviceId, integration.id),
    onSuccess: () => {
      toast.success("Mapping removed.");
      qc.invalidateQueries({ queryKey: ["service-integration-mappings", serviceId] });
    },
    onError: () => toast.error("Failed to remove mapping."),
  });

  return (
    <div className="flex flex-col gap-2">
      <Label>{integration.name}</Label>

      {discovering && (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Discovering PagerDuty services…
        </p>
      )}

      {discoverError && (
        <p className="text-sm text-destructive">
          Couldn't list PagerDuty services. The integration may need to be reconnected.
        </p>
      )}

      {discovered && (
        <div className="flex items-center gap-2">
          <Select
            value={currentMapping?.remoteId ?? undefined}
            onValueChange={(remoteId) => {
              if (remoteId) upsert.mutate(remoteId);
            }}
            disabled={upsert.isPending}
          >
            <SelectTrigger className="flex-1">
              <SelectValue placeholder="Select a PagerDuty service…" />
            </SelectTrigger>
            <SelectContent>
              {discovered
                .filter((svc): svc is typeof svc & { remoteId: string } => Boolean(svc.remoteId))
                .map((svc) => (
                  <SelectItem key={svc.remoteId} value={svc.remoteId}>
                    {svc.label}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>

          {currentMapping && (
            <Button
              variant="outline"
              size="icon"
              onClick={() => remove.mutate()}
              disabled={remove.isPending}
              title="Remove mapping"
            >
              {remove.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
            </Button>
          )}
        </div>
      )}

      <p className="text-xs text-muted-foreground">
        When a check in this service fails, Piro pages the mapped PagerDuty service.
      </p>
    </div>
  );
}
