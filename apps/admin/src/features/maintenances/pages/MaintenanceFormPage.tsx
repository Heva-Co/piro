import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Calendar, RefreshCw, ChevronRight } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { DateTimePicker } from "@/components/DateTimePicker";
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
  const [allServices, setAllServices] = useState(false);
  const [selectedServices, setSelectedServices] = useState<Set<string>>(new Set());
  const [error, setError] = useState("");

  const { data: services = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  function buildRrule() {
    if (scheduleType === "one-time") return "FREQ=MINUTELY;COUNT=1";
    let rule = `FREQ=${frequency};INTERVAL=${interval}`;
    if (frequency === "WEEKLY" && weekdays.size > 0)
      rule += `;BYDAY=${[...weekdays].join(",")}`;
    return rule;
  }

  function buildEndTime() {
    const start = new Date(startDateTime);
    return new Date(start.getTime() + (durationHours * 60 + durationMinutes) * 60000).toISOString();
  }

  const totalSeconds = (durationHours * 60 + durationMinutes) * 60;

  const createMutation = useMutation({
    mutationFn: () =>
      maintenancesApi.create({
        name: title,
        description,
        scheduledStart: new Date(startDateTime).toISOString(),
        scheduledEnd: buildEndTime(),
        isGlobal,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
      navigate(ROUTES.MAINTENANCES.LIST);
    },
    onError: () => setError("Failed to create maintenance."),
  });

  function toggleWeekday(d: string) {
    setWeekdays(prev => {
      const next = new Set(prev);
      if (next.has(d)) next.delete(d); else next.add(d);
      return next;
    });
  }

  function toggleService(slug: string) {
    setSelectedServices(prev => {
      const next = new Set(prev);
      if (next.has(slug)) next.delete(slug); else next.add(slug);
      return next;
    });
  }

  function handleAllToggle(checked: boolean) {
    setAllServices(checked);
    if (checked) setSelectedServices(new Set(services.map(s => s.slug)));
    else setSelectedServices(new Set());
  }

  const rrulePreview = buildRrule();

  return (
    <AdminLayout title="New Maintenance">
      {/* Breadcrumb */}
      <div className="flex items-center gap-1.5 text-sm text-gray-500 mb-5">
        <button onClick={() => navigate(ROUTES.MAINTENANCES.LIST)} className="hover:text-gray-700">Maintenances</button>
        <ChevronRight size={14} />
        <span className="text-gray-900 font-medium">New Maintenance</span>
      </div>

      <div className="max-w-2xl">
        <div className="rounded-2xl border border-border bg-card p-6 mb-4">
          <h1 className="text-xl font-bold text-gray-900 mb-0.5">Create New Maintenance</h1>
          <p className="text-sm text-gray-500 mb-6">Schedule a new maintenance window using iCalendar RRULE format</p>

          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col gap-5">
            {error && (
              <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                <AlertCircle size={15} /> {error}
              </div>
            )}

            {/* Schedule Type */}
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold text-gray-900">Schedule Type *</label>
              <div className="flex gap-5">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="radio" value="one-time" checked={scheduleType === "one-time"}
                    onChange={() => setScheduleType("one-time")} className="accent-gray-900" />
                  <Calendar size={15} className="text-gray-500" />
                  <span className="text-sm text-gray-700">One-Time</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="radio" value="recurring" checked={scheduleType === "recurring"}
                    onChange={() => setScheduleType("recurring")} className="accent-gray-900" />
                  <RefreshCw size={15} className="text-gray-500" />
                  <span className="text-sm text-gray-700">Recurring</span>
                </label>
              </div>
            </div>

            {/* Title */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-gray-900">Title *</label>
              <input type="text" value={title} onChange={e => setTitle(e.target.value)} required
                placeholder="Scheduled maintenance window"
                className="rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
            </div>

            {/* Description */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-gray-900">Description</label>
              <textarea value={description} onChange={e => setDescription(e.target.value)} rows={3}
                placeholder="Details about the maintenance…"
                className="rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400 resize-none" />
            </div>

            {/* Global maintenance toggle */}
            <div className="rounded-xl border border-gray-200 p-4 flex items-center justify-between">
              <div>
                <p className="text-sm font-semibold text-gray-900">Global Maintenance</p>
                <p className="text-xs text-gray-500 mt-0.5">When enabled, this maintenance will be visible on all status pages</p>
              </div>
              <button type="button" onClick={() => setIsGlobal(v => !v)}
                className={`relative w-10 h-6 rounded-full transition-colors ${isGlobal ? "bg-gray-900" : "bg-gray-200"}`}>
                <span className={`absolute top-1 w-4 h-4 rounded-full bg-white shadow transition-transform ${isGlobal ? "translate-x-5" : "translate-x-1"}`} />
              </button>
            </div>

            {/* Start Date/Time */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-gray-900">Start Date/Time *</label>
              <DateTimePicker value={startDateTime} onChange={setStartDateTime} />
            </div>

            {/* Duration */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-gray-900">Duration *</label>
              <div className="flex items-center gap-3">
                <input type="number" min={0} value={durationHours} onChange={e => setDurationHours(Number(e.target.value))}
                  className="w-20 rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
                <span className="text-sm text-gray-500">hours</span>
                <input type="number" min={0} max={59} value={durationMinutes} onChange={e => setDurationMinutes(Number(e.target.value))}
                  className="w-20 rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
                <span className="text-sm text-gray-500">minutes</span>
              </div>
              <p className="text-xs text-gray-400">Total: {totalSeconds.toLocaleString()} seconds ({durationHours * 60 + durationMinutes} minutes)</p>
            </div>

            {/* Schedule Pattern */}
            <div className="rounded-xl border border-gray-200 p-4 flex flex-col gap-3">
              <div className="flex items-center gap-2">
                <AlertCircle size={14} className="text-gray-400" />
                <span className="text-sm font-semibold text-gray-900">Schedule Pattern</span>
              </div>
              <div>
                <p className="text-xs text-gray-500 mb-1.5">iCalendar RRULE (auto-generated)</p>
                <code className="block rounded-lg bg-gray-100 px-3 py-2 text-sm font-mono text-gray-800">{rrulePreview}</code>
                {scheduleType === "one-time" && (
                  <p className="text-xs text-gray-400 mt-1.5">One-time maintenance uses a fixed RRULE that triggers only once.</p>
                )}
              </div>

              {scheduleType === "recurring" && (
                <div className="flex flex-col gap-3 pt-1">
                  <div className="flex gap-3">
                    <div className="flex flex-col gap-1.5 flex-1">
                      <label className="text-xs font-medium text-gray-700">Frequency</label>
                      <select value={frequency} onChange={e => setFrequency(e.target.value)}
                        className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400">
                        <option value="DAILY">Daily</option>
                        <option value="WEEKLY">Weekly</option>
                        <option value="MONTHLY">Monthly</option>
                      </select>
                    </div>
                    <div className="flex flex-col gap-1.5 w-28">
                      <label className="text-xs font-medium text-gray-700">Every (N)</label>
                      <input type="number" min={1} value={interval} onChange={e => setIntervalVal(Number(e.target.value))}
                        className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
                    </div>
                  </div>
                  {frequency === "WEEKLY" && (
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs font-medium text-gray-700">Days</label>
                      <div className="flex gap-1.5">
                        {WEEKDAYS.map(d => (
                          <button key={d.value} type="button" onClick={() => toggleWeekday(d.value)}
                            className={`w-9 h-9 rounded-lg text-xs font-semibold border transition-colors ${weekdays.has(d.value) ? "bg-gray-900 border-gray-900 text-white" : "bg-white border-gray-300 text-gray-600 hover:bg-gray-50"}`}>
                            {d.label}
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Affected Services */}
            {!isGlobal && (
              <div className="flex flex-col gap-2">
                <label className="text-sm font-semibold text-gray-900">Affected Services</label>
                <div className="rounded-xl border border-gray-200 p-4">
                  <p className="text-xs text-gray-500 mb-3">Select services to add:</p>
                  {services.length === 0 ? (
                    <p className="text-sm text-gray-400">No services available.</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-y-2 gap-x-4">
                      {/* All option */}
                      <label className="flex items-center gap-2 cursor-pointer col-span-2 pb-2 border-b border-gray-100 mb-1">
                        <input type="checkbox" checked={allServices}
                          onChange={e => handleAllToggle(e.target.checked)}
                          className="rounded border-gray-300 accent-gray-900" />
                        <span className="text-sm font-semibold text-gray-900">All</span>
                      </label>
                      {services.map(svc => (
                        <label key={svc.slug} className="flex items-center gap-2 cursor-pointer">
                          <input type="checkbox" checked={selectedServices.has(svc.slug)}
                            onChange={() => {
                              toggleService(svc.slug);
                              if (allServices) setAllServices(false);
                            }}
                            className="rounded border-gray-300 accent-gray-900" />
                          <span className="text-sm text-gray-700">{svc.name}</span>
                        </label>
                      ))}
                    </div>
                  )}
                  <p className="text-xs text-gray-400 mt-3">Select services and set their status during the maintenance window</p>
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-between pt-2">
              <button type="button" onClick={() => navigate(ROUTES.MAINTENANCES.LIST)}
                className="rounded-lg border border-gray-300 px-4 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50">
                Cancel
              </button>
              <button type="submit" disabled={createMutation.isPending || !title.trim()}
                className="flex items-center gap-2 rounded-lg bg-gray-900 px-5 py-2.5 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50">
                <Calendar size={15} />
                {createMutation.isPending ? "Creating…" : "Create Maintenance"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </AdminLayout>
  );
}
