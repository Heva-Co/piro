import type { CheckConfigFormValues } from "@/features/checks/validations";

/** Serializes the type-specific fields of a check config form into the backend's typeDataJson shape. */
export function buildTypeDataJson(values: CheckConfigFormValues): string {
  switch (values.type) {
    case "HTTP":
      return JSON.stringify({
        url: values.url,
        method: values.method,
        timeout: values.timeout,
        expectedStatusCodes: values.expectedStatusCodes.split(",").map((s) => s.trim()).filter(Boolean),
        followRedirects: values.followRedirects,
        body: values.body || undefined,
        headers: Object.fromEntries(values.headers.filter((h) => h.key).map((h) => [h.key, h.value])),
        responseRules: values.responseRules.length > 0 ? values.responseRules : undefined,
      });
    case "DNS":
      return JSON.stringify({
        host: values.host,
        recordType: values.recordType,
        expectedValue: values.expectedValue || undefined,
        nameServers: values.nameServers.filter(Boolean).length > 0 ? values.nameServers.filter(Boolean) : undefined,
      });
    case "TCP":
      return JSON.stringify({ host: values.host, port: values.port });
    case "Ping":
      return JSON.stringify({ host: values.host });
    case "SSL":
      return JSON.stringify({ host: values.host, port: values.port });
    case "Heartbeat":
      return JSON.stringify({ gracePeriodSeconds: values.gracePeriodSeconds });
    case "GCP_CloudRunJob":
      return JSON.stringify({
        projectId: values.projectId,
        region: values.region,
        jobName: values.jobName,
        maxAgeHours: values.maxAgeHours,
      });
    default:
      return "{}";
  }
}
