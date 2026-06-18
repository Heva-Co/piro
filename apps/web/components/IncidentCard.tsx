"use client";

import { useState } from "react";
import { ChevronDown } from "lucide-react";
import type { Incident } from "@/lib/api";
import Link from "next/link";

const stateColor: Record<string, string> = {
  Investigating: "text-amber-500",
  Identified: "text-orange-500",
  Monitoring: "text-blue-500",
  Resolved: "text-green-500",
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

function fmtDuration(from: number, to: number): string {
  const secs = to - from;
  if (secs < 60) return `${secs} seconds`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins} minute${mins !== 1 ? "s" : ""}`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs} hour${hrs !== 1 ? "s" : ""}`;
  const days = Math.floor(hrs / 24);
  return `${days} day${days !== 1 ? "s" : ""}`;
}

interface Props {
  incident: Incident;
}

export function IncidentCard({ incident }: Props) {
  const [showComments, setShowComments] = useState(true);
  const endForDuration = incident.endDateTime ?? Math.floor(Date.now() / 1000);
  const lastUpdated =
    incident.comments.length > 0
      ? incident.comments[incident.comments.length - 1].commentedAt
      : incident.startDateTime;

  return (
    <div className="rounded-3xl border p-4 flex flex-col gap-2">
      <div className="flex flex-col gap-0.5">
        <span
          className={`text-xs font-semibold uppercase tracking-wide ${stateColor[incident.state] ?? "text-muted-foreground"}`}
        >
          {incident.state}
        </span>
        <Link href={`/incidents/${incident.id}`} className="font-semibold text-base hover:underline">
          {incident.title}
        </Link>
      </div>

      <div className="flex items-center justify-between gap-2 text-xs font-medium mt-1">
        <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
          {fmtTs(incident.startDateTime)}
        </span>
        <span className="relative flex-1 text-center">
          <span className="absolute inset-y-1/2 left-0 right-0 border-t" />
          <span className="relative z-10 bg-background px-2 text-muted-foreground">
            {fmtDuration(incident.startDateTime, endForDuration)}
          </span>
        </span>
        {incident.endDateTime ? (
          <span className="shrink-0 rounded-full border px-3 py-1.5 whitespace-nowrap">
            {fmtTs(incident.endDateTime)}
          </span>
        ) : (
          <span className="shrink-0 rounded-full border px-3 py-1.5">Ongoing</span>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 text-xs font-medium mt-1">
        <div className="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
          <span>Last Updated</span>
          <span>{fmtTs(lastUpdated)}</span>
        </div>
        <div className="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
          <span>Status</span>
          <span className={stateColor[incident.state] ?? ""}>{incident.state}</span>
        </div>
        <div className="bg-secondary flex items-center justify-between rounded-full border px-4 py-2 text-muted-foreground">
          <span>
            {incident.comments.length > 0
              ? `${incident.comments.length} Update${incident.comments.length !== 1 ? "s" : ""}`
              : "No Updates"}
          </span>
          {incident.comments.length > 0 && (
            <button
              onClick={() => setShowComments((v) => !v)}
              className="rounded-full border bg-background p-1 hover:bg-muted transition-colors -mr-1"
            >
              <ChevronDown
                className={`size-3.5 transition-transform duration-200 ${showComments ? "rotate-180" : ""}`}
              />
            </button>
          )}
        </div>
      </div>

      {showComments && incident.comments.length > 0 && (
        <div className="flex flex-col gap-3 pt-2">
          {incident.comments.map((comment) => (
            <div key={comment.id} className="flex flex-col gap-1 border-b pb-3 last:border-b-0 last:pb-0">
              <div className="flex items-center gap-2 text-xs">
                <span
                  className={`font-semibold uppercase tracking-wide ${stateColor[comment.state] ?? "text-muted-foreground"}`}
                >
                  {comment.state}
                </span>
                <span className="text-muted-foreground">{fmtTs(comment.commentedAt)}</span>
              </div>
              <p className="text-sm">{comment.comment}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
