import { useFormContext } from "react-hook-form";
import { Input } from "@/components/ui/input";
import type { CheckConfigFormValues } from "@/features/checks/validations";
import { Field, FieldDescription, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";

function SslConfig() {
  const { register, formState: { errors } } = useFormContext<CheckConfigFormValues>();

  return (
    <div className="flex flex-col gap-4">
      <Field>
        <Label required>Host</Label>
        <Input {...register("host")} placeholder="example.com" />
        <FieldError errors={[errors.host]} />
      </Field>
      <Field>
        <Label required>Port</Label>
        <Input type="number" {...register("port", { valueAsNumber: true })} />
        <FieldError errors={[errors.port]} />
        <FieldDescription>
          This check only fails when the certificate is actually expired or the handshake fails.
          To alert before that (e.g. warn at 30 days, critical at 7), add an Alert Configuration
          for "Certificate expiry" below.
        </FieldDescription>
      </Field>
    </div>
  );
}

export default SslConfig;
