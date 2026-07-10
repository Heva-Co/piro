import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, Save } from "lucide-react";
import { escalationApi, onCallApi } from "@/lib/api";
import type { UpsertEscalationPolicyRequest } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

interface StepForm {
  order: number;
  delayMinutes: number;
  scheduleId: number | "";
}

export default function EscalationPolicyPage() {
  const qc = useQueryClient();

  const { data: policy, isLoading } = useQuery({
    queryKey: [QUERY_KEYS.ESCALATION_POLICY],
    queryFn: () => escalationApi.get(),
  });

  const { data: schedules = [] } = useQuery({
    queryKey: [QUERY_KEYS.ONCALL_SCHEDULES],
    queryFn: () => onCallApi.list(),
  });

  const [name, setName] = useState("Default Policy");
  const [description, setDescription] = useState("");
  const [reEscalateAfterAck, setReEscalateAfterAck] = useState(0);
  const [reEscalateAfterInactivity, setReEscalateAfterInactivity] = useState(0);
  const [steps, setSteps] = useState<StepForm[]>([{ order: 0, delayMinutes: 0, scheduleId: "" }]);

  const [policySaving, setPolicySaving] = useState(false);
  const [policySuccess, setPolicySuccess] = useState(false);
  const [stepsSaving, setStepsSaving] = useState(false);
  const [stepsSuccess, setStepsSuccess] = useState(false);

  useEffect(() => {
    if (policy) {
      setName(policy.name);
      setDescription(policy.description ?? "");
      setReEscalateAfterAck(policy.reEscalateAfterAckMinutes);
      setReEscalateAfterInactivity(policy.reEscalateAfterInactivityMinutes);
      setSteps(
        policy.steps.length > 0
          ? policy.steps.map((s) => ({ order: s.order, delayMinutes: s.delayMinutes, scheduleId: s.scheduleId }))
          : [{ order: 0, delayMinutes: 0, scheduleId: "" }]
      );
    }
  }, [policy]);

  const buildRequest = (overrides?: Partial<UpsertEscalationPolicyRequest>): UpsertEscalationPolicyRequest => ({
    name,
    description: description || undefined,
    reEscalateAfterAckMinutes: reEscalateAfterAck,
    reEscalateAfterInactivityMinutes: reEscalateAfterInactivity,
    steps: steps
      .filter((s) => s.scheduleId !== "")
      .map((s, i) => ({ order: i, delayMinutes: s.delayMinutes, scheduleId: s.scheduleId as number })),
    ...overrides,
  });

  const upsertMutation = useMutation({
    mutationFn: (data: UpsertEscalationPolicyRequest) => escalationApi.upsert(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: [QUERY_KEYS.ESCALATION_POLICY] }),
  });

  const deleteMutation = useMutation({
    mutationFn: () => escalationApi.delete(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [QUERY_KEYS.ESCALATION_POLICY] });
      setSteps([{ order: 0, delayMinutes: 0, scheduleId: "" }]);
    },
  });

  const handleSavePolicy = async () => {
    setPolicySaving(true);
    await upsertMutation.mutateAsync(buildRequest());
    setPolicySaving(false);
    setPolicySuccess(true);
    setTimeout(() => setPolicySuccess(false), 3000);
  };

  const handleSaveSteps = async () => {
    setStepsSaving(true);
    await upsertMutation.mutateAsync(buildRequest());
    setStepsSaving(false);
    setStepsSuccess(true);
    setTimeout(() => setStepsSuccess(false), 3000);
  };

  const addStep = () =>
    setSteps((prev) => [...prev, { order: prev.length, delayMinutes: 5, scheduleId: "" }]);

  const removeStep = (idx: number) =>
    setSteps((prev) => prev.filter((_, i) => i !== idx).map((s, i) => ({ ...s, order: i })));

  const updateStep = (idx: number, field: keyof StepForm, value: number | string) =>
    setSteps((prev) => prev.map((s, i) => (i === idx ? { ...s, [field]: value } : s)));

  if (isLoading) {
    return (
      <>
        <div className="text-sm text-muted-foreground">Loading…</div>
      </>
    );
  }

  return (
    <>
      <div className="max-w-4xl space-y-4">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Escalation Policy</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Auto-notify on-call users when incidents go unacknowledged.
          </p>
        </div>

        {/* ── General ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">General</h2>
            <p className="text-xs text-muted-foreground mt-0.5">Name and re-escalation timeouts</p>
          </div>

          <div className="grid grid-cols-2 gap-4 mb-4">
            <div className="col-span-2 flex flex-col gap-1.5">
              <label className="text-sm font-medium">Policy name</label>
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
            <div className="col-span-2 flex flex-col gap-1.5">
              <label className="text-sm font-medium">Description</label>
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional description"
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Re-escalate after ACK (minutes)</label>
              <input
                type="number"
                min={0}
                value={reEscalateAfterAck}
                onChange={(e) => setReEscalateAfterAck(Number(e.target.value))}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">0 = disabled</p>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Re-escalate after inactivity (minutes)</label>
              <input
                type="number"
                min={0}
                value={reEscalateAfterInactivity}
                onChange={(e) => setReEscalateAfterInactivity(Number(e.target.value))}
                className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              />
              <p className="text-xs text-muted-foreground">0 = disabled</p>
            </div>
          </div>

          <div className="flex justify-end">
            <button
              onClick={handleSavePolicy}
              disabled={policySaving}
              className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              <Save size={14} />
              {policySuccess ? "Saved!" : policySaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>

        {/* ── Escalation Steps ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold">Escalation steps</h2>
              <p className="text-xs text-muted-foreground mt-0.5">
                Steps fire in order — each step notifies the on-call users of the selected schedule.
              </p>
            </div>
            <button
              onClick={addStep}
              className="flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs font-medium hover:bg-muted transition-colors"
            >
              <Plus size={13} /> Add step
            </button>
          </div>

          <div className="space-y-3">
            {steps.map((step, idx) => (
              <div key={idx} className="flex items-start gap-3 rounded-lg border bg-background p-4">
                <div className="w-6 h-6 rounded-full bg-muted text-foreground flex items-center justify-center text-xs font-semibold shrink-0 mt-0.5">
                  {idx + 1}
                </div>
                <div className="flex-1 grid grid-cols-2 gap-3">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs text-muted-foreground font-medium">Wait (minutes) before notifying</label>
                    <input
                      type="number"
                      min={0}
                      value={step.delayMinutes}
                      onChange={(e) => updateStep(idx, "delayMinutes", Number(e.target.value))}
                      className="rounded-lg border bg-card px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs text-muted-foreground font-medium">Notify on-call from schedule</label>
                    <select
                      value={step.scheduleId}
                      onChange={(e) => updateStep(idx, "scheduleId", Number(e.target.value))}
                      className="rounded-lg border bg-card px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    >
                      <option value="">Select schedule…</option>
                      {schedules.map((s) => (
                        <option key={s.id} value={s.id}>{s.name}</option>
                      ))}
                    </select>
                  </div>
                </div>
                {steps.length > 1 && (
                  <button
                    onClick={() => removeStep(idx)}
                    className="mt-0.5 text-muted-foreground hover:text-destructive transition-colors"
                  >
                    <Trash2 size={15} />
                  </button>
                )}
              </div>
            ))}
          </div>

          <div className="flex justify-end mt-4">
            <button
              onClick={handleSaveSteps}
              disabled={stepsSaving}
              className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              <Save size={14} />
              {stepsSuccess ? "Saved!" : stepsSaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>

        {/* ── Danger zone ── */}
        {policy && (
          <div className="rounded-xl border border-destructive/30 bg-card p-6">
            <div className="mb-4">
              <h2 className="text-base font-semibold text-destructive">Danger zone</h2>
              <p className="text-xs text-muted-foreground mt-0.5">
                Deleting the policy will stop escalations for all future incidents.
              </p>
            </div>
            <button
              onClick={() => deleteMutation.mutate()}
              disabled={deleteMutation.isPending}
              className="rounded-lg border border-destructive text-destructive px-4 py-2 text-sm font-medium hover:bg-destructive/10 disabled:opacity-50 transition-colors"
            >
              {deleteMutation.isPending ? "Deleting…" : "Delete policy"}
            </button>
          </div>
        )}
      </div>
    </>
  );
}
