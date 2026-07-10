import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { X, Plus, Trash2, GripVertical } from "lucide-react";
import { onCallApi, usersApi } from "@/lib/api";
import { DateTimePicker } from "@/components/DateTimePicker";
import { DatePicker } from "@/components/DatePicker";
import { RRuleEditor } from "@/components/RRuleEditor";

import type { OnCallLayer } from "@/lib/api";

interface Props {
  scheduleId: string;
  initialLayer?: OnCallLayer;
  onClose: () => void;
  onSuccess: () => void;
}

export function AddLayerModal({ scheduleId, initialLayer, onClose, onSuccess }: Props) {
  const isEdit = !!initialLayer;
  const [name, setName] = useState(initialLayer?.name ?? "");
  const [firstStart, setFirstStart] = useState(initialLayer?.firstOccurrenceStartsAt ?? "");
  const [firstEnd, setFirstEnd] = useState(initialLayer?.firstOccurrenceEndsAt ?? "");
  const [rrule, setRrule] = useState(initialLayer?.recurrenceRule ?? "FREQ=DAILY");
  const [allDay, setAllDay] = useState(initialLayer?.isAllDay ?? false);
  const [userIds, setUserIds] = useState<number[]>(() =>
    initialLayer ? initialLayer.users.map((u) => u.userId) : []
  );
  const [userSearch, setUserSearch] = useState("");

  const { data: users = [] } = useQuery({
    queryKey: ["users"],
    queryFn: () => usersApi.list(),
  });

  function toAllDayStart(iso: string) {
    if (!iso) return iso;
    return iso.slice(0, 10) + "T00:00:00Z";
  }
  function toAllDayEnd(iso: string) {
    if (!iso) return iso;
    return iso.slice(0, 10) + "T23:59:59Z";
  }

  const payload = {
    name,
    recurrenceRule: rrule,
    firstOccurrenceStartsAt: allDay ? toAllDayStart(firstStart) : firstStart,
    firstOccurrenceEndsAt: allDay ? toAllDayEnd(firstEnd) : firstEnd,
    userIds,
  };

  const mutation = useMutation({
    mutationFn: () =>
      isEdit
        ? onCallApi.updateLayer(scheduleId, initialLayer.id, payload)
        : onCallApi.createLayer(scheduleId, { ...payload, order: 0 }),
    onSuccess,
  });

  const filteredUsers = users.filter(
    (u: { id: number; name: string }) =>
      !userIds.includes(u.id) &&
      u.name.toLowerCase().includes(userSearch.toLowerCase()),
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-lg">
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <h2 className="font-semibold text-foreground">{isEdit ? "Edit Rotation Layer" : "Add Rotation Layer"}</h2>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
            <X size={16} />
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* Name */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Layer name</label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Primary rotation"
              className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          {/* Recurrence */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Rotation frequency</label>
            <RRuleEditor
              value={rrule}
              onChange={setRrule}
              startDate={firstStart ? new Date(firstStart) : undefined}
            />
          </div>

          {/* First occurrence */}
          <div>
            <p className="text-xs text-muted-foreground mb-2">
              Shift times are interpreted in UTC — the schedule's timezone only affects how times are displayed.
            </p>
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-medium text-muted-foreground">First shift</span>
              <label className="flex items-center gap-1.5 cursor-pointer text-xs text-muted-foreground select-none">
                <input
                  type="checkbox"
                  checked={allDay}
                  onChange={(e) => setAllDay(e.target.checked)}
                  className="accent-foreground"
                />
                All day
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-muted-foreground mb-1">Starts</label>
                {allDay ? (
                  <DatePicker
                    value={firstStart ? firstStart.slice(0, 10) : ""}
                    onChange={(d) => setFirstStart(d ? d + "T00:00:00Z" : "")}
                    placeholder="Pick start date"
                    className="w-full"
                  />
                ) : (
                  <DateTimePicker value={firstStart} onChange={setFirstStart} placeholder="Pick start date" />
                )}
              </div>
              <div>
                <label className="block text-xs text-muted-foreground mb-1">Ends</label>
                {allDay ? (
                  <DatePicker
                    value={firstEnd ? firstEnd.slice(0, 10) : ""}
                    onChange={(d) => setFirstEnd(d ? d + "T23:59:59Z" : "")}
                    placeholder="Pick end date"
                    className="w-full"
                  />
                ) : (
                  <DateTimePicker value={firstEnd} onChange={setFirstEnd} placeholder="Pick end date" />
                )}
              </div>
            </div>
          </div>

          {/* User rotation list */}
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1">Rotation order</label>
            {userIds.length > 0 && (
              <ul className="mb-2 space-y-1">
                {userIds.map((uid, idx) => {
                  const user = users.find((u: { id: number; name: string }) => u.id === uid);
                  return (
                    <li key={uid} className="flex items-center gap-2 text-sm">
                      <GripVertical size={12} className="text-muted-foreground" />
                      <span className="flex-1 text-foreground">{idx + 1}. {user?.name ?? uid}</span>
                      <button
                        onClick={() => setUserIds((prev) => prev.filter((id) => id !== uid))}
                        className="text-muted-foreground hover:text-destructive"
                      >
                        <Trash2 size={12} />
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
            <div className="flex gap-2">
              <input
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
                placeholder="Search users…"
                className="flex-1 rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
            {userSearch && filteredUsers.length > 0 && (
              <ul className="mt-1 border border-border rounded-lg divide-y divide-border bg-card shadow">
                {filteredUsers.slice(0, 5).map((u: { id: number; name: string }) => (
                  <li key={u.id}>
                    <button
                      onClick={() => {
                        setUserIds((prev) => [...prev, u.id]);
                        setUserSearch("");
                      }}
                      className="w-full text-left px-3 py-2 text-sm text-foreground hover:bg-muted/50 flex items-center gap-2"
                    >
                      <Plus size={12} className="text-muted-foreground" />
                      {u.name}
                    </button>
                  </li>
                ))}
              </ul>
            )}
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
            disabled={mutation.isPending || !name || !rrule || !firstStart || !firstEnd || userIds.length === 0}
            className="px-4 py-2 rounded-lg bg-foreground text-background text-sm font-medium hover:opacity-90 disabled:opacity-50"
          >
            {mutation.isPending ? (isEdit ? "Saving…" : "Adding…") : (isEdit ? "Save changes" : "Add layer")}
          </button>
        </div>
      </div>
    </div>
  );
}
