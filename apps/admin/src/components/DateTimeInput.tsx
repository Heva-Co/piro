/**
 * DateTimeInput — replaces the useless native datetime-local picker.
 * Renders two plain text inputs (date: YYYY-MM-DD, time: HH:MM) side by side.
 * Value/onChange use ISO-like "YYYY-MM-DDTHH:MM" strings (compatible with datetime-local).
 */

const inp = "rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring";

interface Props {
  value: string;          // "YYYY-MM-DDTHH:MM" or ""
  onChange: (v: string) => void;
  className?: string;
}

function split(v: string): [string, string] {
  if (!v) return ["", ""];
  const [date = "", time = ""] = v.split("T");
  return [date, time.slice(0, 5)]; // keep HH:MM only
}

export function DateTimeInput({ value, onChange, className }: Props) {
  const [date, time] = split(value);

  function handleDate(d: string) {
    onChange(`${d}T${time || "00:00"}`);
  }

  function handleTime(t: string) {
    onChange(`${date || new Date().toISOString().slice(0, 10)}T${t}`);
  }

  return (
    <div className={`flex gap-2 ${className ?? ""}`}>
      <input
        type="text"
        value={date}
        onChange={(e) => handleDate(e.target.value)}
        placeholder="YYYY-MM-DD"
        pattern="\d{4}-\d{2}-\d{2}"
        className={`${inp} w-36`}
      />
      <input
        type="text"
        value={time}
        onChange={(e) => handleTime(e.target.value)}
        placeholder="HH:MM"
        pattern="\d{2}:\d{2}"
        className={`${inp} w-24`}
      />
    </div>
  );
}
