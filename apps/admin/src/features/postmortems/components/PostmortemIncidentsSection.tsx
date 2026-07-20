import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
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
  const available = (incidents ?? []).filter((i) => !linkedIds.has(i.id));

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
        <div className="flex items-center gap-2">
          <select
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
            className="h-9 flex-1 rounded-lg border bg-background px-3 text-sm"
          >
            <option value="">Select an incident to link…</option>
            {available.map((i) => (
              <option key={i.id} value={i.id}>
                #{i.id} · {i.title}
              </option>
            ))}
          </select>
          <Button size="sm" onClick={handleLink} disabled={!selected || linking}>
            <Link2 size={13} /> Link
          </Button>
        </div>

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
                <button
                  onClick={() => onUnlink(i.incidentId)}
                  disabled={linking}
                  className="flex items-center gap-1 rounded-lg border px-2.5 py-1 text-xs font-medium hover:bg-muted transition-colors disabled:opacity-50"
                >
                  <X size={12} /> Unlink
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

export default PostmortemIncidentsSection;
