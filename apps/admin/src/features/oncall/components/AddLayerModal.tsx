import { useState, useMemo } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { X, Plus, Trash2, GripVertical } from "lucide-react";
import { onCallApi, usersApi } from "@/lib/api";
import { DateTimePicker } from "@/components/DateTimePicker";
import { DatePicker } from "@/components/DatePicker";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

import type { OnCallLayer } from "@/lib/api";

interface Props {
  scheduleId: string;
  initialLayer?: OnCallLayer;
  onClose: () => void;
  onSuccess: () => void;
}

const DAY_NAMES = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"] as const;
const DAY_LABELS = ["S", "M", "T", "W", "T", "F", "S"] as const;

type FreqUnit = "day" | "week" | "month";
type EndsType = "never" | "on" | "after";

function buildRrule(
  interval: number,
  unit: FreqUnit,
  bydays: string[],
  ends: EndsType,
  endsDate: string,
  endsCount: number,
): string {
  const freq = unit === "day" ? "DAILY" : unit === "week" ? "WEEKLY" : "MONTHLY";
  const parts: string[] = [`FREQ=${freq}`];
  if (interval > 1) parts.push(`INTERVAL=${interval}`);
  if (unit === "week" && bydays.length > 0) parts.push(`BYDAY=${bydays.join(",")}`);
  if (ends === "on" && endsDate) {
    const d = new Date(endsDate);
    const pad = (n: number) => String(n).padStart(2, "0");
    const until = `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}T000000Z`;
    parts.push(`UNTIL=${until}`);
  } else if (ends === "after" && endsCount > 0) {
    parts.push(`COUNT=${endsCount}`);
  }
  return parts.join(";");
}

const CUSTOM_VALUE = "__custom__";

