import { useState, useMemo, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Plus, AlertTriangle, Settings, Save } from "lucide-react";
import { onCallApi, type OnCallSlot } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import { SectionAccordion } from "@/components/ui/section-accordion";
import { Button } from "@/components/ui/button";
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty";
import DangerZone from "@/components/DangerZone";
import { GanttTimeline } from "../components/GanttTimeline";
import { AddLayerModal, type LayerFormPayload } from "../components/AddLayerModal";
import { AddOverrideModal, type OverrideFormPayload } from "../components/AddOverrideModal";
import GeneralSettingsSection from "../components/GeneralSettingsSection";
import ScheduleCoverageBadge from "../components/ScheduleCoverageBadge";
import ScheduleRangeToolbar from "../components/ScheduleRangeToolbar";
import StagedOverridesList from "../components/StagedOverridesList";
import { useRotationsDraft, type DraftLayer } from "../hooks/useRotationsDraft";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";
import { apiErrorMessage } from "@/utils/apiError";
import { addDays, startOfDay, getRange, isoStr, type ViewMode } from "../scheduleRange";
import { DEFAULT_MEMBER_COLOR, REPLACED_MEMBER_COLOR } from "../colors";

function OnCallScheduleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [viewMode, setViewMode] = useState<ViewMode>("2weeks");
  const [anchor, setAnchor] = useState<Date>(startOfDay(new Date()));
  const [showAddLayer, setShowAddLayer] = useState(false);
  const [showAddOverride, setShowAddOverride] = useState(false);
  const [editLayer, setEditLayer] = useState<DraftLayer | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const confirm = useConfirmDialog();

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

  const draft = useRotationsDraft(schedule?.layers ?? [], schedule?.overrides ?? []);

  // Re-seed the draft whenever the underlying schedule reloads (e.g. after a successful save).
  useEffect(() => {
    if (schedule) draft.reset(schedule.layers, schedule.overrides ?? []);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [schedule]);

  // Live preview of the draft's coverage over the visible range — drives the red gap bands on
  // Final Schedule. Recomputed whenever the draft or the visible range changes.
  const draftBatch = draft.toBatch();
  const { data: livePreview } = useQuery({
    queryKey: [...QUERY_KEYS.ONCALL_SCHEDULE_EXPAND(id!, isoStr(from), isoStr(to)), "gaps-preview", draftBatch],
    queryFn: () => onCallApi.previewRotations(id!, draftBatch, isoStr(from), isoStr(to)),
    enabled: !!id && !!schedule,
  });
  const liveGaps = livePreview?.gaps ?? [];

  async function handleDeleteSchedule() {
    await onCallApi.delete(id!);
    qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES });
    navigate(ROUTES.ONCALL.LIST);
  }

  async function handleDeleteDraftLayer(layerId: number) {
    const layer = draft.layers.find((l) => l.id === layerId);
    const ok = await confirm({
      title: `Delete "${layer?.name ?? "layer"}"?`,
      description: "This will remove the rotation and all its slots once you save.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (ok) draft.deleteLayer(layerId);
  }

  async function handleDeleteDraftOverride(overrideId: number) {
    const ok = await confirm({
      title: "Delete override?",
      description: "This override will be removed once you save.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (ok) draft.deleteOverride(overrideId);
  }

  async function handleSave() {
    if (!id) return;
    setIsSaving(true);
    try {
      const batch = draft.toBatch();
      const preview = await onCallApi.previewRotations(id, batch, isoStr(from), isoStr(addDays(from, 90)));

      if (preview.gaps.length > 0) {
        const ok = await confirm({
          title: "Coverage gaps detected",
          description: `${preview.gaps.length} window${preview.gaps.length === 1 ? "" : "s"} in the next 90 days will have no one on-call. You can review them as red bands in the Final Schedule below. Save anyway?`,
          confirmLabel: "Save anyway",
          destructive: true,
        });
        if (!ok) { setIsSaving(false); return; }
      }

      await onCallApi.saveRotations(id, batch);
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE(id) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES });
      toast.success("Rotations saved.");
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to save rotations."));
    } finally {
      setIsSaving(false);
    }
  }

  function advance(direction: 1 | -1) {
    const days = viewMode === "1day" ? 1 : viewMode === "1week" ? 7 : viewMode === "2weeks" ? 14 : 30;
    setAnchor(prev => addDays(prev, direction * days));
  }

  function goToday() {
    setAnchor(startOfDay(new Date()));
  }

  if (loadingSchedule) {
    return (
      <PageContainer>
        <div className="p-8 text-center text-muted-foreground">Loading…</div>
      </PageContainer>
    );
  }

  if (!schedule) {
    return (
      <PageContainer>
        <Empty className="py-14">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <AlertTriangle />
            </EmptyMedia>
            <EmptyTitle>Schedule not found</EmptyTitle>
            <EmptyDescription>This on-call schedule doesn't exist or was deleted.</EmptyDescription>
          </EmptyHeader>
          <Button variant="outline" onClick={() => navigate(ROUTES.ONCALL.LIST)}>Back to schedules</Button>
        </Empty>
      </PageContainer>
    );
  }

  // Rotations: pure schedule without any override substitution. Bars come from the last
  // *saved* expand (the Gantt bars themselves don't reflect the draft until Save is pressed),
  // but the row list/labels come from the draft so newly-added/edited/deleted layers show up
  // immediately with working edit/delete controls.
  const rotationRows = draft.layers.map((layer) => ({
    label: layer.name,
    layer: {
      id: layer.id,
      scheduleId: schedule.id,
      name: layer.name,
      order: 0,
      recurrenceRule: layer.recurrenceRule,
      firstOccurrenceStartsAt: layer.firstOccurrenceStartsAt,
      firstOccurrenceEndsAt: layer.firstOccurrenceEndsAt,
      isAllDay: false,
      users: layer.users,
    },
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
      fromColor: first.userColor || DEFAULT_MEMBER_COLOR,
      toInitials: (first.replacesUserName.split(" ").map((p: string) => p[0]).join("")).toUpperCase(),
      toColor: replacedSlot?.userColor || REPLACED_MEMBER_COLOR,
    } : undefined;
    return { label, slots: [{ ...first, startsAt, endsAt }], overrideInfo };
  });

  // Final: all slots merged (rotations with overrides applied)
  const finalRows = schedule.layers.map((layer) => ({
    label: layer.name,
    slots: slots.filter((s: OnCallSlot) => s.layerId === layer.id),
  }));

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "On Call Schedules", onClick: () => navigate(ROUTES.ONCALL.LIST) },
          { label: schedule.name },
        ]}
        subheader={schedule.timeZone}
        actions={<ScheduleCoverageBadge hasCoverage={schedule.layers.length > 0} />}
      />

      <SectionAccordion
        title="General Settings"
        description="Name, description, and timezone"
        icon={<Settings size={16} className="text-muted-foreground" />}
      >
        <GeneralSettingsSection schedule={schedule} />
      </SectionAccordion>

      <ScheduleRangeToolbar
        from={from}
        to={to}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        onToday={goToday}
        onAdvance={advance}
      />

      <SectionAccordion
        title="Rotations"
        description="Recurring on-call layers for this schedule"
        defaultOpen
        actions={
          <Button variant="ghost" size="sm" onClick={() => setShowAddLayer(true)}>
            <Plus size={13} /> Add rotation
          </Button>
        }
        disableCard
      >
        {draft.layers.length === 0 ? (
          <p className="text-sm text-muted-foreground italic">No rotations yet.</p>
        ) : (
          <GanttTimeline
            rows={rotationRows}
            from={from}
            to={to}
            totalMs={to.getTime() - from.getTime()}
            onEditLayer={(layer) => setEditLayer(draft.layers.find((l) => l.id === layer.id) ?? null)}
            onDeleteLayer={handleDeleteDraftLayer}
          />
        )}
      </SectionAccordion>

      <SectionAccordion
        title="Overrides"
        description="Temporary substitutions for this period"
        defaultOpen
        actions={
          <Button variant="ghost" size="sm" onClick={() => setShowAddOverride(true)}>
            <Plus size={13} /> Add override
          </Button>
        }
        disableCard
      >
        <div className="flex flex-col gap-4">
          {overrideRows.length === 0 ? (
            <p className="text-sm text-muted-foreground italic">No overrides in this period.</p>
          ) : (
            <GanttTimeline rows={overrideRows} from={from} to={to} totalMs={to.getTime() - from.getTime()} />
          )}

          <StagedOverridesList overrides={draft.overrides} onDelete={handleDeleteDraftOverride} />
        </div>
      </SectionAccordion>

      {draft.isDirty && (
        <div className="sticky bottom-4 z-10 flex justify-end">
          <Button onClick={handleSave} disabled={isSaving} className="shadow-lg">
            <Save size={14} />
            {isSaving ? "Saving…" : "Save rotations"}
          </Button>
        </div>
      )}

      <SectionAccordion
        title="Final Schedule"
        description="Rotations with overrides applied"
        defaultOpen
        disableCard
      >
        {loadingSlots ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : finalRows.every(r => r.slots.length === 0) ? (
          <p className="text-sm text-muted-foreground italic">No coverage in this period.</p>
        ) : (
          <GanttTimeline rows={finalRows} from={from} to={to} totalMs={to.getTime() - from.getTime()} gaps={liveGaps} />
        )}
      </SectionAccordion>

      <SectionAccordion
        title="Danger Zone"
        description="Irreversible actions for this schedule"
        icon={<AlertTriangle size={16} className="text-destructive" />}
        titleClassName="text-destructive"
        disableCard
      >
        <DangerZone objectName="on-call schedule" objectId={schedule.name} onDelete={handleDeleteSchedule} />
      </SectionAccordion>

      {/* Modals */}
      {showAddLayer && (
        <AddLayerModal
          onClose={() => setShowAddLayer(false)}
          onSave={(payload: LayerFormPayload) => {
            draft.addLayer(payload);
            setShowAddLayer(false);
          }}
        />
      )}
      {editLayer && (
        <AddLayerModal
          initialLayer={{
            id: editLayer.id,
            scheduleId: schedule.id,
            name: editLayer.name,
            order: 0,
            recurrenceRule: editLayer.recurrenceRule,
            firstOccurrenceStartsAt: editLayer.firstOccurrenceStartsAt,
            firstOccurrenceEndsAt: editLayer.firstOccurrenceEndsAt,
            isAllDay: false,
            users: editLayer.users,
          }}
          onClose={() => setEditLayer(null)}
          onSave={(payload: LayerFormPayload) => {
            draft.updateLayer(editLayer.id, payload);
            setEditLayer(null);
          }}
        />
      )}
      {showAddOverride && (
        <AddOverrideModal
          onClose={() => setShowAddOverride(false)}
          onSave={(payload: OverrideFormPayload) => {
            draft.addOverride(payload);
            setShowAddOverride(false);
          }}
        />
      )}
    </PageContainer>
  );
}

export default OnCallScheduleDetailPage;
