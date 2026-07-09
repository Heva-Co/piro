import { useFormContext, Controller } from "react-hook-form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { CRON_PRESETS, CHECK_CRITICALITY_MAP, type CheckCriticalityKey } from "@/constants/checks";

export interface CheckGeneralFormValues {
  name: string;
  description: string;
  cron: string;
  showCustomCron: boolean;
  isActive: boolean;
  isMultiRegion: boolean;
  criticality: CheckCriticalityKey;
  autoCreate: boolean;
}

interface Props {
  /** In create mode: editable Select node. In edit mode: read-only input node. */
  typeNode: React.ReactNode;
  /** Slug field node — create: editable Input, edit: read-only input */
  slugNode?: React.ReactNode;
}

export function CheckGeneralSettingsFields({ typeNode, slugNode }: Props) {
  const { register, control, watch, setValue } = useFormContext<CheckGeneralFormValues>();
  const showCustomCron = watch("showCustomCron");
  const cron = watch("cron");
  const isActive = watch("isActive");
  const isMultiRegion = watch("isMultiRegion");
  const autoCreate = watch("autoCreate");

  return (
    <div className="flex flex-col gap-5">
      {/* Name + Slug */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Name <span className="text-destructive">*</span></label>
          <Input {...register("name")} placeholder="Health Endpoint" />
        </div>
        {slugNode && <div className="flex flex-col gap-1.5">{slugNode}</div>}
      </div>

      {/* Description */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Description</label>
        <textarea {...register("description")} rows={2}
          placeholder="A brief description of what this check monitors"
          className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full disabled:cursor-not-allowed disabled:opacity-50 resize-none" />
      </div>

      {/* Type + Cron */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Type <span className="text-destructive">*</span></label>
          {typeNode}
        </div>
        <div className="flex flex-col gap-1.5">
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
          <p className="text-xs text-muted-foreground">
            {showCustomCron ? cron : (CRON_PRESETS.find((p) => p.value === cron)?.label ?? cron)}
          </p>
        </div>
      </div>

      {/* Active + Multi-region */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-2">
          <label className="text-sm font-semibold">Active</label>
          <div className="flex items-center gap-2.5">
            <Switch checked={isActive} onCheckedChange={(v) => setValue("isActive", v)} />
            <span className="text-sm text-muted-foreground">{isActive ? "Check is running" : "Check is paused"}</span>
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-sm font-semibold">Multi-region</label>
          <div className="flex items-center gap-2.5">
            <Switch checked={isMultiRegion} onCheckedChange={(v) => setValue("isMultiRegion", v)} />
            <span className="text-sm text-muted-foreground">{isMultiRegion ? "Enabled" : "Disabled"}</span>
          </div>
        </div>
      </div>

      {/* Incident Automation */}
      <div className="border-t pt-5 flex flex-col gap-4">
        <div>
          <p className="text-sm font-semibold">Incident Automation</p>
          <p className="text-xs text-muted-foreground mt-0.5">Configure how this check interacts with incident management</p>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">Criticality</label>
            <Controller name="criticality" control={control} render={({ field }) => (
              <Select value={field.value} onValueChange={(v) => v && field.onChange(v as CheckCriticalityKey)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.entries(CHECK_CRITICALITY_MAP) as [CheckCriticalityKey, { label: string; description: string }][]).map(([key, meta]) => (
                    <SelectItem key={key} value={key}>
                      {meta.label} — {meta.description}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )} />
            <p className="text-xs text-muted-foreground">Determines incident impact when auto-created</p>
          </div>
          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold">Auto-create incident</label>
            <div className="flex items-center gap-2.5">
              <Switch checked={autoCreate} onCheckedChange={(v) => setValue("autoCreate", v)} />
              <span className="text-sm text-muted-foreground">{autoCreate ? "Enabled" : "Disabled"}</span>
            </div>
            <p className="text-xs text-muted-foreground">Creates an internal incident when this check starts alerting</p>
          </div>
        </div>
      </div>
    </div>
  );
}
