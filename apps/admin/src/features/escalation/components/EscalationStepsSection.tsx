import { useState, useEffect } from "react";
import { toast } from "react-toastify";
import axios from "axios";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, Save, Zap, Bell, ArrowDown } from "lucide-react";
import { onCallApi } from "@/lib/api";
import { escalationApi } from "@/lib/actions/escalation";
import type { EscalationPolicy, UpsertEscalationPolicyRequest } from "@/lib/actions/escalation";
import { QUERY_KEYS } from "@/constants/api";
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
  // RFC 0006 — per-step retries. Phase 1 keeps today's fire-once behavior (maxRetries 1,
  // retryIntervalMinutes 0); a dedicated per-step editor for these arrives in Phase 2.
  maxRetries: number;
  retryIntervalMinutes: number;
  scheduleId: number | "";
}

interface Props {
  policy: EscalationPolicy;
  buildRequest: (overrides?: Partial<UpsertEscalationPolicyRequest>) => UpsertEscalationPolicyRequest;
}

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function EscalationStepsSection(props: Props) {
  const { policy, buildRequest } = props;
  const qc = useQueryClient();

  const { data: schedules = [] } = useQuery({
    queryKey: QUERY_KEYS.ONCALL_SCHEDULES_MEMBERS,
    queryFn: () => onCallApi.listMembers(),
  });

  const [steps, setSteps] = useState<StepForm[]>([
    { order: 0, delayMinutes: 0, maxRetries: 1, retryIntervalMinutes: 0, scheduleId: "" },
  ]);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setSteps(
      policy.steps.length > 0
        ? policy.steps.map((s) => ({
            order: s.order,
            delayMinutes: s.delayMinutes,
            maxRetries: s.maxRetries,
            retryIntervalMinutes: s.retryIntervalMinutes,
            scheduleId: s.scheduleId,
          }))
        : [{ order: 0, delayMinutes: 0, maxRetries: 1, retryIntervalMinutes: 0, scheduleId: "" }]
    );
  }, [policy]);

  const updateMutation = useMutation({
    mutationFn: (data: UpsertEscalationPolicyRequest) => escalationApi.update(policy.id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ESCALATION_POLICY(policy.id) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ESCALATION_POLICIES });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    },
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to save escalation steps.")),
  });

  function handleSave() {
    const incompleteCount = steps.filter((s) => s.scheduleId === "").length;
    if (incompleteCount > 0) {
      toast.warning(
        `${incompleteCount} step${incompleteCount === 1 ? "" : "s"} without a schedule selected will be discarded.`
      );
    }
    updateMutation.mutate(
      buildRequest({
        steps: steps
          .filter((s) => s.scheduleId !== "")
          .map((s, i) => ({
            order: i,
            delayMinutes: s.delayMinutes,
            maxRetries: s.maxRetries,
            retryIntervalMinutes: s.retryIntervalMinutes,
            scheduleId: s.scheduleId as number,
          })),
      })
    );
  }

  const addStep = () =>
    setSteps((prev) => [
      ...prev,
      { order: prev.length, delayMinutes: 5, maxRetries: 1, retryIntervalMinutes: 0, scheduleId: "" },
    ]);

  const removeStep = (idx: number) =>
    setSteps((prev) => prev.filter((_, i) => i !== idx).map((s, i) => ({ ...s, order: i })));

  const updateStep = (idx: number, field: keyof StepForm, value: number | string) =>
    setSteps((prev) => prev.map((s, i) => (i === idx ? { ...s, [field]: value } : s)));

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <p className="text-xs text-muted-foreground">
          Steps fire in order — each step notifies the on-call users of the selected schedule.
        </p>
        <Button variant="outline" size="sm" onClick={addStep}>
          <Plus size={13} /> Add step
        </Button>
      </div>

      {/* Trigger marker */}
      <div className="flex items-center gap-3 rounded-lg border border-dashed bg-muted/30 px-4 py-2.5">
        <div className="w-6 h-6 rounded-full bg-foreground text-background flex items-center justify-center shrink-0">
          <Zap size={13} />
        </div>
        <p className="text-sm text-muted-foreground">Immediately after an alert fires</p>
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
                {idx === 0 ? "starts after" : "escalates after"}{" "}
                <span className="font-medium text-foreground">{step.delayMinutes} minutes</span>
                {step.maxRetries > 1 && (
                  <span>
                    · then notifies{" "}
                    <span className="font-medium text-foreground">{step.maxRetries} times</span>
                    {step.retryIntervalMinutes > 0 && (
                      <> every <span className="font-medium text-foreground">{step.retryIntervalMinutes} min</span></>
                    )}
                  </span>
                )}
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

                  <div className="grid grid-cols-2 gap-3">
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs text-muted-foreground font-medium">Notify how many times</label>
                      <Input
                        type="number"
                        min={1}
                        value={step.maxRetries}
                        onChange={(e) => updateStep(idx, "maxRetries", Math.max(1, Number(e.target.value)))}
                      />
                      <p className="text-[11px] text-muted-foreground">
                        How many times this step pages its on-call before handing off. 1 = notify once.
                      </p>
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs text-muted-foreground font-medium">Minutes between attempts</label>
                      <Input
                        type="number"
                        min={0}
                        value={step.retryIntervalMinutes}
                        disabled={step.maxRetries <= 1}
                        onChange={(e) => updateStep(idx, "retryIntervalMinutes", Math.max(0, Number(e.target.value)))}
                      />
                      <p className="text-[11px] text-muted-foreground">
                        {step.maxRetries <= 1
                          ? "Only applies when notifying more than once."
                          : "Spacing between this step's attempts. 0 = as fast as the 1-minute job allows."}
                      </p>
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

      <div className="flex justify-end">
        <Button onClick={handleSave} disabled={updateMutation.isPending}>
          <Save size={14} />
          {saved ? "Saved!" : updateMutation.isPending ? "Saving…" : "Save"}
        </Button>
      </div>
    </div>
  );
}

export default EscalationStepsSection;
