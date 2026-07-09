import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const inp = (hasError: boolean) =>
  `rounded-lg border ${hasError ? "border-destructive" : "border-border"} bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full`;
const lbl = "text-sm font-semibold";

export function JiraConfig() {
  const { register, formState: { errors } } = useFormContext<IntegrationFormValues>();
  return (
    <div className="flex flex-col gap-4">
      <p className="text-xs text-muted-foreground">
        Credentials for your Jira Cloud instance. The API token is used for authentication via Basic Auth.
      </p>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Base URL <span className="text-destructive">*</span></label>
        <Input
          {...register("jiraBaseUrl", {
            required: "Base URL is required",
            pattern: { value: /^https:\/\/[a-zA-Z0-9-]+\.atlassian\.net\/?$/, message: "Must be a valid Atlassian URL (https://your-org.atlassian.net)" },
          })}
          placeholder="https://your-org.atlassian.net"
          className={inp(!!errors.jiraBaseUrl)}
        />
        {errors.jiraBaseUrl && <p className="text-xs text-destructive">{errors.jiraBaseUrl.message}</p>}
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>Email <span className="text-destructive">*</span></label>
        <Input
          {...register("jiraEmail", {
            required: "Email is required",
            pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: "Enter a valid email" },
          })}
          placeholder="you@example.com"
          className={inp(!!errors.jiraEmail)}
        />
        {errors.jiraEmail && <p className="text-xs text-destructive">{errors.jiraEmail.message}</p>}
      </div>
      <div className="flex flex-col gap-1.5">
        <label className={lbl}>API Token <span className="text-destructive">*</span></label>
        <Input
          type="password"
          {...register("jiraApiToken", { required: "API Token is required" })}
          placeholder="Your Jira API token"
          className={inp(!!errors.jiraApiToken)}
        />
        {errors.jiraApiToken && <p className="text-xs text-destructive">{errors.jiraApiToken.message}</p>}
        <p className="text-xs text-muted-foreground">
          Generate one at <span className="font-mono">id.atlassian.com/manage-profile/security/api-tokens</span>
        </p>
      </div>
      <div className="flex gap-4">
        <div className="flex flex-col gap-1.5 flex-1">
          <label className={lbl}>Project Key</label>
          <Input {...register("jiraProjectKey")} placeholder="e.g. OPS" className={inp(false)} />
        </div>
        <div className="flex flex-col gap-1.5 flex-1">
          <label className={lbl}>Issue Type</label>
          <Input {...register("jiraIssueType")} placeholder="e.g. Incident" className={inp(false)} />
        </div>
      </div>
    </div>
  );
}
