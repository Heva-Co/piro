import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  Blend,
  Activity,
  AlertTriangle,
  CloudAlert,
  ClockAlert,
  CalendarClock,
  Siren,
  Users,
  KeyRound,
  ArrowUpRight,
} from "lucide-react";
import {
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
} from "@/components/ui/command";
import { Skeleton } from "@/components/ui/skeleton";
import { searchApi, type SearchResult, type SearchResultType } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

const TYPE_META: Record<SearchResultType, { label: string; icon: React.ReactNode }> = {
  Service: { label: "Services", icon: <Blend size={15} /> },
  Check: { label: "Checks", icon: <Activity size={15} /> },
  Alert: { label: "Alerts", icon: <AlertTriangle size={15} /> },
  Incident: { label: "Incidents", icon: <CloudAlert size={15} /> },
  Maintenance: { label: "Maintenances", icon: <ClockAlert size={15} /> },
  OnCallSchedule: { label: "On-Call Schedules", icon: <CalendarClock size={15} /> },
  EscalationPolicy: { label: "Escalation Policies", icon: <Siren size={15} /> },
  User: { label: "Users", icon: <Users size={15} /> },
  ApiKey: { label: "API Keys", icon: <KeyRound size={15} /> },
};

const TYPE_ORDER: SearchResultType[] = [
  "Service", "Check", "Alert", "Incident", "Maintenance",
  "OnCallSchedule", "EscalationPolicy", "User", "ApiKey",
];

interface GlobalSearchDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const DEBOUNCE_MS = 300;

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

export function GlobalSearchDialog({ open, onOpenChange }: GlobalSearchDialogProps) {
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const debouncedQuery = useDebouncedValue(query, DEBOUNCE_MS);

  useEffect(() => {
    if (!open) setQuery("");
  }, [open]);

  const canSearch = debouncedQuery.trim().length >= 2;
  const isDebouncing = query.trim().length >= 2 && query !== debouncedQuery;

  const { data: results = [], isFetching } = useQuery({
    queryKey: QUERY_KEYS.SEARCH(debouncedQuery),
    queryFn: () => searchApi.search(debouncedQuery),
    enabled: canSearch,
    placeholderData: (prev) => prev,
  });

  const isLoading = isDebouncing || (canSearch && isFetching);

  const grouped = TYPE_ORDER
    .map((type) => ({ type, items: results.filter((r) => r.type === type) }))
    .filter((g) => g.items.length > 0);

  function go(url: string) {
    onOpenChange(false);
    navigate(url);
  }

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange} title="Search" description="Search across services, checks, incidents, and more">
      <CommandInput
        value={query}
        onValueChange={setQuery}
        placeholder="Search services, checks, incidents..."
      />
      <CommandList>
        {query.trim().length < 2 && (
          <CommandEmpty>Type at least 2 characters to search.</CommandEmpty>
        )}
        {isLoading && <SearchSkeleton />}
        {!isLoading && canSearch && results.length === 0 && (
          <CommandEmpty>No results found.</CommandEmpty>
        )}
        {!isLoading && grouped.map(({ type, items }) => (
          <CommandGroup key={type} heading={TYPE_META[type].label}>
            {items.map((r, i) => (
              <SearchResultItem key={`${type}-${i}`} result={r} onNavigate={go} />
            ))}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  );
}

function SearchSkeleton() {
  return (
    <div className="p-2 flex flex-col gap-1">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="flex items-center gap-2 px-2 py-2">
          <Skeleton className="size-4 shrink-0 rounded" />
          <Skeleton className="h-3 flex-1" style={{ maxWidth: `${60 - i * 8}%` }} />
        </div>
      ))}
    </div>
  );
}

function SearchResultItem({ result, onNavigate }: { result: SearchResult; onNavigate: (url: string) => void }) {
  return (
    <CommandItem value={`${result.type}-${result.title}-${result.url}`} onSelect={() => onNavigate(result.url)}>
      {TYPE_META[result.type].icon}
      <div className="flex flex-col min-w-0 flex-1">
        <span className="truncate">{result.title}</span>
        {result.subtitle && (
          <span className="text-xs text-muted-foreground truncate">{result.subtitle}</span>
        )}
      </div>
      {result.incidentId && result.incidentUrl && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onNavigate(result.incidentUrl!);
          }}
          className="flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs text-muted-foreground hover:text-foreground hover:bg-muted transition-colors shrink-0"
          title="Go to linked incident"
        >
          <ArrowUpRight size={12} />
          Incident
        </button>
      )}
    </CommandItem>
  );
}
