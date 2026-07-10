import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, Server, Copy, AlertCircle, Pencil, Power, Star } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { workersApi, type Worker } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

type ModalState = "none" | "create" | "token" | "editRegion";

export default function WorkersPage() {
  const qc = useQueryClient();
  const { data: workers = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.WORKERS,
    queryFn: workersApi.list,
    refetchInterval: 30_000,
  });

  const [modal, setModal] = useState<ModalState>("none");
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
      setNewToken(data.workerToken);
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

  const setDefaultMutation = useMutation({
    mutationFn: (id: string) => workersApi.setDefault(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.WORKERS }),
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

  function openEditRegion(w: Pick<Worker, "id" | "region">) {
    setEditingWorker(w);
    setEditRegion(w.region);
    setEditError("");
    setModal("editRegion");
  }

  const noLocalExecution = !isLoading && !workers.some((w) => w.isConnected);

  return (
    <div>
      <PageHeader
        breadcrumbs={[{ label: "Workers" }]}
        actions={
          <Button onClick={() => { setModal("create"); setCreateError(""); }}>
            <Plus size={15} /> Register Worker
          </Button>
        }
      />
      <p className="text-sm text-muted-foreground -mt-4 mb-6">
        Remote check workers execute single-region checks (when they're the default worker) or
        participate in every multi-region check. The region label is for display and latency
        reporting only — it does not route checks to a specific worker.
      </p>

      <div className="flex flex-col gap-4">
        {noLocalExecution && (
          <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 flex items-center gap-3">
            <AlertCircle size={16} className="text-amber-600 shrink-0" />
            <p className="text-sm text-amber-800">
              No default worker connected — non-multi-region checks are not executing. Register and connect a worker marked as <strong>default</strong>.
            </p>
          </div>
        )}

        <div className="rounded-xl border border-border bg-card overflow-hidden">
          {isLoading && (
            <div className="py-16 text-center text-sm text-muted-foreground">Loading…</div>
          )}
          {!isLoading && workers.length === 0 && (
            <div className="py-14 flex flex-col items-center gap-3">
              <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center">
                <Server size={20} className="text-muted-foreground" />
              </div>
              <div className="text-center">
                <p className="text-sm font-medium">No workers registered</p>
                <p className="text-xs text-muted-foreground mt-0.5">Register a worker to run checks from an external process.</p>
              </div>
              <Button variant="outline" onClick={() => { setModal("create"); setCreateError(""); }}>
                <Plus size={14} /> Register your first worker
              </Button>
            </div>
          )}
          {workers.map((w, i) => (
            <div key={w.id}
              className={`flex items-center gap-4 px-5 py-4 ${i > 0 ? "border-t border-border" : ""}`}>
              <div className={`w-9 h-9 rounded-full flex items-center justify-center shrink-0 ${w.isBuiltIn ? "bg-blue-500/15" : "bg-muted"}`}>
                <Server size={16} className={w.isBuiltIn ? "text-blue-600" : "text-muted-foreground"} />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold">
                  {w.name}
                  {w.isBuiltIn && <span className="ml-2 text-xs font-normal text-blue-600">(built-in)</span>}
                </p>
                <p className="text-xs text-muted-foreground font-mono">{w.region}{w.version ? ` · v${w.version}` : ""}</p>
              </div>
              <div className="flex items-center gap-4">
                {w.isDefault && (
                  <span className="rounded-full bg-indigo-500/15 text-indigo-600 px-3 py-0.5 text-xs font-semibold">Default</span>
                )}
                <span className={`rounded-full px-3 py-0.5 text-xs font-semibold ${w.isConnected ? "bg-green-500/15 text-green-600 dark:text-green-400" : "bg-muted text-muted-foreground"}`}>
                  {w.isConnected ? "Online" : "Offline"}
                </span>
                {w.lastHeartbeat && (
                  <span className="text-sm text-muted-foreground">
                    {new Date(w.lastHeartbeat).toLocaleDateString("en-US", { month: "numeric", day: "numeric", year: "numeric" })}
                  </span>
                )}
                <div className="flex items-center gap-2">
                  {!w.isDefault && (
                    <Button
                      variant="ghost"
                      size="icon"
                      title="Set as default"
                      onClick={() => setDefaultMutation.mutate(w.id)}
                      disabled={setDefaultMutation.isPending}
                    >
                      <Star size={15} />
                    </Button>
                  )}
                  <Button variant="ghost" size="icon" title="Edit region" onClick={() => openEditRegion(w)}>
                    <Pencil size={15} />
                  </Button>
                  {w.isBuiltIn ? (
                    <Button
                      variant="ghost"
                      size="icon"
                      title={w.isConnected ? "Disable built-in worker" : "Enable built-in worker"}
                      onClick={() => toggleBuiltinMutation.mutate(w.isConnected)}
                      disabled={toggleBuiltinMutation.isPending}
                      className={w.isConnected ? "text-green-500 hover:text-destructive" : "text-muted-foreground hover:text-green-500"}
                    >
                      <Power size={15} />
                    </Button>
                  ) : (
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => { if (confirm(`Remove worker "${w.name}"?`)) deleteMutation.mutate(w.id); }}
                      className="text-muted-foreground hover:text-destructive"
                    >
                      <Trash2 size={16} />
                    </Button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <Dialog open={modal === "create"} onOpenChange={(open) => { if (!open) closeModal(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Register Worker</DialogTitle>
            <DialogDescription>Add a remote worker process to run checks.</DialogDescription>
          </DialogHeader>
          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col gap-4">
            {createError && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle size={14} /> {createError}
              </div>
            )}
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Name</label>
              <Input value={workerName} onChange={(e) => setWorkerName(e.target.value)}
                required autoFocus placeholder="e.g. EU Worker" />
            </div>
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Region</label>
              <Input value={workerRegion} onChange={(e) => setWorkerRegion(e.target.value)}
                required placeholder="e.g. eu-west-1" />
              <p className="text-xs text-muted-foreground">Display label only — used in latency reports, not for routing checks.</p>
            </div>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} className="rounded" />
              <span className="text-sm">Set as default worker <span className="text-muted-foreground">(receives non-multi-region checks when the API worker is disabled)</span></span>
            </label>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={closeModal}>Cancel</Button>
              <Button type="submit" disabled={createMutation.isPending || !workerName.trim()}>
                {createMutation.isPending ? "Registering…" : "Register"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={modal === "token"} onOpenChange={(open) => { if (!open) closeModal(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Worker Token</DialogTitle>
            <DialogDescription>Copy this token now — it won't be shown again.</DialogDescription>
          </DialogHeader>
          <div className="rounded-xl bg-muted border border-border p-4 flex items-start gap-3">
            <div className="flex-1 min-w-0">
              <p className="text-xs text-muted-foreground mb-1">Set as <code className="font-mono">PIRO_WORKER_TOKEN</code> on your worker process:</p>
              <code className="text-sm font-mono break-all">{newToken}</code>
            </div>
            <Button variant="outline" size="icon" onClick={handleCopy} title="Copy">
              {copied ? <span className="text-xs text-green-600 font-medium">✓</span> : <Copy size={16} />}
            </Button>
          </div>
          <div className="flex items-center gap-2 rounded-xl bg-amber-500/10 border border-amber-500/30 px-4 py-3">
            <AlertCircle size={16} className="text-amber-600 shrink-0" />
            <p className="text-sm text-amber-800">Store this token securely. You won't be able to see it again.</p>
          </div>
          <DialogFooter>
            <Button onClick={closeModal}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={modal === "editRegion"} onOpenChange={(open) => { if (!open) closeModal(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Region</DialogTitle>
            <DialogDescription>Update the display region label for this worker.</DialogDescription>
          </DialogHeader>
          {editingWorker && (
            <form onSubmit={(e) => {
              e.preventDefault();
              updateRegionMutation.mutate({ id: editingWorker.id, region: editRegion });
            }} className="flex flex-col gap-4">
              {editError && (
                <div className="flex items-center gap-2 text-sm text-destructive">
                  <AlertCircle size={14} /> {editError}
                </div>
              )}
              <div className="flex flex-col gap-2">
                <label className="text-sm font-semibold">Region</label>
                <Input value={editRegion} onChange={(e) => setEditRegion(e.target.value)}
                  required autoFocus placeholder="e.g. us-east-1" />
              </div>
              <DialogFooter>
                <Button type="button" variant="outline" onClick={closeModal}>Cancel</Button>
                <Button type="submit" disabled={updateRegionMutation.isPending || !editRegion.trim()}>
                  {updateRegionMutation.isPending ? "Saving…" : "Save"}
                </Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