export function AddLayerModal({ scheduleId, initialLayer, onClose, onSuccess }: Props) {
  const isEdit = !!initialLayer;
  const [name, setName] = useState(initialLayer?.name ?? "");
  const [firstStart, setFirstStart] = useState(initialLayer?.firstOccurrenceStartsAt ?? "");
  const [firstEnd, setFirstEnd] = useState(initialLayer?.firstOccurrenceEndsAt ?? "");

  // Derive the weekday from firstStart for the "Weekly on {dayName}" preset
  const startDayIndex = useMemo(() => {
    if (!firstStart) return new Date().getDay();
    return new Date(firstStart).getDay();
  }, [firstStart]);
  const startDayName = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"][startDayIndex];
  const startDayCode = DAY_NAMES[startDayIndex];

  const PRESET_OPTIONS = useMemo(() => [
    { label: "Daily", value: "FREQ=DAILY" },
    { label: `Weekly on ${startDayName}`, value: `FREQ=WEEKLY;BYDAY=${startDayCode}` },
    { label: "Every weekday (Mon–Fri)", value: "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR" },
    { label: "Bi-weekly", value: "FREQ=WEEKLY;INTERVAL=2" },
    { label: "Custom…", value: CUSTOM_VALUE },
  ], [startDayName, startDayCode]);

  // Determine initial selected preset
  const [selectedPreset, setSelectedPreset] = useState<string>(() => {
    if (!initialLayer) return PRESET_OPTIONS[0].value;
    const match = PRESET_OPTIONS.find((p) => p.value !== CUSTOM_VALUE && p.value === initialLayer.recurrenceRule);
    return match ? match.value : CUSTOM_VALUE;
  });

  // Custom builder state
  const [customInterval, setCustomInterval] = useState(1);
  const [customUnit, setCustomUnit] = useState<FreqUnit>("week");
  const [customBydays, setCustomBydays] = useState<string[]>([]);
  const [customEnds, setCustomEnds] = useState<EndsType>("never");
  const [customEndsDate, setCustomEndsDate] = useState("");
  const [customEndsCount, setCustomEndsCount] = useState(1);

  const isCustom = selectedPreset === CUSTOM_VALUE;
  const customRrule = buildRrule(customInterval, customUnit, customBydays, customEnds, customEndsDate, customEndsCount);
  const rrule = isCustom ? customRrule : selectedPreset;
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
            <Select value={selectedPreset} onValueChange={setSelectedPreset}>
              <SelectTrigger className="w-full">
                <SelectValue>
                  {PRESET_OPTIONS.find((p) => p.value === selectedPreset)?.label}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {PRESET_OPTIONS.map((p) => (
                  <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            {isCustom && (
              <div className="mt-3 rounded-lg border border-border bg-background p-3 space-y-3">
                {/* Repeat every */}
                <div className="flex items-center gap-2">
                  <span className="text-sm text-foreground shrink-0">Repeat every</span>
                  <input
                    type="number"
                    min={1}
                    max={99}
                    value={customInterval}
                    onChange={(e) => setCustomInterval(Math.max(1, Math.min(99, Number(e.target.value))))}
                    className="w-16 rounded-lg border border-border bg-background px-2 py-1.5 text-sm text-foreground text-center focus:outline-none focus:ring-1 focus:ring-ring"
                  />
                  <Select value={customUnit} onValueChange={(v) => setCustomUnit(v as FreqUnit)}>
                    <SelectTrigger className="w-28">
                      <SelectValue>{customUnit}</SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="day">day</SelectItem>
                      <SelectItem value="week">week</SelectItem>
                      <SelectItem value="month">month</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                {/* Day-of-week toggles (only when unit = week) */}
                {customUnit === "week" && (
                  <div>
                    <span className="block text-xs text-muted-foreground mb-1.5">Repeat on</span>
                    <div className="flex gap-1">
                      {DAY_LABELS.map((label, i) => {
                        const code = DAY_NAMES[i];
                        const active = customBydays.includes(code);
                        return (
                          <button
                            key={code}
                            type="button"
                            onClick={() =>
                              setCustomBydays((prev) =>
                                active ? prev.filter((d) => d !== code) : [...prev, code]
                              )
                            }
                            className={`w-8 h-8 rounded-full text-xs font-medium transition-colors ${
                              active
                                ? "bg-foreground text-background"
                                : "border border-border text-muted-foreground hover:text-foreground"
                            }`}
                          >
                            {label}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* Ends */}
                <div>
                  <span className="block text-xs text-muted-foreground mb-1.5">Ends</span>
                  <div className="space-y-1.5">
                    {/* Never */}
                    <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                      <input
                        type="radio"
                        name="ends"
                        checked={customEnds === "never"}
                        onChange={() => setCustomEnds("never")}
                        className="accent-foreground"
                      />
                      Never
                    </label>
                    {/* On date */}
                    <div className="flex items-center gap-2">
                      <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                        <input
                          type="radio"
                          name="ends"
                          checked={customEnds === "on"}
                          onChange={() => setCustomEnds("on")}
                          className="accent-foreground"
                        />
                        On
                      </label>
                      <div className={customEnds !== "on" ? "opacity-40 pointer-events-none" : ""}>
                        <DatePicker
                          value={customEndsDate}
                          onChange={(d) => { setCustomEnds("on"); setCustomEndsDate(d); }}
                          placeholder="Pick end date"
                        />
                      </div>
                    </div>
                    {/* After N */}
                    <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                      <input
                        type="radio"
                        name="ends"
                        checked={customEnds === "after"}
                        onChange={() => setCustomEnds("after")}
                        className="accent-foreground"
                      />
                      After
                      <input
                        type="number"
                        min={1}
                        value={customEndsCount}
                        onChange={(e) => { setCustomEnds("after"); setCustomEndsCount(Math.max(1, Number(e.target.value))); }}
                        className="w-16 rounded-lg border border-border bg-background px-2 py-1 text-sm text-foreground text-center focus:outline-none focus:ring-1 focus:ring-ring disabled:opacity-40"
                        disabled={customEnds !== "after"}
                      />
                      <span className="text-muted-foreground">occurrences</span>
                    </label>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* First occurrence */}
          <div>
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
