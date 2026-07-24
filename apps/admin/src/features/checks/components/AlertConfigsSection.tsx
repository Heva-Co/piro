import { useState } from "react";
import { Plus, AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import {
  useAlertConfigs,
  useCreateAlertConfig,
  useUpdateAlertConfig,
  useDeleteAlertConfig,
} from "@/hooks/useChecks";
import { AlertConfigRow, type AlertConfigDraft } from "@/features/checks/components/AlertConfigRow";
import type { AlertConfig } from "@/lib/actions/alert-configs";
import { type CheckDimension, defaultAlertValue, DEFAULT_ALERT_SEVERITY } from "@/types/checks";

function defaultAlertConfigDraft(dimensions: readonly CheckDimension[]): AlertConfigDraft {
  const dim = dimensions[0];
  return {
    dimension: dim?.name ?? "",
    alertValue: dim ? defaultAlertValue(dim) : "",
    failureThreshold: 1,
    successThreshold: 1,
    severity: DEFAULT_ALERT_SEVERITY,
    isActive: true,
  };
}

function toDraft(config: AlertConfig): AlertConfigDraft {
  return {
    dimension: config.dimension,
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
  /** The dimensions the check type exposes (from its manifest) — what an alert rule can watch. */
  dimensions: readonly CheckDimension[];
}

export function AlertConfigsSection(props: Props) {
  const { serviceSlug, checkSlug, dimensions } = props;
  const { data: alertConfigs, isLoading } = useAlertConfigs(serviceSlug, checkSlug);
  const createAlertConfig = useCreateAlertConfig(serviceSlug, checkSlug);
  const updateAlertConfig = useUpdateAlertConfig(serviceSlug, checkSlug);
  const deleteAlertConfig = useDeleteAlertConfig(serviceSlug, checkSlug);

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
        <Empty className="border border-amber-500/20 bg-amber-500/3 rounded-lg py-8">
          <EmptyHeader>
            <EmptyMedia variant="icon" className="text-amber-600 dark:text-amber-400">
              <AlertTriangle />
            </EmptyMedia>
            <EmptyTitle>No alert configurations</EmptyTitle>
            <EmptyDescription>
              This check runs and records status, but no one is notified when it fails. Add an alert below.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}

      {savedConfigs.map((config) => (
        <AlertConfigRow
          key={config.id}
          initial={toDraft(config)}
          saved={config}
          dimensions={dimensions}
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
          initial={defaultAlertConfigDraft(dimensions)}
          saved={null}
          dimensions={dimensions}
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
