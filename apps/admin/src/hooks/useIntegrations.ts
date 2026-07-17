import { useQuery } from "@tanstack/react-query";
import { integrationsApi } from "@/lib/actions/integrations";
import { QUERY_KEYS } from "@/constants/api";

/** Recent inbound webhook requests for an Integration — RFC 0001 §4.4. Shared query key so the section body and its header Refresh action stay in sync. */
export function useIntegrationWebhookLogs(integrationId: string) {
  return useQuery({
    queryKey: QUERY_KEYS.INTEGRATION_WEBHOOK_LOGS(integrationId),
    queryFn: () => integrationsApi.getWebhookLogs(integrationId),
  });
}
