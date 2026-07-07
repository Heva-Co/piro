import { useState, useRef, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, Search, Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { utilsApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

interface TimezonePickerProps {
  value: string;
  onChange: (value: string) => void;
  className?: string;
}

export function TimezonePicker({ value, onChange, className }: TimezonePickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const selectedRef = useRef<HTMLLIElement>(null);

  const { data: timezones = [] } = useQuery({
    queryKey: QUERY_KEYS.TIMEZONES,
    queryFn: utilsApi.timezones,
    staleTime: Infinity,
  });

  const selected = timezones.find((tz) => tz.id === value);

  const filtered = search.trim()
    ? timezones.filter((tz) => {
        const q = search.toLowerCase();
        return (
          tz.id.toLowerCase().includes(q) ||
          tz.displayName.toLowerCase().includes(q) ||
          tz.offset.toLowerCase().includes(q)
        );
      })
    : timezones;

  useEffect(() => {
    if (!open) return;
    setTimeout(() => {
      searchRef.current?.focus();
      selectedRef.current?.scrollIntoView({ block: "nearest" });
    }, 50);
  }, [open]);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
        setSearch("");
      }
    }
    if (open) document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  return (
    <div ref={containerRef} className={cn("relative w-full", className)}>
      {/* Trigger */}
      <button
        type="button"
        onClick={() => { setOpen((o) => !o); setSearch(""); }}
        className="flex h-10 w-full items-center justify-between gap-1.5 rounded-lg border border-input bg-transparent py-2 pr-2 pl-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        <span className="flex-1 text-left truncate">
          {selected ? (
            <>
              <span className="font-medium">{selected.id}</span>
              <span className="ml-1.5 text-xs text-muted-foreground">{selected.offset}</span>
            </>
          ) : (
            <span className="text-muted-foreground">{value || "Select timezone…"}</span>
          )}
        </span>
        <ChevronDown size={14} className="shrink-0 text-muted-foreground" />
      </button>

      {/* Dropdown */}
      {open && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border bg-popover shadow-md ring-1 ring-foreground/10">
          {/* Search */}
          <div className="flex items-center gap-2 border-b px-3 py-2">
            <Search size={13} className="shrink-0 text-muted-foreground" />
            <input
              ref={searchRef}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search timezone…"
              className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            />
          </div>

          {/* List */}
          <ul className="max-h-60 overflow-y-auto p-1">
            {filtered.length === 0 && (
              <li className="px-3 py-2 text-xs text-muted-foreground">No results</li>
            )}
            {filtered.map((tz) => (
              <li
                key={tz.id}
                ref={tz.id === value ? selectedRef : undefined}
                onClick={() => { onChange(tz.id); setOpen(false); setSearch(""); }}
                className={cn(
                  "flex cursor-default items-center gap-2 rounded-md px-3 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground",
                  tz.id === value && "bg-accent/50"
                )}
              >
                <span className="flex-1 truncate">{tz.id}</span>
                <span className="shrink-0 text-xs text-muted-foreground">{tz.offset}</span>
                {tz.id === value && <Check size={12} className="shrink-0" />}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
