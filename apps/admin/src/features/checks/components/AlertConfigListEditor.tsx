import { forwardRef, useImperativeHandle, useRef, useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ALLOWED_ALERT_FORS } from "@/constants/checks";
import { AlertConfigRow, type AlertConfigDraft, type AlertConfigRowHandle } from "@/features/checks/components/AlertConfigRow";
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

interface Row {
  key: string;
  draft: AlertConfigDraft;
}

export interface AlertConfigListEditorHandle {
  /** Validates every row; returns whether all rows are currently valid. Invalid rows open and surface their errors. */
  validateAll: () => Promise<boolean>;
}

interface Props {
  checkType: string;
  value: AlertConfigDraft[];
  onChange: (value: AlertConfigDraft[]) => void;
}

/**
 * In-memory equivalent of AlertConfigsSection for a check that doesn't exist yet —
 * no API calls, just accumulates AlertConfigDraft[] for the parent form to submit
 * alongside check creation.
 */
export const AlertConfigListEditor = forwardRef<AlertConfigListEditorHandle, Props>(function AlertConfigListEditor(props, ref) {
  const { checkType, value, onChange } = props;
  const alertForOptions = ALLOWED_ALERT_FORS[checkType] ?? [DEFAULT_ALERT_FOR];

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
    commit([...rows, { key: crypto.randomUUID(), draft: defaultAlertConfigDraft(alertForOptions) }]);
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
          alertForOptions={alertForOptions}
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
