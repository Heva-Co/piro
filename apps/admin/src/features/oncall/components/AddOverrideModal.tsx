import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { X } from "lucide-react";
import { onCallApi, usersApi } from "@/lib/api";

interface Props {
  scheduleId: string;
  onClose: () => void;
  onSuccess: () => void;
}

export function AddOverrideModal({ scheduleId, onClose, onSuccess }: Props) {
  const [userId, setUserId] = useState<number | "">("");
  const [replacesUserId, setReplacesUserId] = useState<number | "">("");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [reason, setReason] = useState("");

  const { data: users = [] } = useQuery({
    queryKey: ["users"],
    queryFn: () => usersApi.list(),
  });

  const mutation = useMutation({
    mutationFn: () =>
      onCallApi.createOverride(scheduleId, {
        userId: userId as number,
        replacesUserId: replacesUserId !== "" ? (replacesUserId as number) : undefined,
        startsAtUtc: new Date(startsAt).toISOString(),
        endsAtUtc: new Date(endsAt).toISOString(),
        reason: reason || undefined,
      }),
    onSuccess,
  });

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
            <select
              value={userId}
              onChange={(e) => setUserId(e.target.value === "" ? "" : Number(e.target.value))}
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="">Select user…</option>
              {users.map((u: { id: number; name: string }) => (
                <option key={u.id} value={u.id}>{u.name}</option>
              ))}
            </select>
          </div>

          {/* Replaces user (optional) */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Replacing (optional)</label>
            <select
              value={replacesUserId}
              onChange={(e) => setReplacesUserId(e.target.value === "" ? "" : Number(e.target.value))}
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="">No one — additional coverage</option>
              {users.map((u: { id: number; name: string }) => (
                <option key={u.id} value={u.id}>{u.name}</option>
              ))}
            </select>
          </div>

          {/* Date range */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1">Starts</label>
              <input
                type="datetime-local"
                value={startsAt}
                onChange={(e) => setStartsAt(e.target.value)}
                className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1">Ends</label>
              <input
                type="datetime-local"
                value={endsAt}
                onChange={(e) => setEndsAt(e.target.value)}
                className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              />
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
            disabled={mutation.isPending || !userId || !startsAt || !endsAt}
            className="px-4 py-2 rounded-lg bg-foreground text-background text-sm font-medium hover:opacity-90 disabled:opacity-50"
          >
            {mutation.isPending ? "Adding…" : "Add override"}
          </button>
        </div>
      </div>
    </div>
  );
}
