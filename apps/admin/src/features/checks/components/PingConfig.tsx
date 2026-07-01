import { Input } from "@/components/ui/input";
import type { CheckConfigProps } from "./types";

export function PingConfig({ config, onChange }: CheckConfigProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-sm font-semibold">Host <span className="text-destructive">*</span></label>
      <Input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
        placeholder="example.com" />
    </div>
  );
}
