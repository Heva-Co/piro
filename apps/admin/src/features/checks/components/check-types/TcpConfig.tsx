import { useFormContext } from "react-hook-form";
import { Input } from "@/components/ui/input";
import { Field, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import type { CheckConfigFormValues } from "@/features/checks/validations";

function TcpConfig() {
  const { register, formState: { errors } } = useFormContext<CheckConfigFormValues>();

  return (
    <div className="grid grid-cols-2 gap-4">
      <Field>
        <Label required>Host</Label>
        <Input {...register("host")} placeholder="example.com" />
        <FieldError errors={[errors.host]} />
      </Field>
      <Field>
        <Label required>Port</Label>
        <Input type="number" {...register("port", { valueAsNumber: true })} placeholder="80" />
        <FieldError errors={[errors.port]} />
      </Field>
    </div>
  );
}

export default TcpConfig;
