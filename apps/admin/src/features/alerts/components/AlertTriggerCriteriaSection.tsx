import type { AlertDetail } from "@/lib/actions/alerts";
import AlertField from "./AlertField";

interface Props {
  alert: AlertDetail;
}

function AlertTriggerCriteriaSection(props: Props) {
  const { alert } = props;

  if (alert.source !== "Internal") {
    return (
      <p className="text-sm text-muted-foreground">
        This alert was triggered by an external source — Piro doesn't evaluate its own criteria for it.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-5">
        <AlertField label="Dimension">{alert.dimension}</AlertField>
        <AlertField label="Value">{alert.alertValue}</AlertField>
        <AlertField label="Failure threshold">{alert.failureThreshold}</AlertField>
        <AlertField label="Success threshold">{alert.successThreshold}</AlertField>
      </div>
      {alert.alertConfigDescription && (
        <AlertField label="Description">{alert.alertConfigDescription}</AlertField>
      )}
    </div>
  );
}

export default AlertTriggerCriteriaSection;
