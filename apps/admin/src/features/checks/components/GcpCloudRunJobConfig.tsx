import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ROUTES } from "@/constants/routes";
import type { GcpCheckConfigProps } from "./types";

export function GcpCloudRunJobConfig({ config, onChange, integrations }: GcpCheckConfigProps) {
  const gcpIntegrations = integrations.filter((i) => i.type === "GoogleCloud");
  const integrationId = (config.integrationId as number | "") ?? "";

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Google Cloud Integration <span className="text-destructive">*</span></label>
        <Select
          value={String(integrationId)}
          onValueChange={(v) => onChange({ ...config, integrationId: v ? Number(v) : "" })}
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
        {gcpIntegrations.length === 0 && (
          <p className="text-xs text-amber-600">
            No Google Cloud integrations found.{" "}
            <a href={ROUTES.INTEGRATIONS.NEW} className="underline">Create one first.</a>
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Project ID <span className="text-destructive">*</span></label>
          <Input value={(config.projectId as string) ?? ""}
            onChange={(e) => onChange({ ...config, projectId: e.target.value })}
            placeholder="my-gcp-project" />
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Region <span className="text-destructive">*</span></label>
          <Input value={(config.region as string) ?? ""}
            onChange={(e) => onChange({ ...config, region: e.target.value })}
            placeholder="us-central1" />
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Job Name <span className="text-destructive">*</span></label>
        <Input value={(config.jobName as string) ?? ""}
          onChange={(e) => onChange({ ...config, jobName: e.target.value })}
          placeholder="my-batch-job" />
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Max Age (hours)</label>
        <Input type="number" min={1} value={(config.maxAgeHours as number) ?? 25}
          onChange={(e) => onChange({ ...config, maxAgeHours: Number(e.target.value) })} />
        <p className="text-xs text-muted-foreground">
          Mark as DOWN if no execution has completed within this window. Use 25 for a daily job.
        </p>
      </div>
    </div>
  );
}
