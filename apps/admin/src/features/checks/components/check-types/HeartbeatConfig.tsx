import { useFormContext } from "react-hook-form";
import { Input } from "@/components/ui/input";
import { Field, FieldDescription } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import type { CheckConfigFormValues } from "@/features/checks/validations";

export function HeartbeatConfig() {
  const { register } = useFormContext<CheckConfigFormValues>();

  return (
    <Field>
      <Label>Grace period (seconds)</Label>
      <Input type="number" {...register("gracePeriodSeconds", { valueAsNumber: true })} />
      <FieldDescription>
        A heartbeat check waits for a ping from your service. If no ping is received within the grace period, the check is marked as down.
      </FieldDescription>
    </Field>
  );
}

export default HeartbeatConfig