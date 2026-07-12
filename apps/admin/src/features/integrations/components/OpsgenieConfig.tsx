import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const lbl = "text-sm font-semibold";

export function OpsgenieConfig() {
  const { register, formState: { errors } } = useFormContext<IntegrationFormValues>();
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>API Key <span className="text-destructive">*</span></label>
        <Input type="password" {...register("ogApiKey")} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"  />
        {errors.ogApiKey && <p className="text-xs text-destructive">{errors.ogApiKey.message}</p>}
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Region</label>
        <select {...register("ogRegion")}>
          <option value="US">US</option>
          <option value="EU">EU</option>
        </select>
      </div>
    </>
  );
}
