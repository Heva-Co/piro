/**
 * DateTimePicker — calendar popover + time input, similar to MUI DateTimePicker.
 * Value/onChange use "YYYY-MM-DDTHH:MM" strings (same as DateTimeInput).
 */
import { useState, useRef, useEffect } from "react";
import { DayPicker, useDayPicker } from "react-day-picker";
import { ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { format, parse, isValid } from "date-fns";
import "react-day-picker/style.css";

interface Props {
  value: string; // "YYYY-MM-DDTHH:MM" or ""
  onChange: (v: string) => void;
  placeholder?: string;
  className?: string;
}

function splitValue(v: string): { date: Date | undefined; time: string } {
  if (!v) return { date: undefined, time: "00:00" };
  const [datePart, timePart = "00:00"] = v.split("T");
  const parsed = parse(datePart, "yyyy-MM-dd", new Date());
  return { date: isValid(parsed) ? parsed : undefined, time: timePart.slice(0, 5) };
}

/** Custom caption: ‹ Month Year › all in one row */
function MonthCaption({ calendarMonth }: { calendarMonth: { date: Date } }) {
  const { goToMonth, nextMonth, previousMonth } = useDayPicker();
  return (
    <div className="flex items-center justify-between px-1 py-1 mb-1">
      <button
        type="button"
        disabled={!previousMonth}
        onClick={() => previousMonth && goToMonth(previousMonth)}
        className="flex items-center justify-center w-7 h-7 rounded-md hover:bg-gray-100 text-gray-600 disabled:opacity-30"
      >
        <ChevronLeft size={14} />
      </button>
      <span className="text-sm font-semibold text-gray-900">
        {format(calendarMonth.date, "MMMM yyyy")}
      </span>
      <button
        type="button"
        disabled={!nextMonth}
        onClick={() => nextMonth && goToMonth(nextMonth)}
        className="flex items-center justify-center w-7 h-7 rounded-md hover:bg-gray-100 text-gray-600 disabled:opacity-30"
      >
        <ChevronRight size={14} />
      </button>
    </div>
  );
}

export function DateTimePicker({ value, onChange, placeholder = "Pick a date", className }: Props) {
  const { date: selectedDate, time } = splitValue(value);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    if (open) document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  function handleDaySelect(day: Date | undefined) {
    if (!day) return;
    const datePart = format(day, "yyyy-MM-dd");
    onChange(`${datePart}T${time || "00:00"}`);
  }

  function handleTimeChange(e: React.ChangeEvent<HTMLInputElement>) {
    const newTime = e.target.value;
    const datePart = selectedDate ? format(selectedDate, "yyyy-MM-dd") : format(new Date(), "yyyy-MM-dd");
    onChange(`${datePart}T${newTime}`);
  }

  const displayLabel = selectedDate
    ? `${format(selectedDate, "MMM d, yyyy")}  ${time}`
    : "";

  return (
    <div ref={ref} className={`relative inline-block ${className ?? ""}`}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex items-center justify-between gap-2 min-w-52 rounded-lg border border-gray-300 bg-gray-50 px-3 py-2 text-sm text-left hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-indigo-500"
      >
        <span className={displayLabel ? "text-gray-900" : "text-gray-400"}>
          {displayLabel || placeholder}
        </span>
        <ChevronDown size={14} className="text-gray-500 shrink-0" />
      </button>

      {open && (
        <div className="absolute z-50 mt-1 rounded-xl border border-gray-200 bg-white shadow-lg p-3 min-w-[280px]">
          <DayPicker
            mode="single"
            selected={selectedDate}
            onSelect={handleDaySelect}
            showOutsideDays
            components={{ MonthCaption }}
            classNames={{
              root: "!font-sans",
              month_caption: "hidden", // rendered by custom MonthCaption
              nav: "hidden",
              weekdays: "flex",
              weekday: "w-9 text-center text-xs text-gray-400 font-medium py-1",
              weeks: "flex flex-col gap-0.5",
              week: "flex",
              day: "w-9 h-9 text-center text-sm",
              day_button: "w-9 h-9 rounded-full flex items-center justify-center text-sm hover:bg-gray-100 transition-colors",
              selected: "[&>button]:bg-indigo-600 [&>button]:text-white [&>button]:hover:bg-indigo-700",
              today: "[&>button]:font-bold [&>button]:text-indigo-600",
              outside: "opacity-40",
              disabled: "opacity-30 cursor-not-allowed",
            }}
          />

          {/* Time picker */}
          <div className="border-t border-gray-100 mt-2 pt-3 flex items-center gap-2 px-1">
            <span className="text-xs text-gray-500 font-medium w-10">Time</span>
            <input
              type="time"
              value={time}
              onChange={handleTimeChange}
              className="flex-1 rounded-md border border-gray-300 bg-white px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700"
            >
              Done
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
