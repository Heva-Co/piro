import { useEffect, useRef } from "react";
import { useFormContext, Controller } from "react-hook-form";
import cronstrue from "cronstrue";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import { CRON_PRESETS } from "@/constants/checks";
import type { CheckConfigFormValues } from "@/features/checks/validations";
import { Textarea } from "@/components/ui/textarea";
import { slugify } from "@/utils/slugify";

function describeCron(cron: string): string {
  try {
    return cronstrue.toString(cron, { verbose: false });
  } catch {
    return cron;
  }
}

interface Props {
  /** In create mode: editable Select node. In edit mode: read-only input node. */
  typeNode: React.ReactNode;
  /** The slug is editable and auto-derived from Name until the user edits it directly. Omit for edit mode, where the slug is fixed. */
  slugEditable?: boolean;
}

export function CheckGeneralSettingsFields({ typeNode, slugEditable = false }: Props) {
  const { register, control, watch, setValue, formState: { errors } } = useFormContext<CheckConfigFormValues>();
  const showCustomCron = watch("showCustomCron");
  const cron = watch("cron");
  const isActive = watch("isActive");
  const isMultiRegion = watch("isMultiRegion");
  const name = watch("name");

  const slugManual = useRef(false);
  useEffect(() => {
    if (!slugEditable || slugManual.current) return;
    setValue("slug", slugify(name ?? ""));
  }, [slugEditable, name, setValue]);

  return (
    <div className="flex flex-col gap-5">
      {/* Name + Slug */}
      <div className="grid grid-cols-2 gap-4">
        <Field>
          <Label required>Name</Label>
          <Input {...register("name")} placeholder="Health Endpoint" />
          <FieldError errors={[errors.name]} />
        </Field>
        <Field>
          <Label required>Slug</Label>
          {slugEditable ? (
            <Input
              {...register("slug")}
              onChange={(e) => {
                slugManual.current = true;
                setValue("slug", e.target.value);
              }}
              placeholder="health"
              className="font-mono"
            />
          ) : (
            <Input {...register("slug")} readOnly className="font-mono bg-muted text-muted-foreground" />
          )}
          <FieldError errors={[errors.slug]} />
          {!errors.slug && (
            <FieldDescription>
              {slugEditable ? "Unique identifier within this service" : "Cannot be changed after creation"}
            </FieldDescription>
          )}
        </Field>
      </div>

      {/* Description */}
      <Field>
        <FieldLabel >Description</FieldLabel>
        <Textarea {...register("description")} rows={2}
          placeholder="A brief description of what this check monitors" />
      </Field>

      {/* Type + Cron */}
      <div className="grid grid-cols-2 gap-4">
        <Field className="flex flex-col gap-1.5">
          <FieldLabel required>Type</FieldLabel>
          {typeNode}
        </Field>
        <Field className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Cron Schedule</label>
          {showCustomCron ? (
            <Input {...register("cron")} placeholder="*/5 * * * *" className="font-mono" />
          ) : (
            <Controller name="cron" control={control} render={({ field }) => (
              <Select value={field.value} onValueChange={(v) => v && field.onChange(v)}>
                <SelectTrigger className="w-full">
                  <SelectValue>{(v: string) => CRON_PRESETS.find((p) => p.value === v)?.label ?? v}</SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {CRON_PRESETS.filter((p) => p.value !== "custom").map((p) => (
                    <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )} />
          )}
          <button type="button" onClick={() => setValue("showCustomCron", !showCustomCron)}
            className="text-xs text-left hover:underline w-fit">
            {showCustomCron ? "← Use preset" : "Enter custom cron →"}
          </button>
          <FieldError errors={[errors.cron]} />
          {!errors.cron && (
            <p className="text-xs text-muted-foreground">
              {showCustomCron ? describeCron(cron) : (CRON_PRESETS.find((p) => p.value === cron)?.label ?? describeCron(cron))}
            </p>
          )}
        </Field>
      </div>

      {/* Active + Multi-region */}
      <div className="grid grid-cols-2 gap-4">
        <Field >
          <label className="text-sm font-semibold">Active</label>
          <div className="flex items-center gap-2.5">
            <Switch checked={isActive} onCheckedChange={(v) => setValue("isActive", v)} />
            <FieldDescription>{isActive ? "Check is running" : "Check is paused"}</FieldDescription>
          </div>
        </Field>
        <Field >
          <label className="text-sm font-semibold">Multi-region</label>
          <div className="flex items-center gap-2.5">
            <Switch checked={isMultiRegion} onCheckedChange={(v) => setValue("isMultiRegion", v)} />
            <FieldDescription >{isMultiRegion ? "Enabled" : "Disabled"}</FieldDescription>
          </div>
        </Field>
      </div>
    </div>
  );
}
