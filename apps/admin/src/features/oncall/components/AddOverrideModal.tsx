import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { X } from "lucide-react";
import { onCallApi, usersApi } from "@/lib/api";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { DateTimePicker } from "@/components/DateTimePicker";
import { DatePicker } from "@/components/DatePicker";

interface Props {
  scheduleId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export function AddOverrideModal({ scheduleId, onClose, onSuccess }: Props) {
  const [userId, setUserId] = useState<string>("");
  const [replacesUserId, setReplacesUserId] = useState<string>("");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [reason, setReason] = useState("");
  const [allDay, setAllDay] = useState(false);

  const { data: users = [] } = useQuery({
    queryKey: ["users"],
    queryFn: () => usersApi.list(),
  });

  const mutation = useMutation({
    mutationFn: () =>
      onCallApi.createOverride(scheduleId, {
        userId: Number(userId),
        replacesUserId: replacesUserId !== "" ? Number(replacesUserId) : undefined,
        startsAtUtc: startsAtUtc(),
        endsAtUtc: endsAtUtc(),
        reason: reason || undefined,
      }),
    onSuccess,
  });

  function handleAllDayToggle(checked: boolean) {
    setAllDay(checked);
    setStartsAt("");
    setEndsAt("");
  }

  function startsAtUtc(): string {
    if (!startsAt) return "";
    return allDay ? `${startsAt}T00:00:00Z` : startsAt;
  }
  function endsAtUtc(): string {
    if (!endsAt) return "";
    return allDay ? `${endsAt}T23:59:59Z` : endsAt;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <h2 className="font-semibold text-foreground">Add Override</h2>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
            <X size={16} />
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* On-call user */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Who takes on-call</label>
            <Select value={userId} onValueChange={(v) => v && setUserId(v)}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select user…">
                  {users.find((u: { id: number; name: string }) => String(u.id) === userId)?.name ?? "Select user…"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {users.map((u: { id: number; name: string }) => (
                  <SelectItem key={u.id} value={String(u.id)}>{u.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Replaces user (optional) */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Replacing (optional)</label>
            <Select value={replacesUserId} onValueChange={(v) => setReplacesUserId(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="No one — additional coverage">
                  {replacesUserId === ""
                    ? "No one — additional coverage"
                    : users.find((u: { id: number; name: string }) => String(u.id) === replacesUserId)?.name ?? "No one — additional coverage"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">No one — additional coverage</SelectItem>
                {users.map((u: { id: number; name: string }) => (
                  <SelectItem key={u.id} value={String(u.id)}>{u.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Date range */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-medium text-muted-foreground">Date range</span>
              <label className="flex items-center gap-1.5 text-xs text-muted-foreground cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={allDay}
                  onChange={(e) => handleAllDayToggle(e.target.checked)}
                  className="accent-indigo-600 w-3.5 h-3.5"
                />
                All day
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-muted-foreground mb-1">Starts</label>
                {allDay
                  ? <DatePicker value={startsAt} onChange={setStartsAt} className="w-full" />
                  : <DateTimePicker value={startsAt} onChange={setStartsAt} placeholder="Pick date & time" className="w-full" />}
              </div>
              <div>
                <label className="block text-xs text-muted-foreground mb-1">Ends</label>
                {allDay
                  ? <DatePicker value={endsAt} onChange={setEndsAt} className="w-full" />
                  : <DateTimePicker value={endsAt} onChange={setEndsAt} placeholder="Pick date & time" className="w-full" />}
              </div>
            </div>
          </div>

          {/* Reason */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Reason (optional)</label>
            <input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. Vacation"
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>
        </div>

        <div className="flex justify-end gap-2 px-5 py-4 border-t border-border">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg border border-border text-sm text-muted-foreground hover:text-foreground"
          >
            Cancel
          </button>
          <button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending || !userId || !startsAtUtc() || !endsAtUtc()}
            className="px-4 py-2 rounded-lg bg-foreground text-background text-sm font-medium hover:opacity-90 disabled:opacity-50"
          >
            {mutation.isPending ? "Adding…" : "Add override"}
          </button>
        </div>
      </div>
    </div>
  );
}
