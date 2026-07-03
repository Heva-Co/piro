"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, RefreshCw, Globe } from "lucide-react";
import ReactMarkdown from "react-markdown";
import { Timeline } from "@/components/ui/timeline";
import type { publicApi } from "@/lib/api";

type Incident = Awaited<ReturnType<typeof publicApi.incident>>;

const stateColor: Record<string, string> = {
  Investigating: "bg-red-100 text-red-700",
  Identified:    "bg-orange-100 text-orange-700",
  Monitoring:    "bg-amber-100 text-amber-700",
  Resolved:      "bg-green-100 text-green-700",
};

function timeAgo(ts: number): string {
  const diff = Math.floor((Date.now() - ts * 1000) / 1000);
  if (diff < 60) return "just now";
  if (diff < 3600) return `${Math.floor(diff / 60)} minutes ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} hours ago`;
  return `${Math.floor(diff / 86400)} days ago`;
}

function fmtTs(ts: number): string {
  return new Date(ts * 1000).toLocaleString(undefined, {
    month: "short", day: "numeric", year: "numeric",
    hour: "numeric", minute: "2-digit",
  });
}

interface Props {
  id: string;
  initial: Incident;
}

export function IncidentDetailClient({ id, initial }: Props) {
  const [incident, setIncident] = useState(initial);
  const [lastFetched, setLastFetched] = useState(new Date());
  const [fetching, setFetching] = useState(false);

  async function fetchIncident() {
    if (fetching) return;
    setFetching(true);
    try {
      const res = await fetch(`/api/incidents/${id}`);
      if (res.ok) {
        setIncident(await res.json());
        setLastFetched(new Date());
      }
    } catch { /* silent */ }
    finally { setFetching(false); }
  }

  useEffect(() => {
    const interval = setInterval(fetchIncident, 60_000);
    return () => clearInterval(interval);
  }, [id]);

  const latestState = incident.comments.length > 0
    ? incident.comments[incident.comments.length - 1].state
    : incident.state;

  const badgeClass = stateColor[latestState] ?? "bg-gray-100 text-gray-600";

  return (
    <main className="mx-auto w-full max-w-screen-lg px-8 py-10 flex flex-col gap-6">
      {/* Top bar */}
      <div className="flex items-center justify-between gap-4">
        <Link href="/" className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors">
          <ArrowLeft size={15} /> Back
        </Link>
        <div className="flex items-center gap-3 text-xs text-muted-foreground">
          <button
            onClick={fetchIncident}
            disabled={fetching}
            className="hover:text-foreground transition-colors disabled:cursor-not-allowed"
            title="Refresh"
          >
            <RefreshCw size={13} className={`shrink-0 ${fetching ? "animate-spin" : ""}`} />
          </button>
          <span>Fetched {timeAgo(Math.floor(lastFetched.getTime() / 1000))}</span>
          <span className="text-border">·</span>
          <Globe size={13} className="shrink-0" />
          <span>Local Time</span>
        </div>
      </div>

      {/* Header */}
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-3 flex-wrap">
          <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${badgeClass}`}>
            {latestState}
          </span>
        </div>
        <h1 className="text-3xl font-bold tracking-tight">{incident.title}</h1>
        <p className="text-sm text-muted-foreground uppercase tracking-widest font-medium">
          Started &nbsp; {fmtTs(incident.startDateTime)}
          {incident.endDateTime && (
            <span className="ml-3">· Resolved {fmtTs(incident.endDateTime)}</span>
          )}
        </p>
      </div>

      <hr className="border-border" />

      {/* Body */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Timeline */}
        <div className="lg:col-span-2 flex flex-col gap-1">
          <h2 className="text-lg font-semibold mb-4">Timeline</h2>

          <Timeline
            items={[...incident.comments].reverse().map((c) => ({
              id: c.id,
              state: c.state,
              timestamp: c.commentedAt,
              body: <div className="prose prose-sm dark:prose-invert max-w-none [&_p]:my-0.5 [&_ul]:my-1 [&_ol]:my-1 [&_li]:my-0"><ReactMarkdown>{c.comment}</ReactMarkdown></div>,
            }))}
          />
        </div>

        {/* Affected services */}
        <div>
          <div className="rounded-xl border border-border p-4 flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
                Affected Services
              </span>
            </div>

            {incident.isGlobal ? (
              <p className="text-sm text-muted-foreground italic">All services affected</p>
            ) : incident.services.length === 0 ? (
              <p className="text-sm text-muted-foreground">No services listed.</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {incident.services.map((svc) => (
                  <Link
                    key={svc.serviceSlug}
                    href={`/services/${svc.serviceSlug}`}
                    className="rounded-md border border-border bg-secondary/50 px-3 py-1.5 text-sm font-medium hover:bg-secondary transition-colors"
                  >
                    {svc.serviceName || svc.serviceSlug}
                  </Link>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </main>
  );
}
