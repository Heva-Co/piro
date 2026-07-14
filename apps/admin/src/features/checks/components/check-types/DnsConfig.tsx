import { useFormContext, useFieldArray, Controller } from "react-hook-form";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Field, FieldDescription, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import type { CheckConfigFormValues } from "@/features/checks/validations";

const DNS_RECORD_TYPES = ["A", "AAAA", "CNAME"] as const;

export function DnsConfig() {
  const { register, control, watch, formState: { errors } } = useFormContext<CheckConfigFormValues>();
  const recordType = watch("recordType");
  const { fields, append, remove } = useFieldArray({ control, name: "nameServers" as never });

  const evLabel = recordType === "A" || recordType === "AAAA" ? "Expected IP" : "Expected Value";
  const evPlaceholder = recordType === "A" ? "1.2.3.4" : recordType === "AAAA" ? "2001:db8::1" : "example.com";

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4">
        <Field>
          <Label required>Host</Label>
          <Input {...register("host")} placeholder="example.com" />
          <FieldError errors={[errors.host]} />
        </Field>
        <Field>
          <Label>Record Type</Label>
          <Controller name="recordType" control={control} render={({ field }) => (
            <Select value={field.value} onValueChange={(v) => v && field.onChange(v)}>
              <SelectTrigger>
                <SelectValue>{(v: string) => v}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {DNS_RECORD_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          )} />
        </Field>
      </div>

      <Field>
        <Label>{evLabel}</Label>
        <Input {...register("expectedValue")} placeholder={evPlaceholder} />
        <FieldError errors={[errors.expectedValue]} />
        <FieldDescription>Optional. Leave blank to accept any successful resolution.</FieldDescription>
      </Field>

      <Field>
        <Label>Name Servers</Label>
        <FieldDescription>Optional. Leave empty to use the system resolver. Add multiple to query in parallel.</FieldDescription>
        {fields.map((field, i) => (
          <div key={field.id} className="flex gap-2 mb-2">
            <div className="flex-1 flex flex-col gap-1">
              <Input {...register(`nameServers.${i}` as const)} placeholder="8.8.8.8 or ns1.example.com" />
              <FieldError errors={[errors.nameServers?.[i]]} />
            </div>
            <Button type="button" variant="ghost" size="icon" onClick={() => remove(i)}>
              <Trash2 className="size-4 text-muted-foreground" />
            </Button>
          </div>
        ))}
        <button type="button" onClick={() => append("")} className="text-sm hover:underline w-fit">
          + Add Name Server
        </button>
      </Field>
    </div>
  );
}
