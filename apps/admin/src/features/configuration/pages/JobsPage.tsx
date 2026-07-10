import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Clock, ScrollText } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
} from "@/components/ui/table";
import { jobsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const CHECK_EXECUTION_SOURCE = "Piro.Infrastructure.Jobs.CheckExecutionJob";

const JOB_SOURCE_CONTEXT: Record<string, string> = {
  "piro:escalation-check": "Piro.Infrastructure.Jobs.EscalationCheckJob",
  "piro:maintenance-scheduler": "Piro.Infrastructure.Jobs.MaintenanceSchedulerJob",
};

function logsUrlFor(job: { jobGroup: string; jobName: string; check?: { id: number } | null }) {
  if (job.check) {
    return `${ROUTES.LOGS}?source=${encodeURIComponent(CHECK_EXECUTION_SOURCE)}&checkId=${job.check.id}`;
  }
  const source =
    JOB_SOURCE_CONTEXT[`${job.jobGroup}:${job.jobName}`] ?? JOB_SOURCE_CONTEXT[job.jobGroup];
  return source ? `${ROUTES.LOGS}?source=${encodeURIComponent(source)}` : ROUTES.LOGS;
}

const STATE_STYLES: Record<string, string> = {
  Normal: "bg-green-500/15 text-green-600 dark:text-green-400",
  Paused: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  Complete: "bg-muted text-muted-foreground",
  Blocked: "bg-blue-500/15 text-blue-600 dark:text-blue-400",
  Error: "bg-red-500/15 text-red-600 dark:text-red-400",
  None: "bg-muted text-muted-foreground",
};

function formatDate(value?: string | null) {
  if (!value) return "—";
  return new Date(value).toLocaleString("en-US", {
    month: "short", day: "numeric", year: "numeric", hour: "2-digit", minute: "2-digit",
  });
}

export default function JobsPage() {
  const navigate = useNavigate();
  const { data: jobs = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.JOBS,
    queryFn: jobsApi.list,
    refetchInterval: 15_000,
  });

  return (
    <>
      <PageHeader
        breadcrumbs={[{ label: "Jobs" }]}
        subheader="Scheduling status of background jobs — check executions, escalation processing, and maintenance transitions."
      />

      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading && (
          <div className="py-16 text-center text-sm text-muted-foreground">Loading…</div>
        )}
        {!isLoading && jobs.length === 0 && (
          <div className="py-14 flex flex-col items-center gap-3">
            <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center">
              <Clock size={20} className="text-muted-foreground" />
            </div>
            <p className="text-sm font-medium">No scheduled jobs found</p>
          </div>
        )}
        {!isLoading && jobs.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Job</TableHead>
                <TableHead>Check</TableHead>
                <TableHead>State</TableHead>
                <TableHead>Previous Run</TableHead>
                <TableHead>Next Run</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {jobs.map((j) => (
                <TableRow key={`${j.jobGroup}.${j.jobName}.${j.triggerName}`}>
                  <TableCell className="whitespace-normal">
                    <p className="font-medium">{j.jobName}</p>
                    <p className="text-xs text-muted-foreground font-mono">{j.jobGroup}</p>
                  </TableCell>
                  <TableCell className="whitespace-normal">
                    {j.check ? (
                      <button
                        onClick={() => navigate(ROUTES.CHECKS.DETAIL(j.check!.serviceSlug, j.check!.slug))}
                        className="text-left hover:underline"
                      >
                        <p className="font-medium">{j.check.name}</p>
                        <p className="text-xs text-muted-foreground font-mono">{j.check.serviceSlug}/{j.check.slug}</p>
                      </button>
                    ) : (
                      <span className="text-muted-foreground/50">—</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <span className={`rounded-full px-3 py-0.5 text-xs font-semibold ${STATE_STYLES[j.state] ?? STATE_STYLES.None}`}>
                      {j.state}
                    </span>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{formatDate(j.previousFireTimeUtc)}</TableCell>
                  <TableCell className="text-muted-foreground">{formatDate(j.nextFireTimeUtc)}</TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => navigate(logsUrlFor(j))}
                      title="View logs"
                      className="text-muted-foreground hover:text-foreground"
                    >
                      <ScrollText size={15} />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </>
  );
}
