import type { UseFormRegister } from "react-hook-form";

interface FormValues {
  jiraBaseUrl: string;
  jiraEmail: string;
  jiraApiToken: string;
  jiraProjectKey: string;
  jiraIssueType: string;
  [key: string]: unknown;
}

const inp = (hasError: boolean) =>
  `rounded-lg border ${hasError ? "border-destructive" : "border-border"} bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full`;

export function JiraConfig({
  register,
  errors = {},
}: {
  register: UseFormRegister<FormValues>;
  errors?: { baseUrl?: string; email?: string; apiToken?: string };
  // these are only used to satisfy the Controller render prop in the parent
  baseUrl?: string;
  email?: string;
  apiToken?: string;
  projectKey?: string;
  issueType?: string;
}) {
  return (
    <div className="flex flex-col gap-4">
      <p className="text-xs text-muted-foreground">
        Credentials for your Jira Cloud instance. The API token is used for authentication via Basic Auth.
      </p>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">
          Base URL <span className="text-destructive">*</span>
        </label>
        <input
          {...register("jiraBaseUrl", {
            required: "Base URL is required",
            pattern: { value: /^https:\/\/[a-zA-Z0-9-]+\.atlassian\.net\/?$/, message: "Must be a valid Atlassian URL (https://your-org.atlassian.net)" },
          })}
          placeholder="https://your-org.atlassian.net"
          className={inp(!!errors.baseUrl)}
        />
        {errors.baseUrl && <p className="text-xs text-destructive">{errors.baseUrl}</p>}
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">
          Email <span className="text-destructive">*</span>
        </label>
        <input
          {...register("jiraEmail", {
            required: "Email is required",
            pattern: { value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/, message: "Enter a valid email" },
          })}
          placeholder="you@example.com"
          className={inp(!!errors.email)}
        />
        {errors.email && <p className="text-xs text-destructive">{errors.email}</p>}
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">
          API Token <span className="text-destructive">*</span>
        </label>
        <input
          type="password"
          {...register("jiraApiToken", { required: "API Token is required" })}
          placeholder="Your Jira API token"
          className={inp(!!errors.apiToken)}
        />
        {errors.apiToken && <p className="text-xs text-destructive">{errors.apiToken}</p>}
        <p className="text-xs text-muted-foreground">
          Generate one at <span className="font-mono">id.atlassian.com/manage-profile/security/api-tokens</span>
        </p>
      </div>

      <div className="flex gap-4">
        <div className="flex flex-col gap-1.5 flex-1">
          <label className="text-sm font-semibold">Project Key</label>
          <input
            {...register("jiraProjectKey")}
            placeholder="e.g. OPS"
            className={inp(false)}
          />
        </div>
        <div className="flex flex-col gap-1.5 flex-1">
          <label className="text-sm font-semibold">Issue Type</label>
          <input
            {...register("jiraIssueType")}
            placeholder="e.g. Incident"
            className={inp(false)}
          />
        </div>
      </div>
    </div>
  );
}
