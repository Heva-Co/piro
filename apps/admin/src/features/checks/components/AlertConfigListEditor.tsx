import { forwardRef, useImperativeHandle, useRef, useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { AlertConfigRow, type AlertConfigDraft, type AlertConfigRowHandle } from "@/features/checks/components/AlertConfigRow";
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

interface Row {
  key: string;
  draft: AlertConfigDraft;
}

export interface AlertConfigListEditorHandle {
  /** Validates every row; returns whether all rows are currently valid. Invalid rows open and surface their errors. */
  validateAll: () => Promise<boolean>;
}

interface Props {
  /** The dimensions the check type exposes (from its manifest) — what an alert rule can watch. */
  dimensions: readonly CheckDimension[];
  value: AlertConfigDraft[];
  onChange: (value: AlertConfigDraft[]) => void;
}

/**
 * In-memory equivalent of AlertConfigsSection for a check that doesn't exist yet —
 * no API calls, just accumulates AlertConfigDraft[] for the parent form to submit
 * alongside check creation.
 */
export const AlertConfigListEditor = forwardRef<AlertConfigListEditorHandle, Props>(function AlertConfigListEditor(props, ref) {
  const { dimensions, value, onChange } = props;

  const [rows, setRows] = useState<Row[]>(() => value.map((draft) => ({ key: crypto.randomUUID(), draft })));
  const rowRefs = useRef(new Map<string, AlertConfigRowHandle>());

  useImperativeHandle(ref, () => ({
    validateAll: async () => {
      const results = await Promise.all(rows.map((r) => rowRefs.current.get(r.key)?.validate() ?? Promise.resolve(true)));
      return results.every(Boolean);
    },
  }));

  function commit(next: Row[]) {
    setRows(next);
    onChange(next.map((r) => r.draft));
  }

  function addRow() {
    commit([...rows, { key: crypto.randomUUID(), draft: defaultAlertConfigDraft(dimensions) }]);
  }

  function saveRow(key: string, draft: AlertConfigDraft) {
    commit(rows.map((r) => (r.key === key ? { ...r, draft } : r)));
  }

  function removeRow(key: string) {
    rowRefs.current.delete(key);
    commit(rows.filter((r) => r.key !== key));
  }

  return (
    <div className="flex flex-col gap-3">
      {rows.length === 0 && (
        <p className="text-sm text-muted-foreground">No alert configurations yet. Add one below.</p>
      )}

      {rows.map((row) => (
        <AlertConfigRow
          key={row.key}
          ref={(handle) => {
            if (handle) rowRefs.current.set(row.key, handle);
            else rowRefs.current.delete(row.key);
          }}
          initial={row.draft}
          saved={null}
          dimensions={dimensions}
          onSave={async (draft) => saveRow(row.key, draft)}
          onRemove={() => removeRow(row.key)}
          isSaving={false}
          autoSave
        />
      ))}

      <Button type="button" variant="ghost" size="sm" onClick={addRow} className="self-start">
        <Plus size={14} />
        Add alert configuration
      </Button>
    </div>
  );
});
