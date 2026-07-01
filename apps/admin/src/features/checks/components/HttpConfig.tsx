import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { CheckConfigProps } from "./types";

export function HttpConfig({ config, onChange }: CheckConfigProps) {
  const url = (config.url as string) ?? "";
  const method = (config.method as string) ?? "GET";
  const timeout = (config.timeout as number) ?? 5000;
  const codesRaw = config.expectedStatusCodes;
  const expectedStatusCodes = Array.isArray(codesRaw)
    ? codesRaw.join(", ")
    : ((codesRaw as string) ?? "200");
  const followRedirects = (config.followRedirects as boolean) ?? true;
  const body = (config.body as string) ?? "";
  const headers = (config.headers as { key: string; value: string }[]) ?? [{ key: "", value: "" }];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">URL <span className="text-destructive">*</span></label>
        <Input value={url} onChange={(e) => onChange({ ...config, url: e.target.value })}
          placeholder="https://example.com/health" />
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Method</label>
          <Select value={method} onValueChange={(v) => v && onChange({ ...config, method: v })}>
            <SelectTrigger>
              <SelectValue>{(v: string) => v}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"].map((m) => (
                <SelectItem key={m} value={m}>{m}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Timeout (ms)</label>
          <Input type="number" value={timeout}
            onChange={(e) => onChange({ ...config, timeout: Number(e.target.value) })} />
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Expected Status Codes</label>
        <Input
          value={expectedStatusCodes}
          onChange={(e) => onChange({
            ...config,
            expectedStatusCodes: e.target.value.split(",").map((s) => parseInt(s.trim(), 10)).filter((n) => !isNaN(n)),
          })}
          placeholder="200, 201"
        />
        <p className="text-xs text-muted-foreground">Comma-separated list of acceptable HTTP status codes</p>
      </div>

      <label className="flex items-center gap-2 text-sm cursor-pointer">
        <input type="checkbox" checked={followRedirects}
          onChange={(e) => onChange({ ...config, followRedirects: e.target.checked })}
          className="size-4 rounded" />
        Follow Redirects
      </label>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Body</label>
        <textarea value={body} rows={3}
          onChange={(e) => onChange({ ...config, body: e.target.value })}
          className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full font-mono text-xs resize-none" />
      </div>

      <div>
        <label className="text-sm font-semibold block mb-2">Headers</label>
        {headers.map((h, i) => (
          <div key={i} className="flex gap-2 mb-2">
            <Input placeholder="Key" value={h.key}
              onChange={(e) => {
                const hs = [...headers];
                hs[i] = { ...hs[i], key: e.target.value };
                onChange({ ...config, headers: hs });
              }} />
            <Input placeholder="Value" value={h.value}
              onChange={(e) => {
                const hs = [...headers];
                hs[i] = { ...hs[i], value: e.target.value };
                onChange({ ...config, headers: hs });
              }} />
          </div>
        ))}
        <button type="button"
          onClick={() => onChange({ ...config, headers: [...headers, { key: "", value: "" }] })}
          className="text-sm hover:underline">
          + Add Header
        </button>
      </div>
    </div>
  );
}
