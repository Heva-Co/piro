import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { incidentsApi } from "@/lib/actions/incidents";
import { useFormattedDate } from "@/hooks/useFormattedDate";
import type { Postmortem } from "@/lib/actions/postmortems";

interface Props {
  postmortem: Postmortem;
  linking: boolean;
  onLink: (incidentId: number) => void;
  onUnlink: (incidentId: number) => void;
}

// The "data sources" of a postmortem: the incidents it reviews. The picker offers all incidents not
// already linked; the report's timeline is derived from whatever is linked here.
function PostmortemIncidentsSection(props: Props) {
  const { postmortem, linking, onLink, onUnlink } = props;
  const [selected, setSelected] = useState("");
  const { formatTimestamp } = useFormattedDate();

  const { data: incidents } = useQuery({
    queryKey: ["incidents", "all"],
    queryFn: () => incidentsApi.list("all"),
  });

  const linkedIds = useMemo(
    () => new Set(postmortem.incidents.map((i) => i.incidentId)),
    [postmortem.incidents]
  );
  // Only resolved/merged incidents can be linked; a postmortem reviews incidents that are over
  // (matches the backend guard). isResolved is true for both Resolved and Merged.
  const available = (incidents ?? []).filter((i) => !linkedIds.has(i.id) && i.isResolved);
  const hasUnresolved = (incidents ?? []).some((i) => !linkedIds.has(i.id) && !i.isResolved);

  function handleLink() {
    if (!selected) return;
    onLink(Number(selected));
    setSelected("");
  }

  return (
    <div className="rounded-xl border bg-card">
      <div className="border-b px-5 py-3">
        <h2 className="text-sm font-semibold">Referenced incidents</h2>
        <p className="text-xs text-muted-foreground mt-0.5">
          The incidents this review covers. The timeline below is derived from them.
        </p>
      </div>

      <div className="flex flex-col gap-4 p-5">
        {available.length === 0 ? (
          <p className="rounded-lg border border-dashed bg-muted/30 px-4 py-3 text-sm text-muted-foreground">
            {hasUnresolved
              ? "No resolved incidents to link. In-progress incidents can be linked once they're resolved."
              : "No resolved incidents available to link."}
          </p>
        ) : (
          <>
            <div className="flex items-center gap-2">
              <Select value={selected} onValueChange={(v) => setSelected(v ?? "")}>
                <SelectTrigger className="flex-1">
                  <SelectValue placeholder="Select an incident to link…">
                    {(v: string | null) => {
                      const inc = available.find((i) => String(i.id) === v);
                      return inc ? `#${inc.id} · ${inc.title}` : "Select an incident to link…";
                    }}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {available.map((i) => (
                    <SelectItem key={i.id} value={String(i.id)}>
                      #{i.id} · {i.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button size="sm" onClick={handleLink} disabled={!selected || linking}>
                <Link2 size={13} /> Link
              </Button>
            </div>

            {hasUnresolved && (
              <p className="text-xs text-muted-foreground">
                Only resolved incidents can be linked. In-progress incidents are hidden until they're resolved.
              </p>
            )}
          </>
        )}

        {postmortem.incidents.length === 0 ? (
          <p className="text-sm text-muted-foreground">No incidents linked yet.</p>
        ) : (
          <ul className="flex flex-col divide-y">
            {postmortem.incidents.map((i) => (
              <li key={i.incidentId} className="flex items-center justify-between py-2.5">
                <div className="flex flex-col">
                  <span className="text-sm font-medium">
                    #{i.incidentId} · {i.title}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {i.status} · started {formatTimestamp(i.startDateTime)}
                  </span>
                </div>
                <Button variant="outline" size="xs" onClick={() => onUnlink(i.incidentId)} disabled={linking}>
                  <X size={12} /> Unlink
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

export default PostmortemIncidentsSection;
