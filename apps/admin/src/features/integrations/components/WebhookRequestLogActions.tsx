import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useIntegrationWebhookLogs } from "@/hooks/useIntegrations";

interface Props {
  integrationId: string;
}

function WebhookRequestLogActions(props: Props) {
  const { integrationId } = props;
  const { isFetching, refetch } = useIntegrationWebhookLogs(integrationId);

  return (
    <Button
      onClick={() => refetch()}
      disabled={isFetching}
      className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
    >
      <RefreshCw size={12} className={isFetching ? "animate-spin" : ""} />
      Refresh
    </Button>
  );
}

export default WebhookRequestLogActions;
