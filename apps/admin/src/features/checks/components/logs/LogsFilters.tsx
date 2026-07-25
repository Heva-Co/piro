import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

const PAGE_SIZE_OPTIONS = [20, 50, 100, 200];

type StatusFilter = "" | "UP" | "DOWN";

const STATUS_LABELS: Record<StatusFilter, string> = { "": "All", UP: "Up", DOWN: "Down" };

interface Props {
  statusFilter: StatusFilter;
  region: string;
  limit: number;
  regions: string[];
  onStatusChange: (status: StatusFilter) => void;
  onRegionChange: (region: string) => void;
  onLimitChange: (limit: number) => void;
}

// base-ui Select uses "" as the "unset" sentinel, so the "All" options carry a real value we map back.
const ALL_REGIONS = "__all__";

function LogsFilters(props: Props) {
  const { statusFilter, region, limit, regions, onStatusChange, onRegionChange, onLimitChange } = props;

  return (
    <div className="flex items-center gap-3 flex-wrap">
      <div className="flex items-center gap-2">
        <Label className="text-muted-foreground">Status</Label>
        <Select value={statusFilter} onValueChange={(v) => onStatusChange((v ?? "") as StatusFilter)}>
          <SelectTrigger size="sm" className="min-w-28">
            <SelectValue>{(v: string) => STATUS_LABELS[(v ?? "") as StatusFilter]}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">All</SelectItem>
            <SelectItem value="UP">Up</SelectItem>
            <SelectItem value="DOWN">Down</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="flex items-center gap-2">
        <Label className="text-muted-foreground">Region</Label>
        <Select
          value={region || ALL_REGIONS}
          onValueChange={(v) => onRegionChange(v === ALL_REGIONS ? "" : (v ?? ""))}
        >
          <SelectTrigger size="sm" className="min-w-36">
            <SelectValue>{(v: string) => (v === ALL_REGIONS ? "All regions" : v)}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_REGIONS}>All regions</SelectItem>
            {regions.map((r) => (
              <SelectItem key={r} value={r}>{r}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="flex items-center gap-2 ml-auto">
        <Label className="text-muted-foreground">Load last</Label>
        <Select value={String(limit)} onValueChange={(v) => onLimitChange(Number(v))}>
          <SelectTrigger size="sm" className="min-w-32">
            <SelectValue>{(v: string) => `${v} entries`}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {PAGE_SIZE_OPTIONS.map((n) => (
              <SelectItem key={n} value={String(n)}>{n} entries</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}

export default LogsFilters;
