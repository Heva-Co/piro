import { Link } from "react-router-dom";
import { ExternalLink, FileJson } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import { ROUTES } from "@/constants/routes";
import type { AlertDetail } from "@/lib/actions/alerts";
import { AlertSourceBadge } from "./AlertSourceBadge";
import AlertField from "./AlertField";

interface Props {
  alert: AlertDetail;
  onViewPayload: () => void;
}

function AlertOverviewSection(props: Props) {
  const { alert, onViewPayload } = props;
  const { formatDateTimeOrDash } = useFormattedDate();

  return (
    <div className="flex flex-col gap-5">
      {alert.source !== "Internal" ? (
        <AlertField label="Source">
          <div className="flex items-center gap-3">
            <AlertSourceBadge source={alert.source} sourceLabel={alert.sourceLabel} sourceIconifyIcon={alert.sourceIconifyIcon} />
            {alert.sourceUrl && (
              <a
                href={alert.sourceUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
              >
                View in {alert.sourceLabel ?? "source"} <ExternalLink size={12} />
              </a>
            )}
          </div>
        </AlertField>
      ) : (
        <div className="grid grid-cols-2 gap-5">
          <AlertField label="Check">
            {alert.checkSlug && alert.serviceSlug ? (
              <Link to={ROUTES.CHECKS.DETAIL(alert.serviceSlug, alert.checkSlug)} className="font-semibold hover:underline">
                {alert.checkName}
              </Link>
            ) : (
              <span className="text-muted-foreground">—</span>
            )}
          </AlertField>
          <AlertField label="Service">
            {alert.serviceSlug ? (
              <Link to={ROUTES.SERVICES.DETAIL(alert.serviceSlug)} className="font-semibold hover:underline">
                {alert.serviceName}
              </Link>
            ) : (
              <span className="text-muted-foreground">—</span>
            )}
          </AlertField>
        </div>
      )}

      <div className="grid grid-cols-2 gap-5">
        <AlertField label="Fired At">{formatDateTimeOrDash(alert.firedAt)}</AlertField>
        <AlertField label="Resolved At">
          {alert.resolvedAt ? formatDateTimeOrDash(alert.resolvedAt) : <span className="text-red-600 font-medium">Active</span>}
        </AlertField>
      </div>

      <div className="grid grid-cols-2 gap-5">
        <AlertField label="Occurrences">{alert.occurrenceCount}</AlertField>
        <AlertField label="Severity">{alert.severity ?? "—"}</AlertField>
      </div>

      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-muted-foreground">Message</span>
        <p className="text-sm rounded-lg border bg-muted/30 px-3 py-2 font-mono">
          {alert.message ?? "—"}
        </p>
      </div>

      {alert.sourceRawPayload && (
        <div>
          <Button type="button" variant="outline" size="sm" onClick={onViewPayload}>
            <FileJson size={13} />
            View raw payload
          </Button>
        </div>
      )}
    </div>
  );
}

export default AlertOverviewSection;
