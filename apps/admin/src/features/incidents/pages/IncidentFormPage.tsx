import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Save, ChevronRight } from "lucide-react";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Switch } from "@/components/ui/switch";
import { AdminLayout } from "@/components/AdminLayout";
import { DateTimePicker } from "@/components/DateTimePicker";
import { incidentsApi, servicesApi } from "@/lib/api";
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
  const [isGlobal, setIsGlobal] = useState(false);
  const [state] = useState("INVESTIGATING");
  const [initialComment, setInitialComment] = useState("");
  const [acknowledge, setAcknowledge] = useState(false);
  const [selectedServices, setSelectedServices] = useState<Record<string, string>>({});
  const [error, setError] = useState("");

  const { data: services = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      const incident = await incidentsApi.create({
        title,
        state,
        startDateTime: Math.floor(new Date(startDateTime).getTime() / 1000),
        isGlobal,
      });
      for (const [slug] of Object.entries(selectedServices)) {
        try { await incidentsApi.addService(incident.id, slug); } catch { /* best effort */ }
      }
      if (initialComment.trim()) {
        await incidentsApi.addComment(incident.id, initialComment, state);
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
    <AdminLayout title="New Incident">
      {/* Breadcrumb */}
      <div className="flex items-center gap-1.5 text-sm text-gray-500 mb-5">
        <button onClick={() => navigate(ROUTES.INCIDENTS.LIST)} className="hover:text-gray-700">Incidents</button>
        <ChevronRight size={14} />
        <span className="text-gray-900 font-medium">New Incident</span>
      </div>

      <div className="max-w-2xl">
        <div className="rounded-2xl border border-border bg-card overflow-hidden">
          {/* Header */}
          <div className="px-6 pt-6 pb-5 border-b border-gray-100">
            <h1 className="text-xl font-bold text-gray-900">Create New Incident</h1>
            <p className="text-sm text-gray-500 mt-0.5">Create a new incident to track</p>
          </div>

          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
            <div className="px-6 py-5 flex flex-col gap-5">
              {error && (
                <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                  <AlertCircle size={15} /> {error}
                </div>
              )}

              {/* Title */}
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold text-gray-900">
                  Title <span className="text-red-500">*</span>
                </label>
                <input type="text" value={title} onChange={e => setTitle(e.target.value)} required
                  placeholder="Brief description of the incident"
                  className="rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
              </div>

              {/* Start Date/Time */}
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold text-gray-900">
                  Start Date/Time <span className="text-red-500">*</span>
                </label>
                <DateTimePicker value={startDateTime} onChange={setStartDateTime} />
                <p className="text-xs text-gray-400">Enter time in your local timezone. It will be stored as UTC.</p>
              </div>

              {/* Global incident toggle */}
              <div className="rounded-xl border border-gray-200 p-4 flex items-center justify-between">
                <div>
                  <p className="text-sm font-semibold text-gray-900">Global Incident</p>
                  <p className="text-xs text-gray-500 mt-0.5">When enabled, this incident will be visible on all status pages</p>
                </div>
                <Switch checked={isGlobal} onCheckedChange={setIsGlobal} />
              </div>

              {/* Acknowledge */}
              <div className="rounded-xl border border-gray-200 p-4 flex items-center justify-between">
                <div>
                  <p className="text-sm font-semibold text-gray-900">Mark as Acknowledged</p>
                  <p className="text-xs text-gray-500 mt-0.5">The incident is already known and being handled</p>
                </div>
                <Switch checked={acknowledge} onCheckedChange={setAcknowledge} />
              </div>

              {/* Affected Services */}
              {!isGlobal && services.length > 0 && (
                <div className="flex flex-col gap-2">
                  <label className="text-sm font-semibold text-gray-900">Affected Services</label>
                  <div className="rounded-xl border border-gray-200 divide-y divide-gray-100">
                    {services.map((svc) => {
                      const checked = Boolean(selectedServices[svc.slug]);
                      return (
                        <div key={svc.slug} className="flex items-center gap-3 px-4 py-2.5">
                          <input type="checkbox" id={`svc-${svc.slug}`} checked={checked}
                            onChange={() => toggleService(svc.slug)}
                            className="rounded border-gray-300 accent-gray-900" />
                          <label htmlFor={`svc-${svc.slug}`} className="flex-1 text-sm cursor-pointer text-gray-700">
                            {svc.name}
                          </label>
                          {checked && (
                            <select value={selectedServices[svc.slug]}
                              onChange={e => setSelectedServices(prev => ({ ...prev, [svc.slug]: e.target.value }))}
                              className="rounded-lg border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none">
                              {IMPACT_OPTIONS.map(o => <option key={o} value={o}>{o}</option>)}
                            </select>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Initial Update */}
              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold text-gray-900">
                  Initial Update <span className="text-xs font-normal text-gray-400">(Optional)</span>
                </label>
                <MarkdownEditor value={initialComment} onChange={setInitialComment} placeholder="Describe what's happening..." />
                <p className="text-xs text-gray-400">This will be added as the first update for this incident.</p>
              </div>
            </div>

            {/* Footer */}
            <div className="px-6 py-4 border-t border-gray-100 flex justify-end">
              <button type="submit" disabled={createMutation.isPending || !title.trim()}
                className="flex items-center gap-2 rounded-lg bg-gray-900 px-5 py-2.5 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50">
                <Save size={15} />
                {createMutation.isPending ? "Creating…" : "Create Incident"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </AdminLayout>
  );
}
