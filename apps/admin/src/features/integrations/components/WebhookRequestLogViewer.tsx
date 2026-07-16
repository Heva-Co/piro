import { useState } from "react";
import { CheckCircle2, XCircle, HelpCircle, ExternalLink, FileJson } from "lucide-react";
import { Link } from "react-router-dom";
import { ROUTES } from "@/constants/routes";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { useIntegrationWebhookLogs } from "@/hooks/useIntegrations";
import { Button } from "@/components/ui/button";
import { PayloadDialog } from "@/components/PayloadDialog";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";

interface Props {
  integrationId: string;
}

const OUTCOME_LABELS: Record<string, string> = {
  Accepted: "Accepted",
  AcceptedOrphan: "Accepted (orphan)",
  CorrelationMismatch: "Correlation mismatch",
  AuthFailed: "Auth failed",
  ParseError: "Parse error",
  Deduplicated: "Deduplicated",
};

const FAILURE_OUTCOMES = new Set(["AuthFailed", "ParseError"]);

/** Read-only log of an Integration's inbound webhook requests — RFC 0001 §4.4. */
export function WebhookRequestLogViewer(props: Props) {
  const { integrationId } = props;
  const { formatDateTime } = useFormattedDate();
  const [payloadLogId, setPayloadLogId] = useState<number | null>(null);

  const { data: logs = [], isLoading } = useIntegrationWebhookLogs(integrationId);

  const payloadLog = logs.find((l) => l.id === payloadLogId);

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading…</p>;
  }

  return (
    <div className="flex flex-col gap-3">
      {logs.length === 0 ? (
        <p className="text-sm text-muted-foreground">No requests received yet.</p>
      ) : (
        <div className="rounded-xl border bg-card overflow-hidden">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/40">
                <th className="px-4 py-2.5 text-left text-xs font-semibold text-muted-foreground">Received</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold text-muted-foreground">Outcome</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold text-muted-foreground">Alert</th>
                <th className="px-4 py-2.5" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {logs.map((log) => {
                const isFailure = FAILURE_OUTCOMES.has(log.outcome);
                return (
                  <tr key={log.id}>
                    <td className="px-4 py-2.5 text-muted-foreground">{formatDateTime(log.receivedAt)}</td>
                    <td className="px-4 py-2.5">
                      <span className={`inline-flex items-center gap-1.5 ${isFailure ? "text-destructive" : "text-green-600 dark:text-green-400"}`}>
                        {isFailure ? <XCircle size={14} /> : <CheckCircle2 size={14} />}
                        {OUTCOME_LABELS[log.outcome] ?? log.outcome}
                      </span>
                    </td>
                    <td className="px-4 py-2.5">
                      {log.alertId != null ? (
                        <Link to={ROUTES.ALERTS.DETAIL(log.alertId)} className="inline-flex items-center gap-1 text-muted-foreground hover:text-foreground transition-colors">
                          #{log.alertId} <ExternalLink size={12} />
                        </Link>
                      ) : (
                        <Tooltip>
                          <TooltipTrigger render={<HelpCircle size={14} className="text-muted-foreground" />} />
                          <TooltipContent>No alert was created or updated for this request.</TooltipContent>
                        </Tooltip>
                      )}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      <Button type="button" variant="ghost" size="sm" onClick={() => setPayloadLogId(log.id)}>
                        <FileJson size={13} />
                        View payload
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {payloadLog && (
        <PayloadDialog
          open={payloadLogId != null}
          onOpenChange={(open) => !open && setPayloadLogId(null)}
          title="Webhook payload"
          description="Exact request body received, unmodified."
          payload={payloadLog.rawPayload}
        />
      )}
    </div>
  );
}
