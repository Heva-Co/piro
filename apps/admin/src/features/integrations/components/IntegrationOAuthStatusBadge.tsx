import { useQuery } from "@tanstack/react-query";
import { CheckCircle2, Link2Off } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { integrationOAuthApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";

interface Props {
  integrationId: string;
}

/**
 * Status pill for an OAuth-backed integration in the list: reflects the real connection state from
 * the per-integration status endpoint (green "Connected" / amber "Not connected"), rather than
 * inferring health from stored credentials — the app's client id/secret being present says nothing
 * about whether the OAuth handshake was ever completed.
 */
function IntegrationOAuthStatusBadge(props: Props) {
  const { integrationId } = props;

  const { data: status, isLoading } = useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_OAUTH_STATUS(integrationId),
    queryFn: () => integrationOAuthApi.status(integrationId),
  });

  if (isLoading) return <Skeleton className="h-5 w-24 rounded-full" />;

  if (status?.connected) {
    return (
      <Badge
        variant="outline"
        className="border-emerald-500/30 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
      >
        <CheckCircle2 />
        Connected
      </Badge>
    );
  }

  return (
    <Badge
      variant="outline"
      className="border-amber-500/30 bg-amber-500/10 text-amber-600 dark:text-amber-400"
    >
      <Link2Off />
      Not connected
    </Badge>
  );
}

export default IntegrationOAuthStatusBadge;
