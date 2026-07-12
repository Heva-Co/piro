import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const inp = "rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const lbl = "text-sm font-semibold";

 function TwilioConfig() {
  const { register, formState: { errors } } = useFormContext<IntegrationFormValues>();
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Account SID <span className="text-destructive">*</span></label>
        <Input {...register("twAccountSid")} placeholder="ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" className={inp} />
        {errors.twAccountSid && <p className="text-xs text-destructive">{errors.twAccountSid.message}</p>}
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Auth Token <span className="text-destructive">*</span></label>
        <Input type="password" {...register("twAuthToken")} className={inp} />
        {errors.twAuthToken && <p className="text-xs text-destructive">{errors.twAuthToken.message}</p>}
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>From number <span className="text-destructive">*</span></label>
        <Input {...register("twFromNumber")} placeholder="+15005550006" className={inp} />
        {errors.twFromNumber && <p className="text-xs text-destructive">{errors.twFromNumber.message}</p>}
      </div>
    </>
  );
}

export default TwilioConfig