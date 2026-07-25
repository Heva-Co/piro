import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Calendar, RefreshCw } from "lucide-react";
import { DateTimePicker } from "@/components/DateTimePicker";
import { PageHeader } from "@/components/PageHeader";
import PageContainer from "@/components/PageContainer";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Checkbox } from "@/components/ui/checkbox";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import FormActions from "@/components/ui/form-actions";
import { RRuleEditor, ONE_TIME_RRULE } from "@/components/RRuleEditor";
import { maintenancesApi } from "@/lib/api";
import { useAllServices } from "@/hooks/useServices";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";
import { toDateTimeLocalValue } from "@/utils/date";
import MaintenanceField from "../components/MaintenanceField";

function MaintenanceFormPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [scheduleType, setScheduleType] = useState<"one-time" | "recurring">("one-time");
  const [startDateTime, setStartDateTime] = useState(toDateTimeLocalValue(new Date()));
  const [durationSeconds, setDurationSeconds] = useState(3600);
  const [recurringRule, setRecurringRule] = useState("FREQ=WEEKLY;BYDAY=MO");
  const [isGlobal, setIsGlobal] = useState(false);
  const [allServices, setAllServices] = useState(false);
  const [selectedServices, setSelectedServices] = useState<Set<string>>(new Set());
  const [error, setError] = useState("");

  const { data: services = [] } = useAllServices();

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
    setSelectedServices((prev) => {
      const next = new Set(prev);
      if (next.has(slug)) next.delete(slug); else next.add(slug);
      return next;
    });
  }

  function handleAllToggle(checked: boolean) {
    setAllServices(checked);
    if (checked) setSelectedServices(new Set(services.map((s) => s.slug)));
    else setSelectedServices(new Set());
  }

  return (
    <PageContainer>
      <PageHeader
        breadcrumbs={[
          { label: "Maintenances", onClick: () => navigate(ROUTES.MAINTENANCES.LIST) },
          { label: "New Maintenance" },
        ]}
      />

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

            <MaintenanceField label="Schedule Type *">
              <RadioGroup
                value={scheduleType}
                onValueChange={(v) => v && setScheduleType(v as "one-time" | "recurring")}
                className="flex-row gap-5"
              >
                <label className="flex items-center gap-2 cursor-pointer">
                  <RadioGroupItem value="one-time" />
                  <Calendar size={15} className="text-muted-foreground" />
                  <span className="text-sm">One-Time</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <RadioGroupItem value="recurring" />
                  <RefreshCw size={15} className="text-muted-foreground" />
                  <span className="text-sm">Recurring</span>
                </label>
              </RadioGroup>
            </MaintenanceField>

            <MaintenanceField label="Title *">
              <Input type="text" value={title} onChange={(e) => setTitle(e.target.value)} required
                placeholder="Scheduled maintenance window" />
            </MaintenanceField>

            <MaintenanceField label="Description">
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3}
                placeholder="Details about the maintenance…" />
            </MaintenanceField>

            {/* Global maintenance toggle */}
            <div className="rounded-xl border p-4 flex items-center justify-between">
              <div>
                <p className="text-sm font-semibold">Global Maintenance</p>
                <p className="text-xs text-muted-foreground mt-0.5">When enabled, this maintenance will be visible on all status pages</p>
              </div>
              <Switch checked={isGlobal} onCheckedChange={setIsGlobal} />
            </div>

            <MaintenanceField label="Start Date/Time *">
              <DateTimePicker value={startDateTime} onChange={setStartDateTime} />
            </MaintenanceField>

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
                <MaintenanceField label="Duration" hint="Runs once, at the start date/time above.">
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
                </MaintenanceField>
              )}
            </div>

            {/* Affected Services */}
            {!isGlobal && (
              <MaintenanceField label="Affected Services">
                <div className="rounded-xl border p-4">
                  <p className="text-xs text-muted-foreground mb-3">Select services to add:</p>
                  {services.length === 0 ? (
                    <p className="text-sm text-muted-foreground">No services available.</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-y-2 gap-x-4">
                      <label className="flex items-center gap-2 cursor-pointer col-span-2 pb-2 border-b mb-1">
                        <Checkbox checked={allServices} onCheckedChange={(c) => handleAllToggle(c === true)} />
                        <span className="text-sm font-semibold">All</span>
                      </label>
                      {services.map((svc) => (
                        <label key={svc.slug} className="flex items-center gap-2 cursor-pointer">
                          <Checkbox
                            checked={selectedServices.has(svc.slug)}
                            onCheckedChange={() => {
                              toggleService(svc.slug);
                              if (allServices) setAllServices(false);
                            }}
                          />
                          <span className="text-sm">{svc.name}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
              </MaintenanceField>
            )}

            <FormActions
              onCancel={() => navigate(ROUTES.MAINTENANCES.LIST)}
              submitLabel="Create Maintenance"
              submitPendingLabel="Creating…"
              submitIcon={<Calendar size={15} />}
              isPending={createMutation.isPending || !title.trim()}
            />
          </form>
        </div>
      </div>
    </PageContainer>
  );
}

export default MaintenanceFormPage;
