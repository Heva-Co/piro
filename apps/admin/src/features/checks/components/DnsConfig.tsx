import { useState } from "react";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { CheckConfigProps } from "./types";

const DNS_RECORD_TYPES = ["A", "AAAA", "CNAME"] as const;

function isValidIpOrHostname(value: string): boolean {
  if (!value.trim()) return false;
  const ipv4 = /^(\d{1,3}\.){3}\d{1,3}$/.test(value);
  if (ipv4) return value.split(".").every((o) => Number(o) <= 255);
  if (value.includes(":")) return /^[0-9a-fA-F:]+$/.test(value) && value.split(":").length >= 2;
  return /^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$|^[a-zA-Z0-9-]{1,63}$/.test(value.replace(/\.$/, ""));
}

function isValidExpectedValue(value: string, recordType: string): boolean {
  if (!value.trim()) return true;
  if (recordType === "A") return /^(\d{1,3}\.){3}\d{1,3}$/.test(value) && value.split(".").every((o) => Number(o) <= 255);
  if (recordType === "AAAA") return /^[0-9a-fA-F:]+$/.test(value) && value.includes(":");
  if (recordType === "CNAME") return /^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$|^[a-zA-Z0-9-]{1,63}$/.test(value.replace(/\.$/, ""));
  return true;
}

export function DnsConfig({ config, onChange }: CheckConfigProps) {
  const recordType = (config.recordType as string) ?? "A";
  const nameServers = (config.nameServers as string[]) ?? [];
  const expectedValue = (config.expectedValue as string) ?? "";
  const degradedAfter = (config.degradedAfter as number | "") ?? "";
  const downAfter = (config.downAfter as number | "") ?? "";

  const [nsErrors, setNsErrors] = useState<Record<number, string>>({});
  const [evError, setEvError] = useState("");

  function setNs(index: number, value: string) {
    const updated = [...nameServers];
    updated[index] = value;
    onChange({ ...config, nameServers: updated });
  }

  function addNs() {
    onChange({ ...config, nameServers: [...nameServers, ""] });
  }

  function removeNs(index: number) {
    onChange({ ...config, nameServers: nameServers.filter((_, i) => i !== index) });
    setNsErrors((prev) => { const next = { ...prev }; delete next[index]; return next; });
  }

  function validateNs(index: number, value: string) {
    if (value && !isValidIpOrHostname(value)) {
      setNsErrors((prev) => ({ ...prev, [index]: "Must be a valid IP address or hostname." }));
    } else {
      setNsErrors((prev) => { const next = { ...prev }; delete next[index]; return next; });
    }
  }

  function validateEv(value: string) {
    if (value && !isValidExpectedValue(value, recordType)) {
      const hint = recordType === "A" ? "IPv4 address" : recordType === "AAAA" ? "IPv6 address" : "hostname or FQDN";
      setEvError(`Must be a valid ${hint}.`);
    } else {
      setEvError("");
    }
  }

  const evLabel = recordType === "A" || recordType === "AAAA" ? "Expected IP" : "Expected Value";
  const evPlaceholder = recordType === "A" ? "1.2.3.4" : recordType === "AAAA" ? "2001:db8::1" : "example.com";
  const showThresholds = nameServers.length > 1;

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Host <span className="text-destructive">*</span></label>
          <Input value={(config.host as string) ?? ""} onChange={(e) => onChange({ ...config, host: e.target.value })}
            placeholder="example.com" />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Record Type</label>
          <Select value={recordType} onValueChange={(v) => onChange({ ...config, recordType: v, expectedValue: "" })}>
            <SelectTrigger>
              <SelectValue>{(v: string) => v}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {DNS_RECORD_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">{evLabel}</label>
        <Input
          value={expectedValue}
          onChange={(e) => onChange({ ...config, expectedValue: e.target.value })}
          onBlur={(e) => validateEv(e.target.value)}
          placeholder={evPlaceholder}
          className={evError ? "border-destructive" : undefined}
        />
        {evError && <p className="text-xs text-destructive">{evError}</p>}
        <p className="text-xs text-muted-foreground">Optional. Leave blank to accept any successful resolution.</p>
      </div>

      <div>
        <label className="text-sm font-semibold block mb-1">Name Servers</label>
        <p className="text-xs text-muted-foreground mb-2">Optional. Leave empty to use the system resolver. Add multiple to query in parallel.</p>
        {nameServers.map((ns, i) => (
          <div key={i} className="flex gap-2 mb-2">
            <div className="flex-1 flex flex-col gap-1">
              <Input
                value={ns}
                onChange={(e) => setNs(i, e.target.value)}
                onBlur={(e) => validateNs(i, e.target.value)}
                placeholder="8.8.8.8 or ns1.example.com"
                className={nsErrors[i] ? "border-destructive" : undefined}
              />
              {nsErrors[i] && <p className="text-xs text-destructive">{nsErrors[i]}</p>}
            </div>
            <button type="button" onClick={() => removeNs(i)}
              className="shrink-0 px-2 py-1 text-xs text-muted-foreground hover:text-destructive transition-colors">
              Remove
            </button>
          </div>
        ))}
        <button type="button" onClick={addNs} className="text-sm hover:underline">
          + Add Name Server
        </button>
      </div>

      {showThresholds && (
        <div className="grid grid-cols-2 gap-4 pt-2 border-t">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">DEGRADED after N failures</label>
            <Input type="number" min={1} value={degradedAfter}
              onChange={(e) => onChange({ ...config, degradedAfter: e.target.value ? Number(e.target.value) : "" })}
              placeholder="1" />
            <p className="text-xs text-muted-foreground">Default: 1</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">DOWN after N failures</label>
            <Input type="number" min={1} max={nameServers.length} value={downAfter}
              onChange={(e) => onChange({ ...config, downAfter: e.target.value ? Number(e.target.value) : "" })}
              placeholder={String(nameServers.length)} />
            <p className="text-xs text-muted-foreground">Default: all ({nameServers.length})</p>
          </div>
        </div>
      )}
    </div>
  );
}
