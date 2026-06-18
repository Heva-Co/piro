import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { incidentsApi, servicesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const IMPACT_OPTIONS = ["DOWN", "DEGRADED"];

function toLocalDateTimeValue(d: Date) {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function IncidentFormPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [title, setTitle] = useState("");
  const [startDateTime, setStartDateTime] = useState(toLocalDateTimeValue(new Date()));
  const [isGlobal, setIsGlobal] = useState(false);
  const [state, setState] = useState("INVESTIGATING");
  const [initialComment, setInitialComment] = useState("");
  const [selectedServices, setSelectedServices] = useState<Record<string, string>>({}); // slug -> impact
  const [error, setError] = useState("");

  const { data: services = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      const incident = await incidentsApi.create({
        title,
        status: state,
        severity: "medium",
        startedAt: new Date(startDateTime).toISOString(),
      });

      // Add services
      for (const [slug] of Object.entries(selectedServices)) {
        try {
          await incidentsApi.addService(incident.id, slug);
        } catch { /* best effort */ }
      }

      // Add initial comment
      if (initialComment.trim()) {
        await incidentsApi.addComment(incident.id, initialComment, state);
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
    setSelectedServices((prev) => {
      const next = { ...prev };
      if (next[slug]) {
        delete next[slug];
      } else {
        next[slug] = "DOWN";
      }
      return next;
    });
  }

  return (
    <AdminLayout title="New Incident">
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
              placeholder="Brief description of the incident"
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
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

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Initial State</label>
            <select
              value={state}
              onChange={(e) => setState(e.target.value)}
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="INVESTIGATING">Investigating</option>
              <option value="IDENTIFIED">Identified</option>
              <option value="MONITORING">Monitoring</option>
              <option value="RESOLVED">Resolved</option>
            </select>
          </div>

          <label className="flex items-center gap-2 cursor-pointer">
            <div className="relative">
              <input type="checkbox" checked={isGlobal} onChange={(e) => setIsGlobal(e.target.checked)} className="sr-only peer" />
              <div className="w-9 h-5 rounded-full bg-gray-200 peer-checked:bg-indigo-600 transition-colors" />
              <div className="absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform peer-checked:translate-x-4" />
            </div>
            <span className="text-sm font-medium">Global incident</span>
          </label>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Initial Update (optional)</label>
            <textarea
              value={initialComment}
              onChange={(e) => setInitialComment(e.target.value)}
              rows={3}
              placeholder="We are investigating reports of…"
              className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          {services.length > 0 && (
            <div className="flex flex-col gap-2">
              <label className="text-sm font-medium">Affected Services</label>
              <div className="rounded-lg border border-gray-200 divide-y divide-gray-100">
                {services.map((svc) => {
                  const checked = Boolean(selectedServices[svc.slug]);
                  return (
                    <div key={svc.slug} className="flex items-center gap-3 px-3 py-2">
                      <input
                        type="checkbox"
                        id={`svc-${svc.slug}`}
                        checked={checked}
                        onChange={() => toggleService(svc.slug)}
                        className="rounded border-gray-300"
                      />
                      <label htmlFor={`svc-${svc.slug}`} className="flex-1 text-sm cursor-pointer">
                        {svc.name}
                      </label>
                      {checked && (
                        <select
                          value={selectedServices[svc.slug]}
                          onChange={(e) =>
                            setSelectedServices((prev) => ({ ...prev, [svc.slug]: e.target.value }))
                          }
                          className="rounded border border-gray-300 bg-white px-2 py-1 text-xs"
                        >
                          {IMPACT_OPTIONS.map((o) => <option key={o} value={o}>{o}</option>)}
                        </select>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          <div className="flex gap-3">
            <button
              type="submit"
              disabled={createMutation.isPending}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {createMutation.isPending ? "Creating…" : "Create Incident"}
            </button>
            <button
              type="button"
              onClick={() => navigate(ROUTES.INCIDENTS.LIST)}
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
