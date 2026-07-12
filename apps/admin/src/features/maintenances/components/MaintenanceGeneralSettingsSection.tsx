import { useState, useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, CheckCircle, Save } from "lucide-react";
import { DateTimePicker } from "@/components/DateTimePicker";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { maintenancesApi, type Maintenance } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { RRuleEditor, isOneTimeRRule } from "@/components/RRuleEditor";

function pad(n: number) {
  return String(n).padStart(2, "0");
}

function toLocalDT(d: Date) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

interface Props {
  maintenance: Maintenance;
}

export default function MaintenanceGeneralSettingsSection({ maintenance }: Props) {
  const qc = useQueryClient();
  const maintenanceKey = QUERY_KEYS.MAINTENANCE(maintenance.id);

  const oneTime = isOneTimeRRule(maintenance.rRule);

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [startDateTime, setStartDateTime] = useState("");
  const [durationSeconds, setDurationSeconds] = useState(3600);
  const [rRule, setRRule] = useState(maintenance.rRule);
  const [init, setInit] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (init) return;
    setTitle(maintenance.title);
    setDescription(maintenance.description ?? "");
    setStartDateTime(toLocalDT(new Date(maintenance.startDateTime * 1000)));
    setDurationSeconds(maintenance.durationSeconds);
    setRRule(maintenance.rRule);
    setInit(true);
  }, [maintenance, init]);

  const isCancelled = maintenance.displayStatus === "Cancelled";

  const updateMutation = useMutation({
    mutationFn: () =>
      maintenancesApi.update(maintenance.id, {
        title,
        description,
        startDateTime: Math.floor(new Date(startDateTime).getTime() / 1000),
        durationSeconds,
        ...(oneTime ? {} : { rRule }),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: maintenanceKey });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
      setSaved(true);
      setError("");
      setTimeout(() => setSaved(false), 3000);
    },
    onError: () => setError("Failed to save changes."),
  });

  return (
    <form
      onSubmit={(e) => { e.preventDefault(); updateMutation.mutate(); }}
      className="rounded-xl border bg-card p-6 flex flex-col gap-5"
    >
      {error && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          <AlertCircle size={15} /> {error}
        </div>
      )}

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Title</label>
        <Input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          required
          disabled={isCancelled}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Description</label>
        <Textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={2}
          disabled={isCancelled}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Start Date/Time</label>
        <DateTimePicker value={startDateTime} onChange={setStartDateTime} />
      </div>

      {oneTime ? (
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Duration</label>
          <div className="flex items-center gap-2">
            <Input type="number" min={0} value={Math.floor(durationSeconds / 3600)}
              onChange={(e) => setDurationSeconds(Number(e.target.value) * 3600 + (durationSeconds % 3600))}
              disabled={isCancelled}
              className="w-16" />
            <span className="text-sm text-muted-foreground">h</span>
            <Input type="number" min={0} max={59} value={Math.floor((durationSeconds % 3600) / 60)}
              onChange={(e) => setDurationSeconds(Math.floor(durationSeconds / 3600) * 3600 + Number(e.target.value) * 60)}
              disabled={isCancelled}
              className="w-16" />
            <span className="text-sm text-muted-foreground">m</span>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Schedule Pattern</label>
          <RRuleEditor
            value={rRule}
            onChange={setRRule}
            startDate={new Date(startDateTime)}
            showDuration
            durationSeconds={durationSeconds}
            onDurationChange={setDurationSeconds}
          />
        </div>
      )}

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Affected Services</label>
        {maintenance.isGlobal ? (
          <span className="self-start rounded-full bg-indigo-500/15 px-2.5 py-0.5 text-xs font-medium text-indigo-600">
            All services
          </span>
        ) : maintenance.serviceSlugs.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {maintenance.serviceSlugs.map((slug) => (
              <span key={slug} className="rounded-md border bg-muted px-2 py-1 text-xs">{slug}</span>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No services selected.</p>
        )}
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={updateMutation.isPending || isCancelled}>
          <Save size={14} />
          {saved ? "Saved!" : updateMutation.isPending ? "Saving…" : "Save changes"}
          {saved && <CheckCircle size={14} />}
        </Button>
      </div>
    </form>
  );
}
