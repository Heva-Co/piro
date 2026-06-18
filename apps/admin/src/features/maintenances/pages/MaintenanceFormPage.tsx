import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { maintenancesApi, servicesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const WEEKDAYS = [
  { label: "Mo", value: "MO" },
  { label: "Tu", value: "TU" },
  { label: "We", value: "WE" },
  { label: "Th", value: "TH" },
  { label: "Fr", value: "FR" },
  { label: "Sa", value: "SA" },
  { label: "Su", value: "SU" },
];

function pad(n: number) { return String(n).padStart(2, "0"); }
function toLocalDT(d: Date) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function MaintenanceFormPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [scheduleType, setScheduleType] = useState<"one-time" | "recurring">("one-time");
  const [startDateTime, setStartDateTime] = useState(toLocalDT(new Date()));
  const [durationHours, setDurationHours] = useState(1);
  const [durationMinutes, setDurationMinutes] = useState(0);
  const [frequency, setFrequency] = useState("WEEKLY");
  const [interval, setIntervalVal] = useState(1);
  const [weekdays, setWeekdays] = useState<Set<string>>(new Set(["MO"]));
  const [isGlobal, setIsGlobal] = useState(false);
  const [selectedServices, setSelectedServices] = useState<Set<string>>(new Set());
  const [error, setError] = useState("");

  const { data: services = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  function buildRrule() {
    if (scheduleType === "one-time") {
      return "FREQ=MINUTELY;COUNT=1";
    }
    let rule = `FREQ=${frequency};INTERVAL=${interval}`;
    if (frequency === "WEEKLY" && weekdays.size > 0) {
      rule += `;BYDAY=${[...weekdays].join(",")}`;
    }
    return rule;
  }

  function buildEndTime() {
    const start = new Date(startDateTime);
    const end = new Date(start.getTime() + (durationHours * 60 + durationMinutes) * 60000);
    return end.toISOString();
  }

  const createMutation = useMutation({
    mutationFn: () =>
      maintenancesApi.create({
        name: title,
        description,
        scheduledStart: new Date(startDateTime).toISOString(),
        scheduledEnd: buildEndTime(),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
      navigate(ROUTES.MAINTENANCES.LIST);
    },
    onError: () => setError("Failed to create maintenance."),
  });

  function toggleWeekday(d: string) {
    setWeekdays((prev) => {
      const next = new Set(prev);
      if (next.has(d)) next.delete(d); else next.add(d);
      return next;
    });
  }

  const rrulePreview = buildRrule();

  return (
    <AdminLayout title="New Maintenance">
      <div className="max-w-xl">
        <form
          onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}
          className="flex flex-col gap-5"
        >
          {error && (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <AlertCircle size={16} /> {error}
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Title *</label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Description</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          {/* Schedule type */}
          <div className="flex flex-col gap-2">
            <label className="text-sm font-medium">Schedule Type</label>
            <div className="flex gap-4">
              {(["one-time", "recurring"] as const).map((t) => (
                <label key={t} className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    value={t}
                    checked={scheduleType === t}
                    onChange={() => setScheduleType(t)}
                    className="text-indigo-600"
                  />
                  <span className="text-sm capitalize">{t.replace("-", " ")}</span>
                </label>
              ))}
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Start Date/Time</label>
            <input
              type="datetime-local"
              value={startDateTime}
              onChange={(e) => setStartDateTime(e.target.value)}
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="flex gap-3">
            <div className="flex flex-col gap-1.5 flex-1">
              <label className="text-sm font-medium">Duration Hours</label>
              <input
                type="number"
                min={0}
                value={durationHours}
                onChange={(e) => setDurationHours(Number(e.target.value))}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div className="flex flex-col gap-1.5 flex-1">
              <label className="text-sm font-medium">Duration Minutes</label>
              <input
                type="number"
                min={0}
                max={59}
                value={durationMinutes}
                onChange={(e) => setDurationMinutes(Number(e.target.value))}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>

          {scheduleType === "recurring" && (
            <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 flex flex-col gap-4">
              <h3 className="text-sm font-semibold text-gray-700">Recurrence</h3>

              <div className="flex gap-3">
                <div className="flex flex-col gap-1.5 flex-1">
                  <label className="text-sm font-medium">Frequency</label>
                  <select
                    value={frequency}
                    onChange={(e) => setFrequency(e.target.value)}
                    className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm"
                  >
                    <option value="DAILY">Daily</option>
                    <option value="WEEKLY">Weekly</option>
                    <option value="MONTHLY">Monthly</option>
                  </select>
                </div>
                <div className="flex flex-col gap-1.5 w-28">
                  <label className="text-sm font-medium">Every (N)</label>
                  <input
                    type="number"
                    min={1}
                    value={interval}
                    onChange={(e) => setIntervalVal(Number(e.target.value))}
                    className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm"
                  />
                </div>
              </div>

              {frequency === "WEEKLY" && (
                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-medium">Days</label>
                  <div className="flex gap-1.5">
                    {WEEKDAYS.map((d) => (
                      <button
                        key={d.value}
                        type="button"
                        onClick={() => toggleWeekday(d.value)}
                        className={`rounded-md w-9 h-9 text-xs font-medium border transition-colors ${
                          weekdays.has(d.value)
                            ? "bg-indigo-600 border-indigo-600 text-white"
                            : "bg-white border-gray-300 text-gray-600 hover:bg-gray-50"
                        }`}
                      >
                        {d.label}
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* RRULE preview */}
          <div className="rounded-md bg-gray-50 border border-gray-200 px-3 py-2">
            <span className="text-xs text-gray-500 font-medium">RRULE: </span>
            <code className="text-xs font-mono text-gray-700">{rrulePreview}</code>
          </div>

          <label className="flex items-center gap-2 cursor-pointer">
            <div className="relative">
              <input type="checkbox" checked={isGlobal} onChange={(e) => setIsGlobal(e.target.checked)} className="sr-only peer" />
              <div className="w-9 h-5 rounded-full bg-gray-200 peer-checked:bg-indigo-600 transition-colors" />
              <div className="absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform peer-checked:translate-x-4" />
            </div>
            <span className="text-sm font-medium">Global maintenance</span>
          </label>

          {!isGlobal && services.length > 0 && (
            <div className="flex flex-col gap-2">
              <label className="text-sm font-medium">Affected Services</label>
              <div className="rounded-lg border border-gray-200 divide-y divide-gray-100">
                {services.map((svc) => (
                  <div key={svc.slug} className="flex items-center gap-3 px-3 py-2">
                    <input
                      type="checkbox"
                      id={`svc-${svc.slug}`}
                      checked={selectedServices.has(svc.slug)}
                      onChange={() => {
                        setSelectedServices((prev) => {
                          const next = new Set(prev);
                          if (next.has(svc.slug)) next.delete(svc.slug); else next.add(svc.slug);
                          return next;
                        });
                      }}
                      className="rounded border-gray-300"
                    />
                    <label htmlFor={`svc-${svc.slug}`} className="text-sm cursor-pointer">{svc.name}</label>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="flex gap-3">
            <button
              type="submit"
              disabled={createMutation.isPending}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {createMutation.isPending ? "Creating…" : "Create Maintenance"}
            </button>
            <button
              type="button"
              onClick={() => navigate(ROUTES.MAINTENANCES.LIST)}
              className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </AdminLayout>
  );
}
