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
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Port</label>
          <Input type="number" value={(config.port as number) ?? 443}
            onChange={(e) => onChange({ ...config, port: Number(e.target.value) })} />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Warning threshold (days)</label>
          <Input type="number" value={(config.warningDaysBeforeExpiry as number) ?? 30}
            onChange={(e) => onChange({ ...config, warningDaysBeforeExpiry: Number(e.target.value) })} />
        </div>
      </div>
    </div>
  );
}
