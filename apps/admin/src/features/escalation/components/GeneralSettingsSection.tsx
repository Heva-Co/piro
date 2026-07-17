import { useState, useEffect } from "react";
import { toast } from "react-toastify";
import axios from "axios";
import { Save } from "lucide-react";
import { escalationApi } from "@/lib/actions/escalation";
import type { EscalationPolicy, UpsertEscalationPolicyRequest } from "@/lib/actions/escalation";
import { QUERY_KEYS } from "@/constants/api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

interface Props {
  policy: EscalationPolicy;
  buildRequest: (overrides?: Partial<UpsertEscalationPolicyRequest>) => UpsertEscalationPolicyRequest;
}

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function GeneralSettingsSection(props: Props) {
  const { policy, buildRequest } = props;
  const qc = useQueryClient();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [reEscalateAfterInactivity, setReEscalateAfterInactivity] = useState(0);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setName(policy.name);
    setDescription(policy.description ?? "");
    setReEscalateAfterInactivity(policy.reEscalateAfterInactivityMinutes);
  }, [policy]);

  const updateMutation = useMutation({
    mutationFn: (data: UpsertEscalationPolicyRequest) => escalationApi.update(policy.id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ESCALATION_POLICY(policy.id) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ESCALATION_POLICIES });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    },
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to save escalation policy.")),
  });

  function handleSave() {
    updateMutation.mutate(
      buildRequest({
        name,
        description: description || undefined,
        reEscalateAfterInactivityMinutes: reEscalateAfterInactivity,
      })
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4">
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
        <div className="col-span-2 flex flex-col gap-1.5">
          <label className="text-sm font-medium">Re-escalate after inactivity (minutes)</label>
          <Input
            type="number"
            min={0}
            value={reEscalateAfterInactivity}
            onChange={(e) => setReEscalateAfterInactivity(Number(e.target.value))}
          />
          <p className="text-xs text-muted-foreground">
            Once an alert is acknowledged, escalation pauses. If it's still unresolved and nobody
            does anything else for this many minutes, escalation resumes from step 1. Also applies
            if the policy runs out of steps without ever being acknowledged. 0 = never resume automatically.
          </p>
        </div>
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

export default GeneralSettingsSection;
