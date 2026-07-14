import { useFormContext, Controller } from "react-hook-form";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Field, FieldDescription, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import { ROUTES } from "@/constants/routes";
import type { CheckConfigFormValues } from "@/features/checks/validations";
import type { GcpIntegration } from "@/features/checks/components/types";

interface Props {
  integrations: GcpIntegration[];
}

export function GcpCloudRunJobConfig({ integrations }: Props) {
  const { register, control, formState: { errors } } = useFormContext<CheckConfigFormValues>();
  const gcpIntegrations = integrations.filter((i) => i.type === "GoogleCloud");

  return (
    <div className="flex flex-col gap-4">
      <Field>
        <Label required>Google Cloud Integration</Label>
        <Controller name="integrationId" control={control} render={({ field }) => (
          <Select
            value={field.value === "" ? "" : String(field.value)}
            onValueChange={(v) => field.onChange(v ? Number(v) : "")}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select an integration…">
                {(v: string) => gcpIntegrations.find((i) => String(i.id) === v)?.name ?? "Select an integration…"}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {gcpIntegrations.map((i) => (
                <SelectItem key={i.id} value={String(i.id)}>{i.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        )} />
        <FieldError errors={[errors.integrationId]} />
        {gcpIntegrations.length === 0 && (
          <p className="text-xs text-amber-600">
            No Google Cloud integrations found.{" "}
            <a href={ROUTES.INTEGRATIONS.NEW} className="underline">Create one first.</a>
          </p>
        )}
      </Field>

      <div className="grid grid-cols-2 gap-4">
        <Field>
          <Label required>Project ID</Label>
          <Input {...register("projectId")} placeholder="my-gcp-project" />
          <FieldError errors={[errors.projectId]} />
        </Field>
        <Field>
          <Label required>Region</Label>
          <Input {...register("region")} placeholder="us-central1" />
          <FieldError errors={[errors.region]} />
        </Field>
      </div>

      <Field>
        <Label required>Job Name</Label>
        <Input {...register("jobName")} placeholder="my-batch-job" />
        <FieldError errors={[errors.jobName]} />
      </Field>

      <Field>
        <Label>Max Age (hours)</Label>
        <Input type="number" min={1} {...register("maxAgeHours", { valueAsNumber: true })} />
        <FieldDescription>
          Mark as DOWN if no execution has completed within this window. Use 25 for a daily job.
        </FieldDescription>
      </Field>
    </div>
  );
}
