import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, Server, Copy, AlertCircle, Pencil, Power } from "lucide-react";
import { workersApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

function Modal({ children, onClose }: { children: React.ReactNode; onClose: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 p-6 relative">
        {children}
      </div>
    </div>
  );
}

function ModalClose({ onClose }: { onClose: () => void }) {
  return (
    <button type="button" onClick={onClose}
      className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-xl leading-none">×</button>
  );
}

export default function WorkersPage() {
  const qc = useQueryClient();
  const { data: workers = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.WORKERS,
    queryFn: workersApi.list,
    refetchInterval: 30_000,
  });

  const [modal, setModal] = useState<"none" | "create" | "token" | "editRegion">("none");
  const [workerName, setWorkerName] = useState("");
  const [workerRegion, setWorkerRegion] = useState("default");
  const [isDefault, setIsDefault] = useState(false);
  const [newToken, setNewToken] = useState("");
  const [createError, setCreateError] = useState("");
  const [copied, setCopied] = useState(false);
  const [editingWorker, setEditingWorker] = useState<{ id: string; region: string } | null>(null);
  const [editRegion, setEditRegion] = useState("");
  const [editError, setEditError] = useState("");

  const createMutation = useMutation({
    mutationFn: () => workersApi.create(workerName, workerRegion, isDefault),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.WORKERS });
      setNewToken((data as any).workerToken ?? "");
      setWorkerName("");
      setCreateError("");
      setModal("token");
    },
    onError: () => setCreateError("Failed to register worker."),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => workersApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.WORKERS }),
  });

  const updateRegionMutation = useMutation({
    mutationFn: ({ id, region }: { id: string; region: string }) =>
      workersApi.updateRegion(id, region),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.WORKERS });
      closeModal();
    },
    onError: () => setEditError("Failed to update region."),
  });

  const toggleBuiltinMutation = useMutation({
    mutationFn: (disabled: boolean) => workersApi.toggleBuiltin(disabled),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.WORKERS }),
  });

  function closeModal() {
    setModal("none");
    setNewToken("");
    setCopied(false);
    setIsDefault(false);
    setEditingWorker(null);
    setEditRegion("");
    setEditError("");
  }

  function handleCopy() {
    navigator.clipboard.writeText(newToken);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  function openEditRegion(w: { id: string; region: string }) {
    setEditingWorker(w);
    setEditRegion(w.region);
    setEditError("");
    setModal("editRegion");
  }

  const noLocalExecution = !isLoading && !workers.some(w => w.isConnected);

  return (
    <>
      {/* Header */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Workers</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Remote check workers execute monitoring checks from different regions and report results back.
          </p>
        </div>
        <button
          onClick={() => { setModal("create"); setCreateError(""); }}
          className="flex items-center gap-2 rounded-lg bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800"
        >
          <Plus size={15} /> Register Worker
        </button>
      </div>

      <div className="flex flex-col gap-4">
        {/* No-execution warning banner */}
        {noLocalExecution && (
          <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 flex items-center gap-3">
            <AlertCircle size={16} className="text-amber-600 shrink-0" />
            <p className="text-sm text-amber-800">
              No default worker connected — non-multi-region checks are not executing. Register and connect a worker marked as <strong>default</strong>.
            </p>
          </div>
        )}

        {/* Workers list */}
        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {isLoading && (
            <div className="py-16 text-center text-sm text-gray-400">Loading…</div>
          )}
          {!isLoading && workers.length === 0 && (
            <div className="py-14 flex flex-col items-center gap-3">
              <div className="w-12 h-12 rounded-full bg-gray-100 flex items-center justify-center">
                <Server size={20} className="text-gray-400" />
              </div>
              <div className="text-center">
                <p className="text-sm font-medium text-gray-700">No workers registered</p>
                <p className="text-xs text-gray-400 mt-0.5">Register a worker to run checks from external regions.</p>
              </div>
              <button
                onClick={() => { setModal("create"); setCreateError(""); }}
                className="flex items-center gap-2 rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                <Plus size={14} /> Register your first worker
              </button>
            </div>
          )}
          {workers.map((w, i) => (
            <div key={w.id}
              className={`flex items-center gap-4 px-5 py-4 ${i > 0 ? "border-t border-gray-100" : ""}`}>
              <div className={`w-9 h-9 rounded-full flex items-center justify-center shrink-0 ${w.isBuiltIn ? "bg-blue-100" : "bg-gray-100"}`}>
                <Server size={16} className={w.isBuiltIn ? "text-blue-600" : "text-gray-400"} />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-gray-900">
                  {w.name}
                  {w.isBuiltIn && <span className="ml-2 text-xs font-normal text-blue-600">(built-in)</span>}
                </p>
                <p className="text-xs text-gray-400 font-mono">{w.region}{w.version ? ` · v${w.version}` : ""}</p>
              </div>
              <div className="flex items-center gap-4">
                {w.isDefault && (
                  <span className="rounded-full bg-indigo-100 text-indigo-700 px-3 py-0.5 text-xs font-semibold">Default</span>
                )}
                <span className={`rounded-full px-3 py-0.5 text-xs font-semibold ${w.isConnected ? "bg-green-500/15 text-green-600 dark:text-green-400" : "bg-muted text-muted-foreground"}`}>
                  {w.isConnected ? "Online" : "Offline"}
                </span>
                {w.lastHeartbeat && (
                  <span className="text-sm text-gray-400">
                    {new Date(w.lastHeartbeat).toLocaleDateString("en-US", { month: "numeric", day: "numeric", year: "numeric" })}
                  </span>
                )}
                {w.isBuiltIn ? (
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => openEditRegion({ id: w.id, region: w.region })}
                      className="text-gray-400 hover:text-gray-600 transition-colors"
                      title="Edit region"
                    >
                      <Pencil size={15} />
                    </button>
                    <button
                      onClick={() => toggleBuiltinMutation.mutate(w.isConnected)}
                      disabled={toggleBuiltinMutation.isPending}
                      className={`transition-colors ${w.isConnected ? "text-green-500 hover:text-red-500" : "text-gray-400 hover:text-green-500"}`}
                      title={w.isConnected ? "Disable built-in worker" : "Enable built-in worker"}
                    >
                      <Power size={15} />
                    </button>
                  </div>
                ) : (
                  <button
                    onClick={() => { if (confirm(`Remove worker "${w.name}"?`)) deleteMutation.mutate(w.id); }}
                    className="text-gray-400 hover:text-red-600 transition-colors"
                  >
                    <Trash2 size={16} />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Register modal */}
      {modal === "create" && (
        <Modal onClose={closeModal}>
          <ModalClose onClose={closeModal} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">Register Worker</h2>
          <p className="text-sm text-gray-500 mb-5">Add a remote worker to run checks from an external region.</p>
          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col gap-4">
            {createError && (
              <div className="flex items-center gap-2 text-sm text-red-600">
                <AlertCircle size={14} /> {createError}
              </div>
            )}
            <div>
              <label className="block text-sm font-semibold text-gray-900 mb-2">Name</label>
              <input type="text" value={workerName} onChange={(e) => setWorkerName(e.target.value)}
                required autoFocus placeholder="e.g. EU Worker"
                className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
            </div>
            <div>
              <label className="block text-sm font-semibold text-gray-900 mb-2">Region</label>
              <input type="text" value={workerRegion} onChange={(e) => setWorkerRegion(e.target.value)}
                required placeholder="e.g. eu-west-1"
                className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
            </div>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} className="rounded border-gray-300" />
              <span className="text-sm text-gray-700">Set as default worker <span className="text-gray-400">(receives non-multi-region checks when API worker is disabled)</span></span>
            </label>
            <div className="flex justify-end gap-3 mt-2">
              <button type="button" onClick={closeModal}
                className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
                Cancel
              </button>
              <button type="submit" disabled={createMutation.isPending || !workerName.trim()}
                className="rounded-lg bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50">
                {createMutation.isPending ? "Registering…" : "Register"}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Token reveal modal */}
      {modal === "token" && (
        <Modal onClose={closeModal}>
          <ModalClose onClose={closeModal} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">Worker Token</h2>
          <p className="text-sm text-gray-500 mb-5">Copy this token now — it won't be shown again.</p>
          <div className="rounded-xl bg-gray-50 border border-gray-200 p-4 flex items-start gap-3 mb-4">
            <div className="flex-1 min-w-0">
              <p className="text-xs text-gray-500 mb-1">Set as <code className="font-mono">PIRO_WORKER_TOKEN</code> on your worker process:</p>
              <code className="text-sm font-mono text-gray-900 break-all">{newToken}</code>
            </div>
            <button onClick={handleCopy} title="Copy"
              className="flex-shrink-0 rounded-lg border border-border bg-card p-2 hover:bg-gray-100">
              {copied
                ? <span className="text-xs text-green-600 font-medium px-1">Copied!</span>
                : <Copy size={16} className="text-gray-500" />}
            </button>
          </div>
          <div className="flex items-center gap-2 rounded-xl bg-yellow-50 border border-yellow-200 px-4 py-3 mb-6">
            <AlertCircle size={16} className="text-yellow-600 shrink-0" />
            <p className="text-sm text-yellow-800">Store this token securely. You won't be able to see it again.</p>
          </div>
          <div className="flex justify-end">
            <button onClick={closeModal}
              className="rounded-lg bg-gray-900 px-5 py-2 text-sm font-medium text-white hover:bg-gray-800">
              Done
            </button>
          </div>
        </Modal>
      )}

      {/* Edit region modal (built-in worker) */}
      {modal === "editRegion" && editingWorker && (
        <Modal onClose={closeModal}>
          <ModalClose onClose={closeModal} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">Edit Region</h2>
          <p className="text-sm text-gray-500 mb-5">Update the region label for the built-in API worker.</p>
          <form onSubmit={(e) => {
            e.preventDefault();
            updateRegionMutation.mutate({ id: editingWorker.id, region: editRegion });
          }} className="flex flex-col gap-4">
            {editError && (
              <div className="flex items-center gap-2 text-sm text-red-600">
                <AlertCircle size={14} /> {editError}
              </div>
            )}
            <div>
              <label className="block text-sm font-semibold text-gray-900 mb-2">Region</label>
              <input type="text" value={editRegion} onChange={(e) => setEditRegion(e.target.value)}
                required autoFocus placeholder="e.g. us-east-1"
                className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400" />
            </div>
            <div className="flex justify-end gap-3 mt-2">
              <button type="button" onClick={closeModal}
                className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
                Cancel
              </button>
              <button type="submit" disabled={updateRegionMutation.isPending || !editRegion.trim()}
                className="rounded-lg bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50">
                {updateRegionMutation.isPending ? "Saving…" : "Save"}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </>
  );
}
