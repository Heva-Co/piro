import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import axios from "axios";
import { Trash2, AlertTriangle } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { DatePicker } from "@/components/DatePicker";
import { alertsApi } from "@/lib/actions/alerts";
import { QUERY_KEYS } from "@/constants/api";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";

function apiErrorMessage(err: unknown, fallback: string) {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { detail?: string; title?: string } | undefined;
    return data?.detail ?? data?.title ?? fallback;
  }
  return fallback;
}

export default function DataRetentionPage() {
  const queryClient = useQueryClient();
  const confirm = useConfirmDialog();

  // <input type="date"> value: YYYY-MM-DD. Empty until the admin picks a cutoff.
  const [cutoff, setCutoff] = useState("");
  const [matchCount, setMatchCount] = useState<number | null>(null);

  // The cutoff is a calendar date; the delete predicate is "resolved strictly before" it, so we
  // send the start of the chosen day (local midnight) as an ISO instant.
  function cutoffInstant(): string {
    return new Date(`${cutoff}T00:00:00`).toISOString();
  }

  const preview = useMutation({
    mutationFn: () => alertsApi.previewRetention(cutoffInstant()),
    onSuccess: (result) => setMatchCount(result.count),
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to preview matching alerts.")),
  });

  const del = useMutation({
    mutationFn: () => alertsApi.deleteByRetention(cutoffInstant()),
    onSuccess: (result) => {
      toast.success(`Deleted ${result.count} resolved alert${result.count === 1 ? "" : "s"}.`);
      setMatchCount(null);
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.ALERTS });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.DASHBOARD_METRICS_ALL });
    },
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to delete alerts.")),
  });

  async function handleDelete() {
    const ok = await confirm({
      title: "Delete resolved alerts?",
      description:
        `This permanently deletes every resolved alert resolved before ${cutoff} that is not linked ` +
        `to an incident. Active alerts and incident-linked alerts are preserved. This cannot be undone.`,
      confirmLabel: "Delete alerts",
      destructive: true,
    });
    if (ok) del.mutate();
  }

  const busy = preview.isPending || del.isPending;

  return (
    <div className="max-w-2xl">
      <PageHeader breadcrumbs={[{ label: "Data Retention" }]} />
      <p className="text-muted-foreground text-sm -mt-4 mb-6">
        Manually clean up historical alert data. Deletions are permanent and cannot be undone.
      </p>

      {/* ── Delete resolved alerts ── */}
      <div className="rounded-xl border bg-card p-6">
        <div className="mb-4">
          <h2 className="text-base font-semibold">Delete resolved alerts up to a date</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            Removes resolved alerts whose resolution date is before the cutoff. Active alerts and
            alerts linked to an incident are always preserved.
          </p>
        </div>

        <div className="flex flex-col gap-2 max-w-xs mb-5">
          <Label>Delete alerts resolved before</Label>
          <DatePicker
            value={cutoff}
            onChange={(v) => {
              setCutoff(v);
              setMatchCount(null);
            }}
            className="block w-full"
          />
        </div>

        <div className="flex items-center gap-3 mb-5">
          <Button variant="outline" disabled={!cutoff || busy} onClick={() => preview.mutate()}>
            Preview matching alerts
          </Button>
          {matchCount !== null && (
            <span className="text-sm text-muted-foreground">
              {matchCount === 0
                ? "No resolved alerts match this cutoff."
                : `${matchCount} resolved alert${matchCount === 1 ? "" : "s"} will be deleted.`}
            </span>
          )}
        </div>

        <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 flex items-start gap-3">
          <AlertTriangle size={18} className="text-destructive mt-0.5 shrink-0" />
          <div className="flex flex-col gap-3">
            <p className="text-sm text-muted-foreground">
              Deleting alerts is permanent. Their escalation delivery history is removed too. This
              does not affect any incidents the alerts were linked to.
            </p>
            <Button
              variant="destructive"
              className="w-fit"
              disabled={!cutoff || busy}
              onClick={handleDelete}
            >
              <Trash2 size={15} />
              Delete resolved alerts
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
