import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Calendar, RefreshCw, ChevronRight } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { DateTimePicker } from "@/components/DateTimePicker";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { RRuleEditor, ONE_TIME_RRULE } from "@/components/RRuleEditor";
import { maintenancesApi, servicesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

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
  const [durationSeconds, setDurationSeconds] = useState(3600);
  const [recurringRule, setRecurringRule] = useState("FREQ=WEEKLY;BYDAY=MO");
  const [isGlobal, setIsGlobal] = useState(false);
  const [allServices, setAllServices] = useState(false);
  const [selectedServices, setSelectedServices] = useState<Set<string>>(new Set());
  const [error, setError] = useState("");

  const { data: services = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  const rRule = scheduleType === "one-time" ? ONE_TIME_RRULE : recurringRule;

  const createMutation = useMutation({
    mutationFn: () =>
      maintenancesApi.create({
        title,
        description,
        startDateTime: Math.floor(new Date(startDateTime).getTime() / 1000),
        rRule,
        durationSeconds,
        isGlobal,
        serviceSlugs: isGlobal ? undefined : [...selectedServices],
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
      navigate(ROUTES.MAINTENANCES.LIST);
    },
    onError: () => setError("Failed to create maintenance."),
  });

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
          <h1 className="text-xl font-bold mb-0.5">Create New Maintenance</h1>
          <p className="text-sm text-muted-foreground mb-6">Schedule a new maintenance window</p>

          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col gap-5">
            {error && (
              <div className="flex items-center gap-2 rounded-xl border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
                <AlertCircle size={15} /> {error}
              </div>
            )}

            {/* Schedule Type */}
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Schedule Type *</label>
              <div className="flex gap-5">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="radio" value="one-time" checked={scheduleType === "one-time"}
                    onChange={() => setScheduleType("one-time")} className="accent-foreground" />
                  <Calendar size={15} className="text-muted-foreground" />
                  <span className="text-sm">One-Time</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="radio" value="recurring" checked={scheduleType === "recurring"}
                    onChange={() => setScheduleType("recurring")} className="accent-foreground" />
                  <RefreshCw size={15} className="text-muted-foreground" />
                  <span className="text-sm">Recurring</span>
                </label>
              </div>
            </div>

            {/* Title */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Title *</label>
              <Input type="text" value={title} onChange={e => setTitle(e.target.value)} required
                placeholder="Scheduled maintenance window" />
            </div>

            {/* Description */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Description</label>
              <Textarea value={description} onChange={e => setDescription(e.target.value)} rows={3}
                placeholder="Details about the maintenance…" />
            </div>

            {/* Global maintenance toggle */}
            <div className="rounded-xl border p-4 flex items-center justify-between">
              <div>
                <p className="text-sm font-semibold">Global Maintenance</p>
                <p className="text-xs text-muted-foreground mt-0.5">When enabled, this maintenance will be visible on all status pages</p>
              </div>
              <button type="button" onClick={() => setIsGlobal(v => !v)}
                className={`relative w-10 h-6 rounded-full transition-colors ${isGlobal ? "bg-foreground" : "bg-muted"}`}>
                <span className={`absolute top-1 w-4 h-4 rounded-full bg-background shadow transition-transform ${isGlobal ? "translate-x-5" : "translate-x-1"}`} />
              </button>
            </div>

            {/* Start Date/Time */}
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">Start Date/Time *</label>
              <DateTimePicker value={startDateTime} onChange={setStartDateTime} />
            </div>

            {/* Schedule Pattern (recurrence + duration) */}
            <div className="rounded-xl border p-4 flex flex-col gap-3">
              <div className="flex items-center gap-2">
                <AlertCircle size={14} className="text-muted-foreground" />
                <span className="text-sm font-semibold">Schedule Pattern</span>
              </div>

              {scheduleType === "recurring" ? (
                <RRuleEditor
                  value={recurringRule}
                  onChange={setRecurringRule}
                  startDate={new Date(startDateTime)}
                  showDuration
                  durationSeconds={durationSeconds}
                  onDurationChange={setDurationSeconds}
                />
              ) : (
                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">Duration</label>
                  <div className="flex items-center gap-2">
                    <Input type="number" min={0} value={Math.floor(durationSeconds / 3600)}
                      onChange={(e) => setDurationSeconds(Number(e.target.value) * 3600 + (durationSeconds % 3600))}
                      className="w-16" />
                    <span className="text-sm text-muted-foreground">h</span>
                    <Input type="number" min={0} max={59} value={Math.floor((durationSeconds % 3600) / 60)}
                      onChange={(e) => setDurationSeconds(Math.floor(durationSeconds / 3600) * 3600 + Number(e.target.value) * 60)}
                      className="w-16" />
                    <span className="text-sm text-muted-foreground">m</span>
                  </div>
                  <p className="text-xs text-muted-foreground mt-1">Runs once, at the start date/time above.</p>
                </div>
              )}
            </div>

            {/* Affected Services */}
            {!isGlobal && (
              <div className="flex flex-col gap-2">
                <label className="text-sm font-semibold">Affected Services</label>
                <div className="rounded-xl border p-4">
                  <p className="text-xs text-muted-foreground mb-3">Select services to add:</p>
                  {services.length === 0 ? (
                    <p className="text-sm text-muted-foreground">No services available.</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-y-2 gap-x-4">
                      <label className="flex items-center gap-2 cursor-pointer col-span-2 pb-2 border-b mb-1">
                        <input type="checkbox" checked={allServices}
                          onChange={e => handleAllToggle(e.target.checked)}
                          className="rounded accent-foreground" />
                        <span className="text-sm font-semibold">All</span>
                      </label>
                      {services.map(svc => (
                        <label key={svc.slug} className="flex items-center gap-2 cursor-pointer">
                          <input type="checkbox" checked={selectedServices.has(svc.slug)}
                            onChange={() => {
                              toggleService(svc.slug);
                              if (allServices) setAllServices(false);
                            }}
                            className="rounded accent-foreground" />
                          <span className="text-sm">{svc.name}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-between pt-2">
              <button type="button" onClick={() => navigate(ROUTES.MAINTENANCES.LIST)}
                className="rounded-lg border px-4 py-2.5 text-sm font-medium hover:bg-muted">
                Cancel
              </button>
              <button type="submit" disabled={createMutation.isPending || !title.trim()}
                className="flex items-center gap-2 rounded-lg bg-foreground text-background px-5 py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50">
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
