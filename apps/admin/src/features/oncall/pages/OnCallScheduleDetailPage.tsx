import { useState, useMemo } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Plus, AlertTriangle } from "lucide-react";
import { onCallApi, type OnCallSlot, type OnCallLayer } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { GanttTimeline } from "../components/GanttTimeline";
import { AddLayerModal } from "../components/AddLayerModal";
import { AddOverrideModal } from "../components/AddOverrideModal";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";

type ViewMode = "1day" | "1week" | "2weeks" | "1month";

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function startOfDay(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
}

function getRange(anchor: Date, mode: ViewMode): { from: Date; to: Date } {
  const from = startOfDay(anchor);
  switch (mode) {
    case "1day": return { from, to: addDays(from, 1) };
    case "1week": return { from, to: addDays(from, 7) };
    case "2weeks": return { from, to: addDays(from, 14) };
    case "1month": return { from, to: addDays(from, 30) };
  }
}

function fmtRange(from: Date, to: Date, mode: ViewMode): string {
  const opts: Intl.DateTimeFormatOptions = { month: "short", day: "numeric" };
  if (mode === "1day") return from.toLocaleDateString(undefined, { ...opts, weekday: "long" });
  return `${from.toLocaleDateString(undefined, opts)} – ${addDays(to, -1).toLocaleDateString(undefined, opts)}`;
}

function isoStr(d: Date): string {
  return d.toISOString();
}

