import { forwardRef, useImperativeHandle, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from "@/components/ui/accordion";
import { Field, FieldDescription, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import type { AlertConfig, CreateAlertConfigRequest } from "@/lib/actions/alert-configs";
import { alertConfigSchema, type AlertConfigFormValues } from "@/features/checks/validations";
import {
  type AlertFor,
  ALERT_FOR_LABELS,
  ALERT_VALUE_PLACEHOLDERS,
  ALERT_VALUE_DESCRIPTIONS,
  DEFAULT_ALERT_VALUES,
  NUMERIC_ALERT_FORS,
  STATUS_ALERT_VALUES,
  ALERT_SEVERITY_OPTIONS,
} from "@/types/checks";

export type AlertConfigDraft = CreateAlertConfigRequest;

interface Props {
  initial: AlertConfigDraft;
  saved: AlertConfig | null;
  alertForOptions: readonly AlertFor[];
  onSave: (draft: AlertConfigDraft) => Promise<void>;
  onRemove: () => void;
  isSaving: boolean;
  /** When true, every edit is committed immediately via onSave and no Save button is shown — for in-memory drafts (e.g. check creation) where there's nothing to persist yet. */
  autoSave?: boolean;
}

export interface AlertConfigRowHandle {
  /** Runs validation; returns whether the row is currently valid, opening it and surfacing errors if not. */
  validate: () => Promise<boolean>;
}

export const AlertConfigRow = forwardRef<AlertConfigRowHandle, Props>(function AlertConfigRow(props, ref) {
  const { initial, saved, alertForOptions, onSave, onRemove, isSaving, autoSave = false } = props;
  const [error, setError] = useState("");
  const [savedFlash, setSavedFlash] = useState(false);
  const [open, setOpen] = useState(saved === null);

  const { register, control, watch, setValue, handleSubmit, trigger, formState: { errors } } = useForm<AlertConfigFormValues>({
    resolver: zodResolver(alertConfigSchema),
    defaultValues: initial,
  });

  const alertFor = watch("alertFor") as AlertFor;
  const alertValue = watch("alertValue");
  const severity = watch("severity");
  const isActive = watch("isActive");

  function toDraft(values: AlertConfigFormValues): AlertConfigDraft {
    return { ...values, alertFor: values.alertFor as AlertFor };
  }

  async function submit(values: AlertConfigFormValues) {
    setError("");
    try {
      await onSave(toDraft(values));
      setSavedFlash(true);
      setTimeout(() => setSavedFlash(false), 2000);
    } catch {
      setError("Failed to save alert configuration.");
    }
  }

  const commit = handleSubmit(submit);

  useImperativeHandle(ref, () => ({
    validate: async () => {
      const valid = await trigger();
      if (!valid) {
        setOpen(true);
        return false;
      }
      if (autoSave) await commit();
      return true;
    },
  }));

  function onFieldChange() {
    if (autoSave) void commit();
  }

  const summary = `${ALERT_FOR_LABELS[alertFor] ?? alertFor} = ${alertValue} · ${severity}${isActive ? "" : " · Disabled"}`;

  return (
    <Accordion
      value={open ? ["item"] : []}
      onValueChange={(v) => setOpen(v.includes("item"))}
      className="rounded-lg border border-border bg-card"
    >
      <AccordionItem value="item" className="border-none">
        <AccordionTrigger className="px-4 py-3 hover:no-underline">
          <span className="flex flex-col gap-0.5 text-left flex-1 min-w-0">
            <span className="text-sm font-medium truncate">{summary}</span>
          </span>
        </AccordionTrigger>
        <AccordionContent className="px-4 pb-4">
          <form onSubmit={commit} className="flex flex-col gap-4">
            {error && <p className="text-sm text-destructive">{error}</p>}

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <Field>
                <Label>Alert For</Label>
                <Controller name="alertFor" control={control} render={({ field }) => (
                  <Select value={field.value} onValueChange={(v) => {
                    if (!v) return;
                    field.onChange(v);
                    setValue("alertValue", DEFAULT_ALERT_VALUES[v as AlertFor] ?? "");
                    onFieldChange();
                  }}>
                    <SelectTrigger className="w-full">
                      <SelectValue>{ALERT_FOR_LABELS[field.value as AlertFor] ?? field.value}</SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {alertForOptions.map((o) => <SelectItem key={o} value={o}>{ALERT_FOR_LABELS[o as AlertFor] ?? o}</SelectItem>)}
                    </SelectContent>
                  </Select>
                )} />
                <FieldDescription>Which raw signal this alert evaluates.</FieldDescription>
              </Field>
              <Field>
                <Label>Value</Label>
                {alertFor === "Status" ? (
                  <Controller name="alertValue" control={control} render={({ field }) => (
                    <Select value={field.value} onValueChange={(v) => { if (v) { field.onChange(v); onFieldChange(); } }}>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {STATUS_ALERT_VALUES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  )} />
                ) : (
                  <Input
                    type={NUMERIC_ALERT_FORS.has(alertFor) ? "number" : "text"}
                    min={NUMERIC_ALERT_FORS.has(alertFor) ? 0 : undefined}
                    {...register("alertValue")}
                    onChange={(e) => { setValue("alertValue", e.target.value); onFieldChange(); }}
                    placeholder={ALERT_VALUE_PLACEHOLDERS[alertFor]}
                  />
                )}
                <FieldError errors={[errors.alertValue]} />
                {!errors.alertValue && <FieldDescription>{ALERT_VALUE_DESCRIPTIONS[alertFor]}</FieldDescription>}
              </Field>
              <Field>
                <Label>Failure threshold</Label>
                <Input type="number" min={1} {...register("failureThreshold", { valueAsNumber: true })}
                  onChange={(e) => { setValue("failureThreshold", Number(e.target.value)); onFieldChange(); }} />
                <FieldError errors={[errors.failureThreshold]} />
                {!errors.failureThreshold && (
                  <FieldDescription>Consecutive failures before this alert fires.</FieldDescription>
                )}
              </Field>
              <Field>
                <Label>Success threshold</Label>
                <Input type="number" min={1} {...register("successThreshold", { valueAsNumber: true })}
                  onChange={(e) => { setValue("successThreshold", Number(e.target.value)); onFieldChange(); }} />
                <FieldError errors={[errors.successThreshold]} />
                {!errors.successThreshold && (
                  <FieldDescription>Consecutive successes needed to auto-resolve.</FieldDescription>
                )}
              </Field>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <Field>
                <Label>Severity</Label>
                <Controller name="severity" control={control} render={({ field }) => (
                  <Select value={field.value} onValueChange={(v) => { if (v) { field.onChange(v); onFieldChange(); } }}>
                    <SelectTrigger className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ALERT_SEVERITY_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                    </SelectContent>
                  </Select>
                )} />
                <FieldDescription>How urgently this alert should be treated.</FieldDescription>
              </Field>
              <Field>
                <Label>Active</Label>
                <div className="flex items-center gap-2.5 h-9">
                  <Switch checked={isActive} onCheckedChange={(v) => { setValue("isActive", v); onFieldChange(); }} />
                  <span className="text-sm text-muted-foreground">{isActive ? "Enabled" : "Disabled"}</span>
                </div>
                <FieldDescription>Disabled alerts are kept but never evaluated.</FieldDescription>
              </Field>
            </div>

            <div className="flex items-center gap-3 pt-1 border-t -mx-4 px-4 pt-4">
              {!autoSave && (
                <Button type="submit" size="sm" disabled={isSaving}>
                  {savedFlash ? "Saved!" : isSaving ? "Saving…" : saved ? "Save changes" : "Save"}
                </Button>
              )}
              <Button type="button" size="sm" variant="ghost" onClick={onRemove}
                className="text-destructive hover:text-destructive">
                <Trash2 size={14} />
                Remove
              </Button>
            </div>
          </form>
        </AccordionContent>
      </AccordionItem>
    </Accordion>
  );
});
