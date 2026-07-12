import { useState, useEffect } from "react";
import { toast } from "react-toastify";
import axios from "axios";
import { Save } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { onCallApi, type OnCallSchedule } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { TimezonePicker } from "@/components/TimezonePicker";

interface Props {
  schedule: OnCallSchedule;
}

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function GeneralSettingsSection(props: Props) {
  const { schedule } = props;
  const qc = useQueryClient();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [timeZone, setTimeZone] = useState("UTC");
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setName(schedule.name);
    setDescription(schedule.description ?? "");
    setTimeZone(schedule.timeZone);
  }, [schedule]);

  const updateMutation = useMutation({
    mutationFn: () =>
      onCallApi.update(schedule.id, {
        name,
        description,
        timeZone,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULE(schedule.id) });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.ONCALL_SCHEDULES });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    },
    onError: (err) => toast.error(apiErrorMessage(err, "Failed to save schedule.")),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2 flex flex-col gap-1.5">
          <label className="text-sm font-medium">Schedule name</label>
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
          <label className="text-sm font-medium">Timezone</label>
          <TimezonePicker value={timeZone} onChange={setTimeZone} />
          <p className="text-xs text-muted-foreground">
            Used to display shift times in the Gantt. All data is stored in UTC.
          </p>
        </div>
      </div>

      <div className="flex justify-end">
        <Button onClick={() => updateMutation.mutate()} disabled={updateMutation.isPending}>
          <Save size={14} />
          {saved ? "Saved!" : updateMutation.isPending ? "Saving…" : "Save"}
        </Button>
      </div>
    </div>
  );
}

export default GeneralSettingsSection;
