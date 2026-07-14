import { useFormContext } from "react-hook-form";
import { Input } from "@/components/ui/input";
import { Field, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import type { CheckConfigFormValues } from "@/features/checks/validations";

function PingConfig() {
  const { register, formState: { errors } } = useFormContext<CheckConfigFormValues>();

  return (
    <Field>
      <Label required>Host</Label>
      <Input {...register("host")} placeholder="example.com" />
      <FieldError errors={[errors.host]} />
    </Field>
  );
}

export default PingConfig;