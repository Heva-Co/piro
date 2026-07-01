import { Input } from "@/components/ui/input";
import type { CheckConfigProps } from "./types";

export function HeartbeatConfig({ config, onChange }: CheckConfigProps) {
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Grace period (seconds)</label>
        <Input type="number" value={(config.gracePeriodSeconds as number) ?? 60}
          onChange={(e) => onChange({ ...config, gracePeriodSeconds: Number(e.target.value) })} />
      </div>
      <p className="text-sm text-muted-foreground">
        A heartbeat check waits for a ping from your service. If no ping is received within the grace period, the check is marked as down.
      </p>
    </div>
  );
}
