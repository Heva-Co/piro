import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const inp = "rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const lbl = "text-sm font-semibold";

export function NtfyConfig() {
  const { register } = useFormContext<IntegrationFormValues>();
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Server URL <span className="text-destructive">*</span></label>
        <Input type="url" {...register("ntfyServerUrl", { required: "Required" })} className={inp} />
        <p className="text-xs text-muted-foreground">Use <code className="font-mono">https://ntfy.sh</code> for the public server or your self-hosted instance URL.</p>
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Access Token</label>
        <Input {...register("ntfyToken")} placeholder="Optional — required for protected topics" className={inp} />
      </div>
    </>
  );
}
