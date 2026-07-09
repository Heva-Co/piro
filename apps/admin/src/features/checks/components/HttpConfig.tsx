import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { CheckConfigProps } from "./types";

type ResponseRule = {
  type: "contains" | "not_contains" | "regex" | "json_path" | "xml_path";
  value: string;
  expected?: string;
  degraded: boolean;
};

const RULE_TYPES: { value: ResponseRule["type"]; label: string; hint: string; hasExpected: boolean }[] = [
  { value: "contains",     label: "Contains",      hint: "Body must contain this substring.",               hasExpected: false },
  { value: "not_contains", label: "Not Contains",  hint: "Body must NOT contain this substring.",           hasExpected: false },
  { value: "regex",        label: "Regex",         hint: "Body must match this regular expression.",        hasExpected: false },
  { value: "json_path",    label: "JSON Path",     hint: "JSONPath expression (e.g. $.status.indicator).",  hasExpected: true  },
  { value: "xml_path",     label: "XML Path",      hint: "XPath expression (e.g. //status/text()).",        hasExpected: true  },
];

const inp = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";

export function HttpConfig({ config, onChange }: CheckConfigProps) {
  const url = (config.url as string) ?? "";
  const method = (config.method as string) ?? "GET";
  const timeout = (config.timeout as number) ?? 5000;

  // Status codes: support both legacy number[] and new string[]
  const codesRaw = config.expectedStatusCodes;
  const expectedStatusCodes: string = Array.isArray(codesRaw)
    ? codesRaw.join(", ")
    : ((codesRaw as string) ?? "2xx");

  const followRedirects = (config.followRedirects as boolean) ?? true;
  const body = (config.body as string) ?? "";

  const rawHeaders = config.headers;
  const headers: { key: string; value: string }[] = Array.isArray(rawHeaders) && rawHeaders.length > 0
    ? rawHeaders as { key: string; value: string }[]
    : [{ key: "", value: "" }];

  const rawRules = config.responseRules;
  const rules: ResponseRule[] = Array.isArray(rawRules) && rawRules.length > 0
    ? rawRules as ResponseRule[]
    : [];

  const degradedLatencyMs = config.degradedLatencyMs as number | undefined;
  const downLatencyMs = config.downLatencyMs as number | undefined;

  function updateRules(next: ResponseRule[]) {
    onChange({ ...config, responseRules: next });
  }

  function addRule() {
    updateRules([...rules, { type: "contains", value: "", degraded: false }]);
  }

  function removeRule(i: number) {
    updateRules(rules.filter((_, idx) => idx !== i));
  }

  function updateRule(i: number, patch: Partial<ResponseRule>) {
    const next = [...rules];
    next[i] = { ...next[i], ...patch };
    updateRules(next);
  }

  return (
    <div className="flex flex-col gap-5">
      {/* URL */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">URL <span className="text-destructive">*</span></label>
        <input value={url} onChange={(e) => onChange({ ...config, url: e.target.value })}
          placeholder="https://example.com/health" className={inp} />
      </div>

      {/* Method + Timeout */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Method</label>
          <Select value={method} onValueChange={(v) => v && onChange({ ...config, method: v })}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"].map((m) => (
                <SelectItem key={m} value={m}>{m}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Timeout (ms)</label>
          <input type="number" value={timeout} className={inp}
            onChange={(e) => onChange({ ...config, timeout: Number(e.target.value) })} />
        </div>
      </div>

      {/* Expected Status Codes */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Expected Status Codes</label>
        <input
          value={expectedStatusCodes}
          className={inp}
          onChange={(e) => onChange({
            ...config,
            expectedStatusCodes: e.target.value.split(",").map((s) => s.trim()).filter(Boolean),
          })}
          placeholder="2xx, 301"
        />
        <p className="text-xs text-muted-foreground">
          Comma-separated codes or classes: <code>200</code>, <code>2xx</code>, <code>3xx</code>. Default: any 2xx.
        </p>
      </div>

      {/* Follow Redirects */}
      <label className="flex items-center gap-2 text-sm cursor-pointer">
        <input type="checkbox" checked={followRedirects}
          onChange={(e) => onChange({ ...config, followRedirects: e.target.checked })}
          className="size-4 rounded" />
        Follow Redirects
      </label>

      {/* Body */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Body</label>
        <textarea value={body} rows={3}
          onChange={(e) => onChange({ ...config, body: e.target.value })}
          className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full font-mono text-xs resize-none" />
      </div>

      {/* Headers */}
      <div>
        <label className="text-sm font-semibold block mb-2">Headers</label>
        {headers.map((h, i) => (
          <div key={i} className="flex gap-2 mb-2">
            <input placeholder="Key" value={h.key} className={inp}
              onChange={(e) => {
                const hs = [...headers];
                hs[i] = { ...hs[i], key: e.target.value };
                onChange({ ...config, headers: hs });
              }} />
            <input placeholder="Value" value={h.value} className={inp}
              onChange={(e) => {
                const hs = [...headers];
                hs[i] = { ...hs[i], value: e.target.value };
                onChange({ ...config, headers: hs });
              }} />
          </div>
        ))}
        <button type="button"
          onClick={() => onChange({ ...config, headers: [...headers, { key: "", value: "" }] })}
          className="text-sm text-muted-foreground hover:text-foreground transition-colors">
          + Add Header
        </button>
      </div>

      {/* Response Rules */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <div>
            <label className="text-sm font-semibold block">Response Rules</label>
            <p className="text-xs text-muted-foreground">Assertions evaluated against the response body. First failure wins.</p>
          </div>
        </div>
        <div className="flex flex-col gap-3">
          {rules.map((rule, i) => {
            const meta = RULE_TYPES.find((t) => t.value === rule.type)!;
            return (
              <div key={i} className="rounded-lg border border-border bg-muted/20 p-3 flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <Select value={rule.type} onValueChange={(v) => v && updateRule(i, { type: v as ResponseRule["type"], expected: undefined })}>
                    <SelectTrigger className="w-36 shrink-0"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {RULE_TYPES.map((t) => (
                        <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <input
                    value={rule.value}
                    onChange={(e) => updateRule(i, { value: e.target.value })}
                    placeholder={meta.hasExpected ? "Expression" : "Value"}
                    className={inp}
                  />
                  <Button type="button" variant="ghost" size="icon" onClick={() => removeRule(i)}>
                    <Trash2 className="size-4 text-muted-foreground" />
                  </Button>
                </div>
                {meta.hasExpected && (
                  <input
                    value={rule.expected ?? ""}
                    onChange={(e) => updateRule(i, { expected: e.target.value || undefined })}
                    placeholder="Expected value (leave blank to just check existence)"
                    className={inp}
                  />
                )}
                <div className="flex items-center gap-3">
                  <label className="flex items-center gap-1.5 text-xs text-muted-foreground cursor-pointer">
                    <input type="checkbox" checked={rule.degraded}
                      onChange={(e) => updateRule(i, { degraded: e.target.checked })}
                      className="size-3.5 rounded" />
                    Mark as Degraded (not Down) on failure
                  </label>
                  <p className="text-xs text-muted-foreground">{meta.hint}</p>
                </div>
              </div>
            );
          })}
        </div>
        <button type="button" onClick={addRule}
          className="mt-2 text-sm text-muted-foreground hover:text-foreground transition-colors">
          + Add Rule
        </button>
      </div>

      {/* Latency Thresholds */}
      <div className="flex flex-col gap-3">
        <label className="text-sm font-semibold block">Latency Thresholds</label>
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-xs text-muted-foreground">Degraded above (ms)</label>
            <input
              type="number"
              className={inp}
              value={degradedLatencyMs ?? ""}
              placeholder="e.g. 1000"
              onChange={(e) => onChange({
                ...config,
                degradedLatencyMs: e.target.value ? Number(e.target.value) : undefined,
              })}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-xs text-muted-foreground">Down above (ms)</label>
            <input
              type="number"
              className={inp}
              value={downLatencyMs ?? ""}
              placeholder="e.g. 3000"
              onChange={(e) => onChange({
                ...config,
                downLatencyMs: e.target.value ? Number(e.target.value) : undefined,
              })}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
