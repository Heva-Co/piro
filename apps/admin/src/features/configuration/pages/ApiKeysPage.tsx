import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Trash2, Plus, Copy, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { authApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

// Modal backdrop
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
    <button
      type="button"
      onClick={onClose}
      className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-xl leading-none"
    >
      ×
    </button>
  );
}

export default function ApiKeysPage() {
  const qc = useQueryClient();
  const { data: keys = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.API_KEYS,
    queryFn: authApi.apiKeys,
  });

  const [modal, setModal] = useState<"none" | "create" | "secret">("none");
  const [name, setName] = useState("");
  const [newSecret, setNewSecret] = useState<string>("");
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
    if (confirm(`Delete API key "${keyName}"?`)) {
      deleteMutation.mutate(id);
    }
  }

  return (
    <AdminLayout title="API Keys">
      {/* Header */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">API Keys</h1>
          <p className="text-sm text-gray-500 mt-0.5">Manage API keys for programmatic access to the Piro API.</p>
        </div>
        <button
          onClick={openCreate}
          className="flex items-center gap-2 rounded-lg bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800"
        >
          <Plus size={15} /> New API Key
        </button>
      </div>

      {/* Keys list */}
      <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
        {isLoading && (
          <div className="py-16 text-center text-sm text-gray-400">Loading…</div>
        )}
        {!isLoading && keys.length === 0 && (
          <div className="py-16 text-center text-sm text-gray-400">
            No API keys yet. Create one to get started.
          </div>
        )}
        {keys.map((k, i) => (
          <div
            key={k.id}
            className={`flex items-center justify-between px-5 py-4 ${i > 0 ? "border-t border-gray-100" : ""}`}
          >
            <div className="flex flex-col gap-0.5">
              <span className="text-sm font-semibold text-gray-900">{k.name}</span>
              <span className="text-xs font-mono text-gray-400">{k.maskedKey}</span>
            </div>
            <div className="flex items-center gap-4">
              <span className="rounded-full bg-gray-900 px-3 py-0.5 text-xs font-semibold text-white">ACTIVE</span>
              <span className="text-sm text-gray-500">
                {new Date(k.createdAt).toLocaleDateString("en-US", { month: "numeric", day: "numeric", year: "numeric" })}
              </span>
              <button
                onClick={() => handleDelete(k.id, k.name)}
                className="text-gray-400 hover:text-red-600 transition-colors"
              >
                <Trash2 size={16} />
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Create modal */}
      {modal === "create" && (
        <Modal onClose={closeModal}>
          <ModalClose onClose={closeModal} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">New API Key</h2>
          <p className="text-sm text-gray-500 mb-5">Give your key a descriptive name.</p>
          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
            {createError && (
              <div className="flex items-center gap-2 text-sm text-red-600 mb-3">
                <AlertCircle size={14} /> {createError}
              </div>
            )}
            <label className="block text-sm font-semibold text-gray-900 mb-2">Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              autoFocus
              placeholder="e.g. CI/CD Pipeline"
              className="w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-gray-400"
            />
            <div className="flex justify-end gap-3 mt-6">
              <button
                type="button"
                onClick={closeModal}
                className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={createMutation.isPending || !name.trim()}
                className="rounded-lg bg-gray-600 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50"
              >
                {createMutation.isPending ? "Creating…" : "Create"}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Secret reveal modal */}
      {modal === "secret" && (
        <Modal onClose={closeModal}>
          <ModalClose onClose={closeModal} />
          <h2 className="text-xl font-bold text-gray-900 mb-1">New API Key</h2>
          <p className="text-sm text-gray-500 mb-5">Copy your API key now — it won't be shown again.</p>

          <div className="rounded-xl bg-gray-50 border border-gray-200 p-4 flex items-start gap-3 mb-4">
            <div className="flex-1 min-w-0">
              <p className="text-xs text-gray-500 mb-1">Your new API key:</p>
              <code className="text-sm font-mono text-gray-900 break-all">{newSecret}</code>
            </div>
            <button
              onClick={handleCopy}
              title="Copy"
              className="flex-shrink-0 rounded-lg border border-gray-200 bg-white p-2 hover:bg-gray-100 transition-colors"
            >
              {copied
                ? <span className="text-xs text-green-600 font-medium px-1">Copied!</span>
                : <Copy size={16} className="text-gray-500" />}
            </button>
          </div>

          <div className="flex items-center gap-2 rounded-xl bg-yellow-50 border border-yellow-200 px-4 py-3 mb-6">
            <AlertCircle size={16} className="text-yellow-600 shrink-0" />
            <p className="text-sm text-yellow-800">Store this key securely. You won't be able to see it again.</p>
          </div>

          <div className="flex justify-end">
            <button
              onClick={closeModal}
              className="rounded-lg bg-gray-900 px-5 py-2 text-sm font-medium text-white hover:bg-gray-800"
            >
              Done
            </button>
          </div>
        </Modal>
      )}
    </AdminLayout>
  );
}
