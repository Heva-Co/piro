import { useMemo, useState } from "react";
import { DatePicker } from "@/components/DatePicker";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  DAY_LABELS,
  DAY_NAMES,
  buildPresetOptions,
  buildRrule,
  formatRRuleHuman,
  type CustomRecurrence,
  type EndsType,
  type FreqUnit,
} from "./rrule-builder";

const CUSTOM_VALUE = "__custom__";

interface Props {
  /** Current RRULE string (without the leading "RRULE:" prefix). */
  value: string;
  onChange: (rrule: string) => void;
  /** Anchor date used to derive the "Weekly on X" preset label and month/date math. Defaults to now. */
  startDate?: Date;
  /** When set, also renders a duration (hours/minutes) field and reports changes in seconds. */
  showDuration?: boolean;
  durationSeconds?: number;
  onDurationChange?: (seconds: number) => void;
  className?: string;
}

export function RRuleEditor({
  value,
  onChange,
  startDate,
  showDuration = false,
  durationSeconds = 0,
  onDurationChange,
  className,
}: Props) {
  const anchor = startDate ?? new Date();
  const presetOptions = useMemo(() => [
    ...buildPresetOptions(anchor),
    { label: "Custom…", value: CUSTOM_VALUE },
  ], [anchor]);

  const matchedPreset = presetOptions.find((p) => p.value !== CUSTOM_VALUE && p.value === value);
  const [selectedPreset, setSelectedPreset] = useState<string>(matchedPreset?.value ?? CUSTOM_VALUE);
  const isCustom = selectedPreset === CUSTOM_VALUE;

  const [custom, setCustom] = useState<CustomRecurrence>({
    interval: 1,
    unit: "week",
    bydays: [],
    ends: "never",
    endsDate: "",
    endsCount: 1,
  });

  function selectPreset(preset: string) {
    setSelectedPreset(preset);
    if (preset !== CUSTOM_VALUE) onChange(preset);
    else onChange(buildRrule(custom));
  }

  function updateCustom(patch: Partial<CustomRecurrence>) {
    const next = { ...custom, ...patch };
    setCustom(next);
    onChange(buildRrule(next));
  }

  const durationHours = Math.floor(durationSeconds / 3600);
  const durationMinutes = Math.floor((durationSeconds % 3600) / 60);

  function updateDuration(hours: number, minutes: number) {
    onDurationChange?.((hours * 60 + minutes) * 60);
  }

  return (
    <div className={className}>
      <Select value={selectedPreset} onValueChange={(v) => v && selectPreset(v)}>
        <SelectTrigger className="w-full">
          <SelectValue>{presetOptions.find((p) => p.value === selectedPreset)?.label}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          {presetOptions.map((p) => (
            <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>

      {isCustom && (
        <div className="mt-3 rounded-lg border border-border bg-background p-3 flex flex-col gap-3">
          <div className="flex items-center gap-2">
            <span className="text-sm text-foreground shrink-0">Repeat every</span>
            <Input
              type="number"
              min={1}
              max={99}
              value={custom.interval}
              onChange={(e) => updateCustom({ interval: Math.max(1, Math.min(99, Number(e.target.value))) })}
              className="w-16 text-center"
            />
            <Select value={custom.unit} onValueChange={(v) => v && updateCustom({ unit: v as FreqUnit })}>
              <SelectTrigger className="w-28">
                <SelectValue>{custom.unit}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="day">day</SelectItem>
                <SelectItem value="week">week</SelectItem>
                <SelectItem value="month">month</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {custom.unit === "week" && (
            <div>
              <span className="block text-xs text-muted-foreground mb-1.5">Repeat on</span>
              <div className="flex gap-1">
                {DAY_LABELS.map((label, i) => {
                  const code = DAY_NAMES[i];
                  const active = custom.bydays.includes(code);
                  return (
                    <button
                      key={code}
                      type="button"
                      onClick={() => updateCustom({
                        bydays: active ? custom.bydays.filter((d) => d !== code) : [...custom.bydays, code],
                      })}
                      className={`w-8 h-8 rounded-full text-xs font-medium transition-colors ${
                        active
                          ? "bg-foreground text-background"
                          : "border border-border text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      {label}
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          <div>
            <span className="block text-xs text-muted-foreground mb-1.5">Ends</span>
            <div className="flex flex-col gap-1.5">
              <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                <input type="radio" name="rrule-ends" checked={custom.ends === "never"}
                  onChange={() => updateCustom({ ends: "never" as EndsType })} className="accent-foreground" />
                Never
              </label>
              <div className="flex items-center gap-2">
                <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                  <input type="radio" name="rrule-ends" checked={custom.ends === "on"}
                    onChange={() => updateCustom({ ends: "on" as EndsType })} className="accent-foreground" />
                  On
                </label>
                <div className={custom.ends !== "on" ? "opacity-40 pointer-events-none" : ""}>
                  <DatePicker
                    value={custom.endsDate}
                    onChange={(d) => updateCustom({ ends: "on" as EndsType, endsDate: d })}
                    placeholder="Pick end date"
                  />
                </div>
              </div>
              <label className="flex items-center gap-2 text-sm text-foreground cursor-pointer">
                <input type="radio" name="rrule-ends" checked={custom.ends === "after"}
                  onChange={() => updateCustom({ ends: "after" as EndsType })} className="accent-foreground" />
                After
                <Input
                  type="number"
                  min={1}
                  value={custom.endsCount}
                  onChange={(e) => updateCustom({ ends: "after" as EndsType, endsCount: Math.max(1, Number(e.target.value)) })}
                  disabled={custom.ends !== "after"}
                  className="w-16 text-center"
                />
                <span className="text-muted-foreground">occurrences</span>
              </label>
            </div>
          </div>
        </div>
      )}

      {showDuration && (
        <div className="flex flex-col gap-1.5 mt-3">
          <label className="text-sm font-semibold">Duration</label>
          <div className="flex items-center gap-2">
            <Input type="number" min={0} value={durationHours}
              onChange={(e) => updateDuration(Number(e.target.value), durationMinutes)}
              className="w-16" />
            <span className="text-sm text-muted-foreground">h</span>
            <Input type="number" min={0} max={59} value={durationMinutes}
              onChange={(e) => updateDuration(durationHours, Number(e.target.value))}
              className="w-16" />
            <span className="text-sm text-muted-foreground">m</span>
          </div>
        </div>
      )}

      <p className="text-xs text-muted-foreground mt-2 capitalize">{formatRRuleHuman(value)}</p>
    </div>
  );
}
