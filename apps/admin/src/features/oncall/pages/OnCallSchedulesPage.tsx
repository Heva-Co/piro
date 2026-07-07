import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, CalendarClock, X } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { onCallApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const TIMEZONES = [
  "UTC",
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "America/Bogota",
  "America/Argentina/Buenos_Aires",
  "America/Sao_Paulo",
  "America/Mexico_City",
  "Europe/London",
  "Europe/Madrid",
  "Europe/Paris",
  "Europe/Berlin",
  "Asia/Tokyo",
  "Asia/Shanghai",
  "Asia/Kolkata",
  "Australia/Sydney",
];

export default function OnCallSchedulesPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [timeZone, setTimeZone] = useState("UTC");
  const [formError, setFormError] = useState("");

  const { data: schedules = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.ONCALL_SCHEDULES,
    queryFn: onCallApi.list,
  });

  const createMutation = useMutation({
    mutationFn: () =>
      onCallApi.create({ name, description: description || undefined, timeZone, notifyOnShiftStart: false }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES });
      setOpen(false);
      resetForm();
      navigate(ROUTES.ONCALL.DETAIL(created.id));
    },
    onError: () => setFormError("Failed to create schedule."),
  });

  function resetForm() {
    setName("");
    setDescription("");
    setTimeZone("UTC");
    setFormError("");
  }

  function handleOpen() {
    resetForm();
    setOpen(true);
  }

  return (
    <AdminLayout title="On-Call Schedules">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Define who is on-call at any given moment using rotation layers and overrides.
          </p>
          <button
            onClick={handleOpen}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90"
          >
            <Plus size={15} /> Add schedule
          </button>
        </div>

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {isLoading && (
            <div className="p-8 text-center text-sm text-muted-foreground">Loading…</div>
          )}
          {!isLoading && schedules.length === 0 && (
            <div className="p-8 text-center text-sm text-muted-foreground">
              No on-call schedules yet.
            </div>
          )}
          {!isLoading && schedules.length > 0 && (
            <table className="min-w-full text-sm">
              <thead className="border-b border-border bg-muted/50">
                <tr>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Name</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Timezone</th>
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Layers</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {schedules.map((s) => (
                  <tr
                    key={s.id}
                    className="hover:bg-muted/50 cursor-pointer"
                    onClick={() => navigate(ROUTES.ONCALL.DETAIL(s.id))}
                  >
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <CalendarClock size={14} className="text-muted-foreground shrink-0" />
                        <span className="font-medium text-foreground">{s.name}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3.5 text-muted-foreground text-xs">{s.timeZone}</td>
                    <td className="px-5 py-3.5 text-muted-foreground text-xs">{s.layers.length}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Create schedule modal */}
      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-border">
              <h2 className="text-sm font-semibold text-foreground">New On-Call Schedule</h2>
              <button onClick={() => setOpen(false)} className="text-muted-foreground hover:text-foreground">
                <X size={16} />
              </button>
            </div>
            <div className="flex flex-col gap-4 px-6 py-5">
              {formError && (
                <p className="text-xs text-destructive">{formError}</p>
              )}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Name</label>
                <input
                  autoFocus
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Production on-call"
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Timezone</label>
                <select
                  value={timeZone}
                  onChange={(e) => setTimeZone(e.target.value)}
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
                >
                  {TIMEZONES.map((tz) => (
                    <option key={tz} value={tz}>{tz}</option>
                  ))}
                </select>
                <p className="text-xs text-muted-foreground">
                  Used to display shift times in the Gantt and for shift-start notifications. All data is stored in UTC — this only affects how times are shown.
                </p>
              </div>
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-foreground">Description <span className="text-muted-foreground font-normal">(optional)</span></label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Who this schedule covers and when"
                  rows={2}
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20 resize-none"
                />
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border">
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded-lg border border-border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => { setFormError(""); createMutation.mutate(); }}
                disabled={!name.trim() || createMutation.isPending}
                className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {createMutation.isPending ? "Creating…" : "Create schedule"}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
