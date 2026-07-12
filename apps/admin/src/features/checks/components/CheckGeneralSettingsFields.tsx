import { useFormContext, Controller } from "react-hook-form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { CRON_PRESETS } from "@/constants/checks";

export interface CheckGeneralFormValues {
  name: string;
  description: string;
  cron: string;
  showCustomCron: boolean;
  isActive: boolean;
  isMultiRegion: boolean;
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
    </div>
  );
}
