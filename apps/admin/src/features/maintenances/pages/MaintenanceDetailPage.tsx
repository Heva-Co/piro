import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, AlertCircle, CheckCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { DateTimePicker } from "@/components/DateTimePicker";
import { maintenancesApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

function pad(n: number) { return String(n).padStart(2, "0"); }
function toLocalDT(d: Date) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function MaintenanceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const maintenanceKey = QUERY_KEYS.MAINTENANCE(id!);

  const { data: maintenance, isLoading } = useQuery({
    queryKey: maintenanceKey,
    queryFn: () => maintenancesApi.get(id!),
  });

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [scheduledStart, setScheduledStart] = useState("");
  const [scheduledEnd, setScheduledEnd] = useState("");
  const [init, setInit] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");
  const [cancelConfirm, setCancelConfirm] = useState("");

  useEffect(() => {
    if (maintenance && !init) {
      setName(maintenance.name);
      setDescription(maintenance.description ?? "");
      setScheduledStart(toLocalDT(new Date(maintenance.scheduledStart)));
      setScheduledEnd(toLocalDT(new Date(maintenance.scheduledEnd)));
      setInit(true);
    }
  }, [maintenance, init]);

  const updateMutation = useMutation({
    mutationFn: () =>
      maintenancesApi.update(id!, {
        name,
        description,
        scheduledStart: new Date(scheduledStart).toISOString(),
        scheduledEnd: new Date(scheduledEnd).toISOString(),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: maintenanceKey });
      setSuccess(true);
      setError("");
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: () => setError("Failed to save changes."),
  });

  const cancelMutation = useMutation({
    mutationFn: () => maintenancesApi.cancel(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: maintenanceKey });
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MAINTENANCES });
    },
    onError: () => setError("Failed to cancel maintenance."),
  });

  if (isLoading) {
    return <AdminLayout title="Maintenance"><p className="text-gray-400">Loading…</p></AdminLayout>;
  }
  if (!maintenance) {
    return <AdminLayout title="Maintenance"><p className="text-red-600">Not found.</p></AdminLayout>;
  }

  const isCancelled = maintenance.status?.toLowerCase() === "cancelled";

  return (
    <AdminLayout title={`Maintenance #${maintenance.id}`}>
      <div className="max-w-xl flex flex-col gap-6">
        <button
          onClick={() => navigate(ROUTES.MAINTENANCES.LIST)}
          className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-900 self-start"
        >
          <ChevronLeft size={16} /> Back to Maintenances
        </button>

        <form
          onSubmit={(e) => { e.preventDefault(); updateMutation.mutate(); }}
          className="flex flex-col gap-5"
        >
          {success && (
            <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
              <CheckCircle size={16} /> Saved successfully.
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <AlertCircle size={16} /> {error}
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Title</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
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

          <div className="flex gap-6 flex-wrap">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Scheduled Start</label>
              <DateTimePicker value={scheduledStart} onChange={setScheduledStart} />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Scheduled End</label>
              <DateTimePicker value={scheduledEnd} onChange={setScheduledEnd} />
            </div>
          </div>

          <div className="flex items-center gap-2 text-sm text-gray-500">
            <span>Status:</span>
            <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium capitalize ${isCancelled ? "bg-gray-100 text-gray-500" : "bg-green-100 text-green-700"}`}>
              {maintenance.status}
            </span>
          </div>

          {maintenance.services && maintenance.services.length > 0 && (
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium text-gray-500">Affected Services</label>
              <div className="flex flex-wrap gap-2">
                {maintenance.services.map((s) => (
                  <span key={s.slug} className="rounded-md border border-gray-200 bg-gray-50 px-2 py-1 text-xs">{s.name}</span>
                ))}
              </div>
            </div>
          )}

          <button
            type="submit"
            disabled={updateMutation.isPending || isCancelled}
            className="self-start rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {updateMutation.isPending ? "Saving…" : "Save Changes"}
          </button>
        </form>

        {/* Danger zone */}
        {!isCancelled && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-4 flex flex-col gap-3">
            <h3 className="text-sm font-semibold text-red-700">Danger Zone</h3>
            <p className="text-sm text-red-600">
              Cancel this maintenance. Type the maintenance name to confirm.
            </p>
            <div className="flex gap-2">
              <input
                type="text"
                value={cancelConfirm}
                onChange={(e) => setCancelConfirm(e.target.value)}
                placeholder={maintenance.name}
                className="flex-1 rounded-md border border-red-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-500"
              />
              <button
                type="button"
                disabled={cancelConfirm !== maintenance.name || cancelMutation.isPending}
                onClick={() => cancelMutation.mutate()}
                className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {cancelMutation.isPending ? "Cancelling…" : "Cancel Maintenance"}
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
