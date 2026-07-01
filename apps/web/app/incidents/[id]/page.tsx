import { notFound } from "next/navigation";
import Link from "next/link";
import { Monitor } from "lucide-react";
import { publicApi, type ServiceStatus } from "@/lib/api";

interface Props {
  params: Promise<{ id: string }>;
}

export async function generateMetadata({ params }: Props) {
  const { id } = await params;
  try {
    const incident = await publicApi.incident(id);
    return { title: incident.title };
  } catch {
    return { title: "Incident" };
  }
}

const stateColor: Record<string, string> = {
  Investigating: "text-amber-500",
  Identified: "text-orange-500",
  Monitoring: "text-blue-500",
  Resolved: "text-green-500",
};

const impactColor: Record<ServiceStatus, string> = {
  DOWN: "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400",
  DEGRADED: "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400",
  MAINTENANCE: "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400",
  UP: "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400",
  NO_DATA: "bg-secondary text-muted-foreground",
  FAILURE: "bg-secondary text-muted-foreground",
};

function fmtTs(ts: number): string {
  return new Date(ts * 1000).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export default async function IncidentDetailPage({ params }: Props) {
  const { id } = await params;

  let incident;
  try {
    incident = await publicApi.incident(id);
  } catch {
    notFound();
  }

  return (
    <div className="max-w-6xl mx-auto px-4 py-10 flex flex-col gap-6">
      <div>
        <Link href="/" className="text-sm text-muted-foreground hover:underline mb-2 inline-block">
          ← Back
        </Link>
        <h1 className="text-3xl font-bold">{incident.title}</h1>
        <p className="text-muted-foreground text-sm mt-1">
          Started {fmtTs(incident.startDateTime)}
          {incident.endDateTime ? (
            <> · Resolved {fmtTs(incident.endDateTime)}</>
          ) : (
            <>
              {" · "}
              <span className={stateColor[incident.state] ?? ""}>{incident.state}</span>
            </>
          )}
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Updates */}
        <div className="lg:col-span-2 rounded-2xl border overflow-hidden">
          <div className="flex items-center gap-2 px-5 py-4 border-b bg-secondary/30">
            <Monitor className="size-4 text-muted-foreground" />
            <span className="font-medium text-sm">Updates ({incident.comments.length})</span>
          </div>
          {incident.comments.length === 0 ? (
            <div className="px-5 py-8 text-center text-muted-foreground text-sm">No updates yet.</div>
          ) : (
            <div className="divide-y">
              {incident.comments.map((comment) => (
                <div key={comment.id} className="px-5 py-4 flex flex-col gap-1">
                  <div className="flex items-center justify-between gap-2">
                    <span
                      className={`text-xs font-semibold uppercase tracking-wide ${stateColor[comment.state] ?? "text-muted-foreground"}`}
                    >
                      {comment.state}
                    </span>
                    <span className="text-xs text-muted-foreground">
                      {fmtTs(comment.commentedAt)}
                    </span>
                  </div>
                  <p className="text-sm">{comment.comment}</p>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Affected services */}
        <div className="rounded-2xl border overflow-hidden">
          <div className="flex items-center gap-2 px-5 py-4 border-b bg-secondary/30">
            <Monitor className="size-4 text-muted-foreground" />
            <span className="font-medium text-sm">
              Affected Services ({incident.isGlobal ? "All" : incident.services.length})
            </span>
          </div>
          {incident.isGlobal ? (
            <div className="px-5 py-6 flex flex-col items-center gap-2 text-center">
              <Monitor className="size-10 text-amber-500 opacity-70" />
              <span className="text-sm font-medium">All services affected</span>
              <span className="text-xs text-muted-foreground">
                This is a global incident affecting all services.
              </span>
            </div>
          ) : incident.services.length === 0 ? (
            <div className="px-5 py-10 flex flex-col items-center gap-2 text-muted-foreground">
              <Monitor className="size-10 opacity-30" />
              <span className="text-sm">No services affected</span>
            </div>
          ) : (
            <div className="divide-y">
              {incident.services.map((svc) => (
                <div
                  key={svc.serviceSlug}
                  className="px-5 py-3 flex items-center justify-between gap-3"
                >
                  <Link
                    href={`/services/${svc.serviceSlug}`}
                    className="text-sm font-medium hover:underline"
                  >
                    {svc.serviceSlug}
                  </Link>
                  <span
                    className={`text-xs font-semibold rounded-full px-2.5 py-0.5 ${impactColor[svc.impact]}`}
                  >
                    {svc.impact}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
