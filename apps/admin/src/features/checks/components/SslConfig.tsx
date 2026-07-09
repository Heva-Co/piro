import { Input } from "@/components/ui/input";
import type { CheckConfigProps } from "./types";

export function SslConfig({ config, onChange }: CheckConfigProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Host <span className="text-destructive">*</span></label>
        <Input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
          placeholder="example.com" />
      </div>
      <div className="grid grid-cols-3 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Port</label>
          <Input type="number" value={(config.port as number) ?? 443}
            onChange={(e) => onChange({ ...config, port: Number(e.target.value) })} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Warning threshold (days)</label>
          <Input type="number" value={(config.warningDaysBeforeExpiry as number) ?? 14}
            onChange={(e) => onChange({ ...config, warningDaysBeforeExpiry: Number(e.target.value) })}
            placeholder="14" />
          <p className="text-xs text-muted-foreground">Check goes degraded within this many days</p>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Critical threshold (days)</label>
          <Input type="number" value={(config.criticalDaysBeforeExpiry as number) ?? 3}
            onChange={(e) => onChange({ ...config, criticalDaysBeforeExpiry: Number(e.target.value) })}
            placeholder="3" />
          <p className="text-xs text-muted-foreground">Check goes down within this many days</p>
        </div>
      </div>
    </div>
  );
}
