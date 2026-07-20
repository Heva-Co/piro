// Human-readable labels for timeline "source" identifiers shared across features. The backend emits
// technical ids (e.g. "incident:Created", "alert:Fired"); this maps them to friendly text. Shared by
// the postmortem timeline and available to the incident timeline.
const SOURCE_LABELS: Record<string, string> = {
  "incident:Created": "Incident created",
  "incident:StatusChanged": "Status changed",
  "incident:CommentPosted": "Comment posted",
  "incident:Acknowledged": "Acknowledged",
  "incident:ServiceAdded": "Service added",
  "incident:ServiceRemoved": "Service removed",
  "incident:MergedTo": "Merged into another incident",
  "incident:MergedFrom": "Absorbed another incident",
  "incident:Published": "Published",
  "incident:Unpublished": "Unpublished",
  "incident:AlertFired": "Alert fired",
  "incident:ImpactChanged": "Impact changed",
  "alert:Fired": "Alert fired",
  "alert:Resolved": "Alert resolved",
};

/**
 * Maps a timeline source id to a human-readable label. Falls back to a de-prefixed, spaced version of
 * the id for anything not enumerated (e.g. "incident:SomethingNew" -> "Something New").
 */
export function timelineSourceLabel(source: string): string {
  if (SOURCE_LABELS[source]) return SOURCE_LABELS[source];
  const bare = source.includes(":") ? source.slice(source.indexOf(":") + 1) : source;
  return bare.replace(/([a-z])([A-Z])/g, "$1 $2");
}
