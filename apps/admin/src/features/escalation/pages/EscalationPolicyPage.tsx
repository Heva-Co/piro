import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, Save, Zap, Bell, ArrowDown } from "lucide-react";
import { escalationApi, onCallApi } from "@/lib/api";
import type { UpsertEscalationPolicyRequest } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";

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
    queryKey: QUERY_KEYS.ONCALL_SCHEDULES_MEMBERS,
    queryFn: () => onCallApi.listMembers(),
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
        <PageHeader
          breadcrumbs={[{ label: "Escalation Policy" }]}
          subheader="Auto-notify on-call users when incidents go unacknowledged."
        />

        {/* ── General ── */}
        <div className="rounded-xl border bg-card p-6">
          <div className="mb-4">
            <h2 className="text-base font-semibold">General</h2>
            <p className="text-xs text-muted-foreground mt-0.5">Name and re-escalation timeouts</p>
          </div>

          <div className="grid grid-cols-2 gap-4 mb-4">
            <div className="col-span-2 flex flex-col gap-1.5">
              <label className="text-sm font-medium">Policy name</label>
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="col-span-2 flex flex-col gap-1.5">
              <label className="text-sm font-medium">Description</label>
              <Input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional description"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Re-escalate after ACK (minutes)</label>
              <Input
                type="number"
                min={0}
                value={reEscalateAfterAck}
                onChange={(e) => setReEscalateAfterAck(Number(e.target.value))}
              />
              <p className="text-xs text-muted-foreground">0 = disabled</p>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Re-escalate after inactivity (minutes)</label>
              <Input
                type="number"
                min={0}
                value={reEscalateAfterInactivity}
                onChange={(e) => setReEscalateAfterInactivity(Number(e.target.value))}
              />
              <p className="text-xs text-muted-foreground">0 = disabled</p>
            </div>
          </div>

          <div className="flex justify-end">
            <Button onClick={handleSavePolicy} disabled={policySaving}>
              <Save size={14} />
              {policySuccess ? "Saved!" : policySaving ? "Saving…" : "Save"}
            </Button>
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
            <Button variant="outline" size="sm" onClick={addStep}>
              <Plus size={13} /> Add step
            </Button>
          </div>

          {/* Trigger marker */}
          <div className="flex items-center gap-3 rounded-lg border border-dashed bg-muted/30 px-4 py-2.5">
            <div className="w-6 h-6 rounded-full bg-foreground text-background flex items-center justify-center shrink-0">
              <Zap size={13} />
            </div>
            <p className="text-sm text-muted-foreground">Immediately after an incident is triggered</p>
          </div>

          <div>
            {steps.map((step, idx) => {
              const members = step.scheduleId === "" ? [] : schedules.find((s) => s.id === step.scheduleId)?.members ?? [];
              const scheduleName = step.scheduleId === ""
                ? ""
                : schedules.find((s) => s.id === step.scheduleId)?.name ?? "";

              return (
                <div key={idx}>
                  <div className="flex items-center gap-2 pl-3 py-2 text-xs text-muted-foreground">
                    <ArrowDown size={13} />
                    escalates after <span className="font-medium text-foreground">{step.delayMinutes} minutes</span>
                  </div>

                  <div className="flex items-start gap-3 rounded-lg border bg-background p-4">
                    <div className="w-6 h-6 rounded-full bg-muted text-foreground flex items-center justify-center text-xs font-semibold shrink-0 mt-0.5">
                      {idx + 1}
                    </div>
                    <div className="flex-1 space-y-3">
                      <div className="grid grid-cols-2 gap-3">
                        <div className="flex flex-col gap-1.5">
                          <label className="text-xs text-muted-foreground font-medium">Wait (minutes) before notifying</label>
                          <Input
                            type="number"
                            min={0}
                            value={step.delayMinutes}
                            onChange={(e) => updateStep(idx, "delayMinutes", Number(e.target.value))}
                          />
                        </div>
                        <div className="flex flex-col gap-1.5">
                          <label className="text-xs text-muted-foreground font-medium">Notify on-call from schedule</label>
                          <Select
                            value={step.scheduleId === "" ? "" : String(step.scheduleId)}
                            onValueChange={(v) => updateStep(idx, "scheduleId", v ? Number(v) : "")}
                          >
                            <SelectTrigger className="w-full">
                              <SelectValue placeholder="Select schedule…">
                                {step.scheduleId === "" ? "Select schedule…" : scheduleName || "Select schedule…"}
                              </SelectValue>
                            </SelectTrigger>
                            <SelectContent>
                              {schedules.map((s) => (
                                <SelectItem key={s.id} value={String(s.id)}>{s.name}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>

                      {step.scheduleId !== "" && (
                        <div className="flex items-center gap-2 rounded-lg bg-muted/40 px-3 py-2">
                          <Bell size={13} className="text-muted-foreground shrink-0" />
                          <span className="text-xs text-muted-foreground">Notify:</span>
                          {members.length > 0 ? (
                            <div className="flex items-center gap-1.5 flex-wrap">
                              {members.map((m) => (
                                <Avatar key={m.userId} size="sm" title={m.userName}>
                                  <AvatarFallback
                                    className="text-white"
                                    style={{ backgroundColor: m.userColor || "#6366f1" }}
                                  >
                                    {m.userInitials}
                                  </AvatarFallback>
                                </Avatar>
                              ))}
                            </div>
                          ) : (
                            <span className="text-xs text-muted-foreground italic">no members in this schedule</span>
                          )}
                        </div>
                      )}
                    </div>
                    {steps.length > 1 && (
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        onClick={() => removeStep(idx)}
                        className="mt-0.5 text-muted-foreground hover:text-destructive"
                      >
                        <Trash2 size={15} />
                      </Button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          <div className="flex justify-end mt-4">
            <Button onClick={handleSaveSteps} disabled={stepsSaving}>
              <Save size={14} />
              {stepsSuccess ? "Saved!" : stepsSaving ? "Saving…" : "Save"}
            </Button>
          </div>
        </div>

      </div>
    </>
  );
}
