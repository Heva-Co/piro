import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Icon } from "@iconify/react";
import { Loader2, Link2, Unlink } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { integrationOAuthApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";

interface Props {
  /** The integration being connected — must already be saved (has an id). */
  integrationId: string;
  /** Iconify icon for the provider, from the manifest. */
  iconifyIcon?: string | null;
  /** Provider label, from the manifest. */
  label?: string | null;
}

/**
 * Connect / Disconnect control for an OAuth-backed integration (RFC 0004). Rendered only when the
 * integration type's manifest declares RequiresOAuthConnection — the button is manifest-driven, not
 * hardcoded per provider. Connecting redirects the browser to the provider's authorization page.
 */
export function IntegrationOAuthConnect(props: Props) {
  const { integrationId, iconifyIcon, label } = props;
  const qc = useQueryClient();

  const { data: status, isLoading } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_OAUTH_STATUS(integrationId),
    queryFn: () => integrationOAuthApi.status(integrationId),
  });

  const connect = useMutation({
    mutationFn: () => integrationOAuthApi.connect(integrationId),
    onSuccess: (data) => {
      // Hand the browser off to the provider's consent screen.
      window.location.href = data.authorizationUrl;
    },
    onError: (err: unknown) => {
      const message =
        err && typeof err === "object" && "message" in err ? String(err.message) : "Failed to start connection.";
      toast.error(message);
    },
  });

  const disconnect = useMutation({
    mutationFn: () => integrationOAuthApi.disconnect(integrationId),
    onSuccess: () => {
      toast.success("Disconnected.");
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INTEGRATION_OAUTH_STATUS(integrationId) });
    },
    onError: () => toast.error("Failed to disconnect."),
  });

  const providerName = label ?? "provider";

  return (
    <div className="flex items-center justify-between">
      <div className="flex items-center gap-3">
        {iconifyIcon && <Icon icon={iconifyIcon} className="h-6 w-6" />}
        <div>
          <p className="text-sm font-medium">
            {isLoading
              ? "Checking connection…"
              : status?.connected
                ? `Connected to ${providerName}`
                : `Not connected to ${providerName}`}
          </p>
          {status?.connected && status.expiresAt && (
            <p className="text-xs text-muted-foreground">
              Token expires {new Date(status.expiresAt).toLocaleString()}
            </p>
          )}
        </div>
      </div>

      {status?.connected ? (
        <Button variant="outline" onClick={() => disconnect.mutate()} disabled={disconnect.isPending}>
          {disconnect.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Unlink className="h-4 w-4" />}
          Disconnect
        </Button>
      ) : (
        <Button onClick={() => connect.mutate()} disabled={connect.isPending || isLoading}>
          {connect.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Link2 className="h-4 w-4" />}
          Connect
        </Button>
      )}
    </div>
  );
}