export default function OnCallScheduleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [viewMode, setViewMode] = useState<ViewMode>("2weeks");
  const [anchor, setAnchor] = useState<Date>(startOfDay(new Date()));
  const [showAddLayer, setShowAddLayer] = useState(false);
  const [showAddOverride, setShowAddOverride] = useState(false);
  const [editLayer, setEditLayer] = useState<OnCallLayer | null>(null);
  const confirm = useConfirmDialog();

  async function handleDeleteLayer(layerId: number) {
    const layer = schedule?.layers.find((l: OnCallLayer) => l.id === layerId);
    const ok = await confirm({
      title: `Delete "${layer?.name ?? "layer"}"?`,
      description: "This will remove the rotation and all its slots. This action cannot be undone.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (ok) deleteLayerMutation.mutate(layerId);
  }

  const { from, to } = useMemo(() => getRange(anchor, viewMode), [anchor, viewMode]);

  const { data: schedule, isLoading: loadingSchedule } = useQuery({
    queryKey: QUERY_KEYS.ONCALL_SCHEDULE(id!),
    queryFn: () => onCallApi.get(id!),
    enabled: !!id,
  });

  // Pure rotation slots — no overrides applied (for the Rotations section)
  const { data: rotationSlots = [] } = useQuery({
    queryKey: [...QUERY_KEYS.ONCALL_SCHEDULE_EXPAND(id!, isoStr(from), isoStr(to)), "pure"],
    queryFn: () => onCallApi.expand(id!, isoStr(from), isoStr(to), false),
    enabled: !!id,
  });

  // Slots with overrides applied — for Overrides section and Final Schedule
  const { data: slots = [], isLoading: loadingSlots } = useQuery({
    queryKey: QUERY_KEYS.ONCALL_SCHEDULE_EXPAND(id!, isoStr(from), isoStr(to)),
    queryFn: () => onCallApi.expand(id!, isoStr(from), isoStr(to), true),
    enabled: !!id,
  });

  const deleteLayerMutation = useMutation({
    mutationFn: (layerId: number) => onCallApi.deleteLayer(id!, layerId),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE(id!) }),
  });

  function advance(direction: 1 | -1) {
    const days = viewMode === "1day" ? 1 : viewMode === "1week" ? 7 : viewMode === "2weeks" ? 14 : 30;
    setAnchor(prev => addDays(prev, direction * days));
  }

  function goToday() {
    setAnchor(startOfDay(new Date()));
  }

  if (loadingSchedule) {
    return <><div className="p-8 text-center text-muted-foreground">Loading…</div></>;
  }

  if (!schedule) {
    return <><div className="p-8 text-center text-muted-foreground">Schedule not found.</div></>;
  }

  // Rotations: pure schedule without any override substitution
  const rotationRows = schedule.layers.map((layer: OnCallLayer) => ({
    label: layer.name,
    layer,
    slots: rotationSlots.filter((s: OnCallSlot) => s.layerId === layer.id),
  }));

  // One row per unique override (deduplicated by userId+replacesUserName),
  // spanning the full override range (min startsAt → max endsAt of all its slots)
  const overrideSlots = slots.filter((s: OnCallSlot) => s.isOverride);
  const overrideMap = new Map<string, { label: string; slots: OnCallSlot[] }>();
  for (const s of overrideSlots) {
    const key = `${s.userId}:${s.replacesUserName ?? ""}`;
    if (!overrideMap.has(key)) {
      overrideMap.set(key, {
        label: s.replacesUserName
          ? `${s.userInitials} → ${s.replacesUserName}`
          : `${s.userName} (extra)`,
        slots: [],
      });
    }
    overrideMap.get(key)!.slots.push(s);
  }
  // Collapse each group into one synthetic slot spanning the full override range + avatar info
  const overrideRows = Array.from(overrideMap.values()).map(({ label, slots: oSlots }) => {
    const first = oSlots[0];
    const startsAt = oSlots.reduce((min, s) => s.startsAt < min ? s.startsAt : min, first.startsAt);
    const endsAt   = oSlots.reduce((max, s) => s.endsAt > max ? s.endsAt : max, first.endsAt);
    // Find color for the replaced user from rotation slots
    const replacedSlot = rotationSlots.find((s: OnCallSlot) => s.userName === first.replacesUserName);
    const overrideInfo = first.replacesUserName ? {
      fromInitials: first.userInitials,
      fromColor: first.userColor || "#6366f1",
      toInitials: (first.replacesUserName.split(" ").map((p: string) => p[0]).join("")).toUpperCase(),
      toColor: replacedSlot?.userColor || "#94a3b8",
    } : undefined;
    return { label, slots: [{ ...first, startsAt, endsAt }], overrideInfo };
  });

  // Final: all slots merged (rotations with overrides applied)
  const finalRows = schedule.layers.map((layer: OnCallLayer) => ({
    label: layer.name,
    slots: slots.filter((s: OnCallSlot) => s.layerId === layer.id),
  }));

  const VIEW_MODES: { label: string; value: ViewMode }[] = [
    { label: "1 Day", value: "1day" },
    { label: "1 Week", value: "1week" },
    { label: "2 Weeks", value: "2weeks" },
    { label: "1 Month", value: "1month" },
  ];

  return (
    <>
      {/* Breadcrumb */}
      <button
        onClick={() => navigate(ROUTES.ONCALL.LIST)}
        className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground mb-4"
      >
        <ChevronLeft size={14} /> On-Call Schedules
      </button>

      {/* Schedule header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold text-foreground">{schedule.name}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">{schedule.timeZone}</p>
        </div>
        <div className="flex items-center gap-2 text-muted-foreground text-sm">
          {schedule.layers.length === 0 ? (
            <span className="inline-flex items-center gap-1.5 text-amber-600 dark:text-amber-500">
              <AlertTriangle size={13} />
              No coverage — add a rotation layer
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-green-500 inline-block" />
              Active
            </span>
          )}
        </div>
      </div>

      {/* Date nav + view toggle */}
      <div className="flex items-center gap-3 mb-6 flex-wrap">
        <button onClick={goToday} className="px-3 py-1.5 rounded-lg border border-border text-sm hover:bg-muted/50">Today</button>
        <div className="flex items-center gap-1">
          <button onClick={() => advance(-1)} className="p-1.5 rounded-lg border border-border hover:bg-muted/50">
            <ChevronLeft size={14} />
          </button>
          <button onClick={() => advance(1)} className="p-1.5 rounded-lg border border-border hover:bg-muted/50">
            <ChevronRight size={14} />
          </button>
        </div>
        <span className="font-semibold text-foreground text-sm">{fmtRange(from, to, viewMode)}</span>

        <div className="ml-auto flex items-center gap-1 rounded-lg border border-border p-0.5">
          {VIEW_MODES.map(({ label, value }) => (
            <button
              key={value}
              onClick={() => setViewMode(value)}
              className={`px-3 py-1 rounded-md text-sm transition-colors ${
                viewMode === value
                  ? "bg-foreground text-background"
                  : "text-muted-foreground hover:text-foreground"
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Rotations section */}
      <div className="rounded-xl border border-border bg-card mb-4">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted/30 rounded-t-xl">
          <h2 className="font-semibold text-sm text-foreground">Rotations</h2>
          <button
            onClick={() => setShowAddLayer(true)}
            className="flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 font-medium"
          >
            <Plus size={13} /> Add rotation
          </button>
        </div>
        <div className="py-3">
          {schedule.layers.length === 0 ? (
            <p className="text-sm text-muted-foreground italic px-4">No rotations yet.</p>
          ) : (
            <GanttTimeline
              rows={rotationRows}
              from={from}
              to={to}
              totalMs={to.getTime() - from.getTime()}
              onEditLayer={(layer) => setEditLayer(layer)}
              onDeleteLayer={handleDeleteLayer}
            />
          )}
        </div>
      </div>

      {/* Overrides section */}
      <div className="rounded-xl border border-border bg-card mb-4">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-muted/30 rounded-t-xl">
          <h2 className="font-semibold text-sm text-foreground">Overrides</h2>
          <div className="flex items-center gap-3">
            <button
              onClick={() => setShowAddOverride(true)}
              className="text-sm text-blue-600 hover:text-blue-700 font-medium"
            >
              Take on-call for an hour
            </button>
            <button
              onClick={() => setShowAddOverride(true)}
              className="flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 font-medium"
            >
              <Plus size={13} /> Add override
            </button>
          </div>
        </div>
        <div className="px-4 py-3">
          {overrideRows.length === 0 ? (
            <p className="text-sm text-muted-foreground italic">No overrides in this period.</p>
          ) : (
            <GanttTimeline rows={overrideRows} from={from} to={to} totalMs={to.getTime() - from.getTime()} />
          )}
        </div>
      </div>

      {/* Final schedule section */}
      <div className="rounded-xl border border-border bg-card">
        <div className="px-4 py-3 border-b border-border bg-muted/30 rounded-t-xl">
          <h2 className="font-semibold text-sm text-foreground">Final Schedule</h2>
        </div>
        <div className="px-4 py-3">
          {loadingSlots ? (
            <p className="text-sm text-muted-foreground">Loading…</p>
          ) : finalRows.every(r => r.slots.length === 0) ? (
            <p className="text-sm text-muted-foreground italic">No coverage in this period.</p>
          ) : (
            <GanttTimeline rows={finalRows} from={from} to={to} totalMs={to.getTime() - from.getTime()} />
          )}
        </div>
      </div>

      {/* Modals */}
      {showAddLayer && (
        <AddLayerModal
          scheduleId={id!}
          onClose={() => setShowAddLayer(false)}
          onSuccess={() => {
            setShowAddLayer(false);
            qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE(id!) });
            qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES });
          }}
        />
      )}
      {editLayer && (
        <AddLayerModal
          scheduleId={id!}
          initialLayer={editLayer}
          onClose={() => setEditLayer(null)}
          onSuccess={() => {
            setEditLayer(null);
            qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE(id!) });
            qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE_EXPAND(id!, isoStr(from), isoStr(to)) });
          }}
        />
      )}
      {showAddOverride && (
        <AddOverrideModal
          scheduleId={id!}
          onClose={() => setShowAddOverride(false)}
          onSuccess={() => {
            setShowAddOverride(false);
            qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE(id!) });
            qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE_EXPAND(id!, isoStr(from), isoStr(to)) });
          }}
        />
      )}
    </>
  );
}
