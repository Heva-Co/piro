import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const inp = "rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const lbl = "text-sm font-semibold";

export function OpsgenieConfig() {
  const { register, formState: { errors } } = useFormContext<IntegrationFormValues>();
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>API Key <span className="text-destructive">*</span></label>
        <Input {...register("ogApiKey", { required: "Required" })} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" className={inp} />
        {errors.ogApiKey && <p className="text-xs text-destructive">{errors.ogApiKey.message}</p>}
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Region</label>
        <select {...register("ogRegion")} className={inp}>
          <option value="US">US</option>
          <option value="EU">EU</option>
        </select>
      </div>
    </>
  );
}
