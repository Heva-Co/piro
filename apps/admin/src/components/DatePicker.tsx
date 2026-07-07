/**
 * DatePicker — calendar popover, date only. No time input.
 * Value/onChange use "YYYY-MM-DD" strings.
 */
import { useState, useRef, useEffect } from "react";
import { DayPicker, useDayPicker } from "react-day-picker";
import { ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { format, parse, isValid } from "date-fns";
import "react-day-picker/style.css";

interface Props {
  value: string; // "YYYY-MM-DD" or ""
  onChange: (v: string) => void; // emits "YYYY-MM-DD"
  placeholder?: string;
  className?: string;
}

function MonthCaption({ calendarMonth }: { calendarMonth: { date: Date } }) {
  const { goToMonth, nextMonth, previousMonth } = useDayPicker();
  return (
    <div className="flex items-center justify-between px-1 py-1 mb-1">
      <button
        type="button"
        disabled={!previousMonth}
        onClick={() => previousMonth && goToMonth(previousMonth)}
        className="flex items-center justify-center w-7 h-7 rounded-md hover:bg-muted text-muted-foreground disabled:opacity-30"
      >
        <ChevronLeft size={14} />
      </button>
      <span className="text-sm font-semibold text-foreground">
        {format(calendarMonth.date, "MMMM yyyy")}
      </span>
      <button
        type="button"
        disabled={!nextMonth}
        onClick={() => nextMonth && goToMonth(nextMonth)}
        className="flex items-center justify-center w-7 h-7 rounded-md hover:bg-muted text-muted-foreground disabled:opacity-30"
      >
        <ChevronRight size={14} />
      </button>
    </div>
  );
}

export function DatePicker({ value, onChange, placeholder = "Pick a date", className }: Props) {
  const parsed = value ? parse(value, "yyyy-MM-dd", new Date()) : undefined;
  const selectedDate = parsed && isValid(parsed) ? parsed : undefined;
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    if (open) document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  function handleDaySelect(day: Date | undefined) {
    if (!day) return;
    onChange(format(day, "yyyy-MM-dd"));
    setOpen(false);
  }

  const displayLabel = selectedDate ? format(selectedDate, "MMM d, yyyy") : "";

  return (
    <div ref={ref} className={`relative ${className ?? "inline-block"}`}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="w-full flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-2 text-sm text-left hover:bg-muted focus:outline-none focus:ring-2 focus:ring-indigo-500"
      >
        <span className={displayLabel ? "text-foreground" : "text-muted-foreground"}>
          {displayLabel || placeholder}
        </span>
        <ChevronDown size={14} className="text-muted-foreground shrink-0" />
      </button>

      {open && (
        <div className="absolute z-50 mt-1 rounded-xl border border-border bg-card shadow-lg p-3 min-w-[280px]">
          <DayPicker
            mode="single"
            selected={selectedDate}
            onSelect={handleDaySelect}
            showOutsideDays
            components={{ MonthCaption }}
            classNames={{
              root: "!font-sans",
              month_caption: "hidden",
              nav: "hidden",
              weekdays: "flex",
              weekday: "w-9 text-center text-xs text-muted-foreground font-medium py-1",
              weeks: "flex flex-col gap-0.5",
              week: "flex",
              day: "w-9 h-9 text-center text-sm",
              day_button: "w-9 h-9 rounded-full flex items-center justify-center text-sm hover:bg-muted transition-colors",
              selected: "[&>button]:bg-indigo-600 [&>button]:text-white [&>button]:hover:bg-indigo-700",
              today: "[&>button]:font-bold [&>button]:text-indigo-600",
              outside: "opacity-40",
              disabled: "opacity-30 cursor-not-allowed",
            }}
          />
        </div>
      )}
    </div>
  );
}
