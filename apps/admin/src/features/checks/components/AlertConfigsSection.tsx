import { useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  useAlertConfigs,
  useCreateAlertConfig,
  useUpdateAlertConfig,
  useDeleteAlertConfig,
} from "@/hooks/useChecks";
import { ALLOWED_ALERT_FORS } from "@/constants/checks";
import { AlertConfigRow, type AlertConfigDraft } from "@/features/checks/components/AlertConfigRow";
import type { AlertConfig } from "@/lib/actions/alert-configs";
import { type AlertFor, DEFAULT_ALERT_FOR, DEFAULT_ALERT_SEVERITY, DEFAULT_ALERT_VALUES } from "@/types/checks";

function defaultAlertConfigDraft(alertForOptions: readonly AlertFor[]): AlertConfigDraft {
  const alertFor = alertForOptions[0] ?? DEFAULT_ALERT_FOR;
  return {
    alertFor,
    alertValue: DEFAULT_ALERT_VALUES[alertFor] ?? "",
    failureThreshold: 1,
    successThreshold: 1,
    severity: DEFAULT_ALERT_SEVERITY,
    isActive: true,
  };
}

function toDraft(config: AlertConfig): AlertConfigDraft {
  return {
    alertFor: config.alertFor,
    alertValue: config.alertValue,
    failureThreshold: config.failureThreshold,
    successThreshold: config.successThreshold,
    severity: config.severity,
    isActive: config.isActive,
  };
}

/** A row still being added has no server id yet — tracked locally until its first save. */
interface NewAlertConfigRow {
  key: string;
}

interface Props {
  serviceSlug: string;
  checkSlug: string;
  checkType: string;
}

export function AlertConfigsSection(props: Props) {
  const { serviceSlug, checkSlug, checkType } = props;
  const { data: alertConfigs, isLoading } = useAlertConfigs(serviceSlug, checkSlug);
  const createAlertConfig = useCreateAlertConfig(serviceSlug, checkSlug);
  const updateAlertConfig = useUpdateAlertConfig(serviceSlug, checkSlug);
  const deleteAlertConfig = useDeleteAlertConfig(serviceSlug, checkSlug);

  const alertForOptions = ALLOWED_ALERT_FORS[checkType] ?? [DEFAULT_ALERT_FOR];

  const [newRows, setNewRows] = useState<NewAlertConfigRow[]>([]);

  function addRow() {
    setNewRows((prev) => [...prev, { key: crypto.randomUUID() }]);
  }

  function removeRow(key: string) {
    setNewRows((prev) => prev.filter((r) => r.key !== key));
  }

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading…</div>;
  }

  const savedConfigs = alertConfigs ?? [];

  return (
    <div className="flex flex-col gap-3">
      {savedConfigs.length === 0 && newRows.length === 0 && (
        <p className="text-sm text-muted-foreground">No alert configurations yet. Add one below.</p>
      )}

      {savedConfigs.map((config) => (
        <AlertConfigRow
          key={config.id}
          initial={toDraft(config)}
          saved={config}
          alertForOptions={alertForOptions}
          onSave={async (draft) => {
            await updateAlertConfig.mutateAsync({ id: config.id, data: draft });
          }}
          onRemove={() => deleteAlertConfig.mutate(config.id)}
          isSaving={updateAlertConfig.isPending}
        />
      ))}

      {newRows.map((row) => (
        <AlertConfigRow
          key={row.key}
          initial={defaultAlertConfigDraft(alertForOptions)}
          saved={null}
          alertForOptions={alertForOptions}
          onSave={async (draft) => {
            await createAlertConfig.mutateAsync(draft);
            removeRow(row.key);
          }}
          onRemove={() => removeRow(row.key)}
          isSaving={createAlertConfig.isPending}
        />
      ))}

      <Button type="button" variant="ghost" size="sm" onClick={addRow} className="self-start">
        <Plus size={14} />
        Add alert configuration
      </Button>
    </div>
  );
}
