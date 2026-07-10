import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Trash2, Plus, Copy, AlertCircle, KeyRound } from "lucide-react";
import { authApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
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

type ModalState = "none" | "create" | "secret";

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("en-US", { month: "numeric", day: "numeric", year: "numeric" });
}

export default function ApiKeysPage() {
  const qc = useQueryClient();
  const { data: keys = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.API_KEYS,
    queryFn: authApi.apiKeys,
  });

  const [modal, setModal] = useState<ModalState>("none");
  const [name, setName] = useState("");
  const [newSecret, setNewSecret] = useState("");
  const [copied, setCopied] = useState(false);
  const [createError, setCreateError] = useState("");

  const createMutation = useMutation({
    mutationFn: () => authApi.createApiKey(name),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.API_KEYS });
      setNewSecret(data.rawKey ?? "");
      setName("");
      setCreateError("");
      setModal("secret");
    },
    onError: () => setCreateError("Failed to create API key."),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => authApi.deleteApiKey(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.API_KEYS }),
  });

  function openCreate() {
    setName("");
    setCreateError("");
    setModal("create");
  }

  function closeModal() {
    setModal("none");
    setNewSecret("");
    setCopied(false);
  }

  function handleCopy() {
    navigator.clipboard.writeText(newSecret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  function handleDelete(id: number, keyName: string) {
    if (confirm(`Revoke API key "${keyName}"? This cannot be undone.`)) {
      deleteMutation.mutate(id);
    }
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[{ label: "API Keys" }]}
        subheader="Manage API keys for programmatic access to the Piro API."
        actions={
          <Button onClick={openCreate}>
            <Plus size={15} /> New API Key
          </Button>
        }
      />

      <div className="rounded-xl border border-border bg-card overflow-hidden">
        {isLoading && (
          <div className="py-16 text-center text-sm text-muted-foreground">Loading…</div>
        )}
        {!isLoading && keys.length === 0 && (
          <div className="py-14 flex flex-col items-center gap-3">
            <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center">
              <KeyRound size={20} className="text-muted-foreground" />
            </div>
            <div className="text-center">
              <p className="text-sm font-semibold">No API keys yet</p>
              <p className="text-sm text-muted-foreground">Create one to get started.</p>
            </div>
            <Button variant="outline" onClick={openCreate}>
              <Plus size={15} /> New API Key
            </Button>
          </div>
        )}
        {keys.map((k, i) => (
          <div
            key={k.id}
            className={`flex items-center gap-4 px-5 py-4 ${i > 0 ? "border-t border-border" : ""}`}
          >
            <div className="flex-1 min-w-0">
              <p className="text-sm font-semibold">{k.name}</p>
              <p className="text-xs font-mono text-muted-foreground">{k.maskedKey}</p>
            </div>
            <div className="flex items-center gap-4">
              <span
                className={
                  k.status === "Active"
                    ? "rounded-full bg-green-500/15 text-green-600 dark:text-green-400 px-3 py-0.5 text-xs font-semibold"
                    : "rounded-full bg-muted text-muted-foreground px-3 py-0.5 text-xs font-semibold"
                }
              >
                {k.status}
              </span>
              <span className="text-sm text-muted-foreground">
                {k.lastUsedAt ? `Last used ${formatDate(k.lastUsedAt)}` : "Never used"}
              </span>
              <span className="text-sm text-muted-foreground">{formatDate(k.createdAt)}</span>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => handleDelete(k.id, k.name)}
                className="text-muted-foreground hover:text-destructive"
              >
                <Trash2 size={16} />
              </Button>
            </div>
          </div>
        ))}
      </div>

      <Dialog open={modal === "create"} onOpenChange={(open) => { if (!open) closeModal(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New API Key</DialogTitle>
            <DialogDescription>Give your key a descriptive name.</DialogDescription>
          </DialogHeader>
          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col gap-4">
            {createError && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle size={14} /> {createError}
              </div>
            )}
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Name</label>
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                autoFocus
                placeholder="e.g. CI/CD Pipeline"
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={closeModal}>Cancel</Button>
              <Button type="submit" disabled={createMutation.isPending || !name.trim()}>
                {createMutation.isPending ? "Creating…" : "Create"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={modal === "secret"} onOpenChange={(open) => { if (!open) closeModal(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New API Key</DialogTitle>
            <DialogDescription>Copy your API key now — it won't be shown again.</DialogDescription>
          </DialogHeader>

          <div className="rounded-xl bg-muted border border-border p-4 flex items-start gap-3">
            <div className="flex-1 min-w-0">
              <p className="text-xs text-muted-foreground mb-1">Your new API key:</p>
              <code className="text-sm font-mono break-all">{newSecret}</code>
            </div>
            <Button variant="outline" size="icon" onClick={handleCopy} title="Copy">
              {copied
                ? <span className="text-xs text-green-600 font-medium px-1">Copied!</span>
                : <Copy size={16} />}
            </Button>
          </div>

          <div className="flex items-center gap-2 rounded-xl bg-amber-500/10 border border-amber-500/30 px-4 py-3">
            <AlertCircle size={16} className="text-amber-600 dark:text-amber-400 shrink-0" />
            <p className="text-sm text-amber-800 dark:text-amber-300">
              Store this key securely. You won't be able to see it again.
            </p>
          </div>

          <DialogFooter>
            <Button onClick={closeModal}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
