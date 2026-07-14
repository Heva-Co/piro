import { useFormContext, useFieldArray, Controller } from "react-hook-form";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Field, FieldDescription, FieldError } from "@/components/ui/field";
import { Label } from "@/components/ui/label";
import type { CheckConfigFormValues } from "@/features/checks/validations";
import { Textarea } from "@/components/ui/textarea";

const RULE_TYPES: { value: CheckConfigFormValues["responseRules"][number]["type"]; label: string; hint: string; hasExpected: boolean }[] = [
  { value: "contains",     label: "Contains",      hint: "Body must contain this substring.",               hasExpected: false },
  { value: "not_contains", label: "Not Contains",  hint: "Body must NOT contain this substring.",           hasExpected: false },
  { value: "regex",        label: "Regex",         hint: "Body must match this regular expression.",        hasExpected: false },
  { value: "json_path",    label: "JSON Path",     hint: "JSONPath expression (e.g. $.status.indicator).",  hasExpected: true  },
  { value: "xml_path",     label: "XML Path",      hint: "XPath expression (e.g. //status/text()).",        hasExpected: true  },
];

export function HttpConfig() {
  const { register, control, watch, formState: { errors } } = useFormContext<CheckConfigFormValues>();
  const method = watch("method");
  const headers = useFieldArray({ control, name: "headers" });
  const rules = useFieldArray({ control, name: "responseRules" });

  return (
    <div className="flex flex-col gap-5">
      <Field>
        <Label required>URL</Label>
        <Input {...register("url")} placeholder="https://example.com/health" />
        <FieldError errors={[errors.url]} />
      </Field>

      <div className="grid grid-cols-3 gap-4">
        <Field>
          <Label>Method</Label>
          <Controller name="method" control={control} render={({ field }) => (
            <Select value={field.value} onValueChange={(v) => v && field.onChange(v)}>
              <SelectTrigger><SelectValue>{(v: string | null) => v ?? "GET"}</SelectValue></SelectTrigger>
              <SelectContent>
                {["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"].map((m) => (
                  <SelectItem key={m} value={m}>{m}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          )} />
        </Field>
        <Field>
          <Label>Timeout (ms)</Label>
          <Input type="number" {...register("timeout", { valueAsNumber: true })} />
        </Field>
        <Field>
          <Label>Expected Status Codes</Label>
          <Input {...register("expectedStatusCodes")} placeholder="2xx, 301" />
          <FieldError errors={[errors.expectedStatusCodes]} />
          {!errors.expectedStatusCodes && (
            <FieldDescription>Codes or classes — <code>200</code>, <code>2xx</code>, <code>3xx</code>. Default: <code>200</code>.</FieldDescription>
          )}
        </Field>
      </div>

      <label className="flex items-center gap-2 text-sm cursor-pointer">
        <input type="checkbox" {...register("followRedirects")} className="size-4 rounded" />
        Follow Redirects
      </label>

      {method !== "GET" && (
        <Field>
          <Label>Body</Label>
          <Textarea {...register("body")} rows={3}/>
        </Field>
      )}

      <div>
        <label className="text-sm font-semibold block">Headers</label>
        <p className="text-xs text-muted-foreground mb-2">Custom headers sent with the request.</p>
        {headers.fields.map((field, i) => (
          <div key={field.id} className="flex gap-2 mb-2">
            <Input placeholder="Key" {...register(`headers.${i}.key` as const)} />
            <Input placeholder="Value" {...register(`headers.${i}.value` as const)} />
            <Button type="button" variant="ghost" size="icon" onClick={() => headers.remove(i)}>
              <Trash2 className="size-4 text-muted-foreground" />
            </Button>
          </div>
        ))}
        <button type="button"
          onClick={() => headers.append({ key: "", value: "" })}
          className="text-sm text-muted-foreground hover:text-foreground transition-colors">
          + Add Header
        </button>
      </div>

      <div>
        <div className="flex items-center justify-between mb-2">
          <div>
            <label className="text-sm font-semibold block">Response Rules</label>
            <p className="text-xs text-muted-foreground">Assertions evaluated against the response body. First failure wins.</p>
          </div>
        </div>
        <div className="flex flex-col gap-3">
          {rules.fields.map((field, i) => {
            const ruleType = watch(`responseRules.${i}.type`);
            const meta = RULE_TYPES.find((t) => t.value === ruleType)!;
            return (
              <div key={field.id} className="rounded-lg border border-border bg-muted/20 p-3 flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <Controller name={`responseRules.${i}.type`} control={control} render={({ field: typeField }) => (
                    <Select value={typeField.value} onValueChange={(v) => v && typeField.onChange(v)}>
                      <SelectTrigger className="w-36 shrink-0"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {RULE_TYPES.map((t) => (
                          <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )} />
                  <Input
                    {...register(`responseRules.${i}.value` as const)}
                    placeholder={meta.hasExpected ? "Expression" : "Value"}
                  />
                  <Button type="button" variant="ghost" size="icon" onClick={() => rules.remove(i)}>
                    <Trash2 className="size-4 text-muted-foreground" />
                  </Button>
                </div>
                {meta.hasExpected && (
                  <Input
                    {...register(`responseRules.${i}.expected` as const)}
                    placeholder="Expected value (leave blank to just check existence)"
                  />
                )}
                <div className="flex items-center gap-3">
                  <label className="flex items-center gap-1.5 text-xs text-muted-foreground cursor-pointer">
                    <input type="checkbox" {...register(`responseRules.${i}.degraded` as const)} className="size-3.5 rounded" />
                    Mark as Degraded (not Down) on failure
                  </label>
                  <p className="text-xs text-muted-foreground">{meta.hint}</p>
                </div>
              </div>
            );
          })}
        </div>
        <button type="button"
          onClick={() => rules.append({ type: "contains", value: "", degraded: false })}
          className="mt-2 text-sm text-muted-foreground hover:text-foreground transition-colors">
          + Add Rule
        </button>
      </div>
    </div>
  );
}
