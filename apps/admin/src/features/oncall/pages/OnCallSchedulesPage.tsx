import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Plus, CalendarClock, X, Trash2 } from "lucide-react";
import { onCallApi, type OnCallLayer } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";
import { TimezonePicker } from "@/components/TimezonePicker";

const MAX_VISIBLE = 4;

function MemberAvatars({ layers }: { layers: OnCallLayer[] }) {
  // Collect unique users across all layers
  const seen = new Set<number>();
  const users: { userId: number; userName: string; userInitials: string; userColor: string }[] = [];
  for (const layer of layers) {
    for (const u of layer.users) {
      if (!seen.has(u.userId)) {
        seen.add(u.userId);
        users.push(u);
      }
    }
  }

  if (users.length === 0) {
    return <span className="text-xs text-muted-foreground">—</span>;
  }

  const visible = users.slice(0, MAX_VISIBLE);
  const extra = users.length - MAX_VISIBLE;

  return (
    <div className="flex items-center -space-x-2">
      {visible.map((u) => (
        <div
          key={u.userId}
          title={u.userName}
          className="size-7 rounded-full border-2 border-card flex items-center justify-center text-white text-[10px] font-semibold shrink-0"
          style={{ backgroundColor: u.userColor || "#6366f1" }}
        >
          {u.userInitials}
        </div>
      ))}
      {extra > 0 && (
        <div className="size-7 rounded-full border-2 border-card bg-muted flex items-center justify-center text-[10px] font-semibold text-muted-foreground shrink-0">
          +{extra}
        </div>
      )}
    </div>
  );
}


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

  const confirm = useConfirmDialog();

  const deleteMutation = useMutation({
    mutationFn: (id: number) => onCallApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES }),
  });

  const handleDelete = async (e: React.MouseEvent, id: number, name: string) => {
    e.stopPropagation();
    const ok = await confirm({
      title: "Delete schedule",
      description: `Are you sure you want to delete "${name}"? This action cannot be undone.`,
      confirmLabel: "Delete",
      destructive: true,
    });
    if (ok) deleteMutation.mutate(id);
  };

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
    <>
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
                  <th className="px-5 py-3 text-left text-xs font-semibold text-muted-foreground">Members</th>
                  <th className="px-5 py-3" />
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
                    <td className="px-5 py-3.5">
                      <MemberAvatars layers={s.layers} />
                    </td>
                    <td className="px-5 py-3.5 text-right">
                      <button
                        onClick={(e) => handleDelete(e, s.id, s.name)}
                        className="text-muted-foreground hover:text-destructive transition-colors"
                      >
                        <Trash2 size={15} />
                      </button>
                    </td>
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
                <TimezonePicker value={timeZone} onChange={setTimeZone} />
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
    </>
  );
}
