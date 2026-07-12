import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Save } from "lucide-react";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Switch } from "@/components/ui/switch";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import { DateTimePicker } from "@/components/DateTimePicker";
import { PageHeader } from "@/components/PageHeader";
import { useAllServices } from "@/hooks/useServices";
import { incidentsApi } from "@/lib/actions/incidents";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const IMPACT_OPTIONS = ["DOWN", "DEGRADED"];

function toLocalDT(d: Date) {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

export default function IncidentFormPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [title, setTitle] = useState("");
  const [startDateTime, setStartDateTime] = useState(toLocalDT(new Date()));
  const [status] = useState("INVESTIGATING");
  const [initialComment, setInitialComment] = useState("");
  const [acknowledge, setAcknowledge] = useState(false);
  const [selectedServices, setSelectedServices] = useState<Record<string, string>>({});
  const [error, setError] = useState("");

  const { data: services = [] } = useAllServices();

  const createMutation = useMutation({
    mutationFn: async () => {
      const incident = await incidentsApi.create({
        title,
        status,
        startDateTime: Math.floor(new Date(startDateTime).getTime() / 1000),
      });
      for (const [slug] of Object.entries(selectedServices)) {
        try { await incidentsApi.addService(incident.id, slug, ""); } catch { /* best effort */ }
      }
      if (initialComment.trim()) {
        await incidentsApi.addComment(incident.id, initialComment, status);
      }
      if (acknowledge) {
        await incidentsApi.acknowledge(incident.id);
      }
      return incident;
    },
    onSuccess: (incident) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.INCIDENTS });
      navigate(ROUTES.INCIDENTS.DETAIL(incident.id));
    },
    onError: () => setError("Failed to create incident."),
  });

  function toggleService(slug: string) {
    setSelectedServices(prev => {
      const next = { ...prev };
      if (next[slug]) delete next[slug]; else next[slug] = "DOWN";
      return next;
    });
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Incidents", onClick: () => navigate(ROUTES.INCIDENTS.LIST) },
          { label: "New Incident" },
        ]}
      />

      <div className="max-w-2xl">
        <div className="rounded-2xl border border-border bg-card overflow-hidden">
          <div className="px-6 pt-6 pb-5 border-b border-border">
            <h1 className="text-xl font-bold text-foreground">Create New Incident</h1>
            <p className="text-sm text-muted-foreground mt-0.5">Create a new incident to track</p>
          </div>

          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
            <div className="px-6 py-5 flex flex-col gap-5">
              {error && (
                <div className="flex items-center gap-2 rounded-xl border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
                  <AlertCircle size={15} /> {error}
                </div>
              )}

              {/* Title */}
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold text-foreground">
                  Title <span className="text-destructive">*</span>
                </label>
                <Input value={title} onChange={e => setTitle(e.target.value)} required
                  placeholder="Brief description of the incident" />
              </div>

              {/* Start Date/Time */}
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold text-foreground">
                  Start Date/Time <span className="text-destructive">*</span>
                </label>
                <DateTimePicker value={startDateTime} onChange={setStartDateTime} />
                <p className="text-xs text-muted-foreground">Enter time in your local timezone. It will be stored as UTC.</p>
              </div>

              {/* Acknowledge */}
              <div className="rounded-xl border border-border p-4 flex items-center justify-between">
                <div>
                  <p className="text-sm font-semibold text-foreground">Mark as Acknowledged</p>
                  <p className="text-xs text-muted-foreground mt-0.5">The incident is already known and being handled</p>
                </div>
                <Switch checked={acknowledge} onCheckedChange={setAcknowledge} />
              </div>

              {/* Affected Services */}
              {services.length > 0 && (
                <div className="flex flex-col gap-2">
                  <label className="text-sm font-semibold text-foreground">Affected Services</label>
                  <div className="rounded-xl border border-border divide-y divide-border">
                    {services.map((svc) => {
                      const checked = Boolean(selectedServices[svc.slug]);
                      return (
                        <div key={svc.slug} className="flex items-center gap-3 px-4 py-2.5">
                          <input type="checkbox" id={`svc-${svc.slug}`} checked={checked}
                            onChange={() => toggleService(svc.slug)}
                            className="size-4 rounded border-border accent-foreground cursor-pointer" />
                          <label htmlFor={`svc-${svc.slug}`} className="flex-1 text-sm cursor-pointer text-foreground">
                            {svc.name}
                          </label>
                          {checked && (
                            <Select
                              value={selectedServices[svc.slug]}
                              onValueChange={(v) => v && setSelectedServices(prev => ({ ...prev, [svc.slug]: v }))}
                            >
                              <SelectTrigger className="w-32 h-8 text-xs">
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {IMPACT_OPTIONS.map(o => <SelectItem key={o} value={o}>{o}</SelectItem>)}
                              </SelectContent>
                            </Select>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Initial Update */}
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold text-foreground">
                  Initial Update <span className="text-xs font-normal text-muted-foreground">(Optional)</span>
                </label>
                <MarkdownEditor value={initialComment} onChange={setInitialComment} placeholder="Describe what's happening..." />
                <p className="text-xs text-muted-foreground">This will be added as the first update for this incident.</p>
              </div>
            </div>

            {/* Footer */}
            <div className="px-6 py-4 border-t border-border flex justify-end">
              <Button type="submit" disabled={createMutation.isPending || !title.trim()}>
                <Save size={15} />
                {createMutation.isPending ? "Creating…" : "Create Incident"}
              </Button>
            </div>
          </form>
        </div>
      </div>
    </>
  );
}
