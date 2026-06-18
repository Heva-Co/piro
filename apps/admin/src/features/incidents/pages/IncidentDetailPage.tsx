import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Trash2, Plus, AlertCircle, ChevronLeft } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { incidentsApi, servicesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const STATE_BADGE: Record<string, string> = {
  INVESTIGATING: "bg-amber-100 text-amber-700",
  IDENTIFIED: "bg-orange-100 text-orange-700",
  MONITORING: "bg-blue-100 text-blue-700",
  RESOLVED: "bg-green-100 text-green-700",
};

export default function IncidentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const incidentKey = QUERY_KEYS.INCIDENT(id!);
  const commentsKey = ["incident-comments", id];

  const { data: incident, isLoading } = useQuery({
    queryKey: incidentKey,
    queryFn: () => incidentsApi.get(id!),
  });

  const { data: comments = [] } = useQuery({
    queryKey: commentsKey,
    queryFn: () => incidentsApi.comments(id!),
    enabled: Boolean(id),
  });

  const { data: services = [] } = useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });

  // Comment form
  const [commentBody, setCommentBody] = useState("");
  const [commentState, setCommentState] = useState("INVESTIGATING");
  const [commentError, setCommentError] = useState("");

  // Incident edit fields
  const [title, setTitle] = useState("");
  const [titleInit, setTitleInit] = useState(false);
  if (incident && !titleInit) {
    setTitle(incident.title);
    setTitleInit(true);
  }

  // Add service
  const [showAddService, setShowAddService] = useState(false);
  const [addServiceSlug, setAddServiceSlug] = useState("");
  const [serviceError, setServiceError] = useState("");

  const addCommentMutation = useMutation({
    mutationFn: () => incidentsApi.addComment(id!, commentBody, commentState),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: commentsKey });
      qc.invalidateQueries({ queryKey: incidentKey });
      setCommentBody("");
      setCommentError("");
    },
    onError: () => setCommentError("Failed to add update."),
  });

  const deleteCommentMutation = useMutation({
    mutationFn: (commentId: number) => incidentsApi.deleteComment(id!, commentId),
    onSuccess: () => qc.invalidateQueries({ queryKey: commentsKey }),
  });

  const updateTitleMutation = useMutation({
    mutationFn: () => incidentsApi.update(id!, { title }),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  const addServiceMutation = useMutation({
    mutationFn: () => incidentsApi.addService(id!, addServiceSlug),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: incidentKey });
      setShowAddService(false);
      setAddServiceSlug("");
      setServiceError("");
    },
    onError: () => setServiceError("Failed to add service."),
  });

  const removeServiceMutation = useMutation({
    mutationFn: (slug: string) => incidentsApi.removeService(id!, slug),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKey }),
  });

  if (isLoading) {
    return (
      <AdminLayout title="Incident">
        <p className="text-gray-400">Loading…</p>
      </AdminLayout>
    );
  }

  if (!incident) {
    return (
      <AdminLayout title="Incident">
        <p className="text-red-600">Incident not found.</p>
      </AdminLayout>
    );
  }

  const availableServices = services.filter(
    (s) => !incident.services?.some((is) => is.slug === s.slug)
  );

  return (
    <AdminLayout title={`Incident #${incident.id}`}>
      <div className="flex flex-col gap-4">
        <button
          onClick={() => navigate(ROUTES.INCIDENTS.LIST)}
          className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-900 self-start"
        >
          <ChevronLeft size={16} /> Back to Incidents
        </button>

        <div className="flex gap-6 flex-col lg:flex-row">
          {/* Left panel — updates timeline */}
          <div className="flex-1 lg:flex-[2] flex flex-col gap-4">
            <div className="rounded-lg border border-gray-200 bg-white shadow-sm">
              <div className="px-4 py-3 border-b border-gray-100">
                <h2 className="text-sm font-semibold">Updates</h2>
              </div>

              {comments.length === 0 && (
                <p className="px-4 py-6 text-sm text-gray-400 text-center">No updates yet.</p>
              )}

              <div className="divide-y divide-gray-100">
                {[...comments].reverse().map((c) => (
                  <div key={c.id} className="px-4 py-3 flex gap-3">
                    <div className="flex-1 flex flex-col gap-1.5">
                      <div className="flex items-center gap-2">
                        <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${STATE_BADGE[c.status.toUpperCase()] ?? "bg-gray-100 text-gray-600"}`}>
                          {c.status}
                        </span>
                        <span className="text-xs text-gray-400">
                          {new Date(c.createdAt).toLocaleString()}
                        </span>
                      </div>
                      <p className="text-sm text-gray-700 whitespace-pre-wrap">{c.body}</p>
                    </div>
                    <button
                      onClick={() => {
                        if (confirm("Delete this update?")) {
                          deleteCommentMutation.mutate(c.id);
                        }
                      }}
                      className="shrink-0 rounded p-1 text-gray-300 hover:text-red-600 hover:bg-red-50 transition-colors"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                ))}
              </div>

              {/* Add update form */}
              <div className="px-4 py-4 border-t border-gray-100 flex flex-col gap-3">
                <h3 className="text-sm font-semibold text-gray-600">Post Update</h3>
                {commentError && (
                  <div className="flex items-center gap-2 text-sm text-red-600">
                    <AlertCircle size={14} /> {commentError}
                  </div>
                )}
                <select
                  value={commentState}
                  onChange={(e) => setCommentState(e.target.value)}
                  className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 self-start"
                >
                  <option value="INVESTIGATING">Investigating</option>
                  <option value="IDENTIFIED">Identified</option>
                  <option value="MONITORING">Monitoring</option>
                  <option value="RESOLVED">Resolved</option>
                </select>
                <textarea
                  value={commentBody}
                  onChange={(e) => setCommentBody(e.target.value)}
                  rows={3}
                  placeholder="Describe the current situation…"
                  className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
                <button
                  onClick={() => addCommentMutation.mutate()}
                  disabled={!commentBody.trim() || addCommentMutation.isPending}
                  className="self-start rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                >
                  {addCommentMutation.isPending ? "Posting…" : "Post Update"}
                </button>
              </div>
            </div>
          </div>

          {/* Right panel — incident details */}
          <div className="lg:flex-1 flex flex-col gap-4">
            <div className="rounded-lg border border-gray-200 bg-white shadow-sm">
              <div className="px-4 py-3 border-b border-gray-100">
                <h2 className="text-sm font-semibold">Details</h2>
              </div>
              <div className="px-4 py-4 flex flex-col gap-4">
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-medium text-gray-500 uppercase tracking-wide">Title</label>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={title}
                      onChange={(e) => setTitle(e.target.value)}
                      className="flex-1 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    />
                    <button
                      onClick={() => updateTitleMutation.mutate()}
                      disabled={title === incident.title || updateTitleMutation.isPending}
                      className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                    >
                      Save
                    </button>
                  </div>
                </div>

                <div>
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide">Start</span>
                  <p className="text-sm mt-1">{new Date(incident.startedAt).toLocaleString()}</p>
                </div>

                <div>
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide">State</span>
                  <div className="mt-1">
                    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${STATE_BADGE[incident.status?.toUpperCase()] ?? "bg-gray-100 text-gray-600"}`}>
                      {incident.status}
                    </span>
                  </div>
                </div>

                <div>
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-xs font-medium text-gray-500 uppercase tracking-wide">Affected Services</span>
                    <button
                      onClick={() => setShowAddService((v) => !v)}
                      className="flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800"
                    >
                      <Plus size={12} /> Add
                    </button>
                  </div>

                  {showAddService && (
                    <div className="mb-2 flex flex-col gap-2">
                      {serviceError && (
                        <p className="text-xs text-red-600">{serviceError}</p>
                      )}
                      <div className="flex gap-2">
                        <select
                          value={addServiceSlug}
                          onChange={(e) => setAddServiceSlug(e.target.value)}
                          className="flex-1 rounded border border-gray-300 bg-white px-2 py-1 text-sm"
                        >
                          <option value="">Select service…</option>
                          {availableServices.map((s) => (
                            <option key={s.slug} value={s.slug}>{s.name}</option>
                          ))}
                        </select>
                        <button
                          onClick={() => { if (addServiceSlug) addServiceMutation.mutate(); }}
                          disabled={!addServiceSlug || addServiceMutation.isPending}
                          className="rounded-md bg-indigo-600 px-2 py-1 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                        >
                          Add
                        </button>
                      </div>
                    </div>
                  )}

                  {incident.services?.length === 0 && (
                    <p className="text-sm text-gray-400">No services.</p>
                  )}
                  <div className="flex flex-col gap-1">
                    {incident.services?.map((svc) => (
                      <div key={svc.slug} className="flex items-center justify-between rounded-md border border-gray-100 px-3 py-1.5 text-sm">
                        <span>{svc.name}</span>
                        <button
                          onClick={() => removeServiceMutation.mutate(svc.slug)}
                          className="text-gray-300 hover:text-red-600 transition-colors"
                        >
                          <Trash2 size={13} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
}
