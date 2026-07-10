import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Clock, ScrollText } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { jobsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

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
          <div className="py-16 text-center text-sm text-gray-400">Loading…</div>
        )}
        {!isLoading && jobs.length === 0 && (
          <div className="py-14 flex flex-col items-center gap-3">
            <div className="w-12 h-12 rounded-full bg-gray-100 flex items-center justify-center">
              <Clock size={20} className="text-gray-400" />
            </div>
            <p className="text-sm font-medium text-gray-700">No scheduled jobs found</p>
          </div>
        )}
        {!isLoading && jobs.length > 0 && (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs font-semibold text-gray-500 uppercase">
                <th className="px-5 py-3">Job</th>
                <th className="px-5 py-3">Check</th>
                <th className="px-5 py-3">State</th>
                <th className="px-5 py-3">Previous Run</th>
                <th className="px-5 py-3">Next Run</th>
                <th className="px-5 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {jobs.map((j, i) => (
                <tr key={`${j.jobGroup}.${j.jobName}.${j.triggerName}`}
                  className={i > 0 ? "border-t border-gray-100" : ""}>
                  <td className="px-5 py-4">
                    <p className="font-medium text-gray-900">{j.jobName}</p>
                    <p className="text-xs text-gray-400 font-mono">{j.jobGroup}</p>
                  </td>
                  <td className="px-5 py-4">
                    {j.check ? (
                      <button
                        onClick={() => navigate(ROUTES.CHECKS.DETAIL(j.check!.serviceSlug, j.check!.slug))}
                        className="text-left hover:underline"
                      >
                        <p className="font-medium text-gray-900">{j.check.name}</p>
                        <p className="text-xs text-gray-400 font-mono">{j.check.serviceSlug}/{j.check.slug}</p>
                      </button>
                    ) : (
                      <span className="text-gray-300">—</span>
                    )}
                  </td>
                  <td className="px-5 py-4">
                    <span className={`rounded-full px-3 py-0.5 text-xs font-semibold ${STATE_STYLES[j.state] ?? STATE_STYLES.None}`}>
                      {j.state}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-gray-600">{formatDate(j.previousFireTimeUtc)}</td>
                  <td className="px-5 py-4 text-gray-600">{formatDate(j.nextFireTimeUtc)}</td>
                  <td className="px-5 py-4">
                    {j.check && (
                      <button
                        onClick={() => navigate(ROUTES.CHECKS.LOGS(j.check!.serviceSlug, j.check!.slug))}
                        title="View logs"
                        className="flex items-center gap-1.5 text-gray-400 hover:text-gray-700 transition-colors"
                      >
                        <ScrollText size={15} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
