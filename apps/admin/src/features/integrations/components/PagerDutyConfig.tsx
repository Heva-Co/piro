import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const inp = "rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const lbl = "text-sm font-semibold";

export function PagerDutyConfig() {
  const { register, formState: { errors } } = useFormContext<IntegrationFormValues>();
  return (
    <div className="flex flex-col gap-1.5">
      <label className={lbl}>Routing Key <span className="text-destructive">*</span></label>
      <Input type="password" {...register("pdRoutingKey")} placeholder="PagerDuty Events API v2 routing key" className={inp} />
      {errors.pdRoutingKey && <p className="text-xs text-destructive">{errors.pdRoutingKey.message}</p>}
    </div>
  );
}
