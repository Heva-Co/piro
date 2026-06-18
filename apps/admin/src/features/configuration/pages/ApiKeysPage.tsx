import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Trash2, Plus, Copy, CheckCircle, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { authApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export default function ApiKeysPage() {
  const qc = useQueryClient();
  const { data: keys = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.API_KEYS,
    queryFn: authApi.apiKeys,
  });

  const [showCreate, setShowCreate] = useState(false);
  const [name, setName] = useState("");
  const [newSecret, setNewSecret] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [createError, setCreateError] = useState("");

  const createMutation = useMutation({
    mutationFn: () => authApi.createApiKey(name),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.API_KEYS });
      setNewSecret(data.secret);
      setName("");
      setShowCreate(false);
      setCreateError("");
    },
    onError: () => setCreateError("Failed to create API key."),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => authApi.deleteApiKey(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.API_KEYS }),
  });

  function handleCopy() {
    if (newSecret) {
      navigator.clipboard.writeText(newSecret);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }

  return (
    <AdminLayout title="API Keys">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between">
          <p className="text-sm text-gray-500">{keys.length} key{keys.length !== 1 ? "s" : ""}</p>
          <button
            onClick={() => { setShowCreate((v) => !v); setNewSecret(null); }}
            className="flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            <Plus size={16} /> Create API Key
          </button>
        </div>

        {newSecret && (
          <div className="rounded-lg border border-green-200 bg-green-50 p-4">
            <div className="flex items-center gap-2 text-sm font-medium text-green-800 mb-2">
              <CheckCircle size={16} /> API key created — copy it now, it won't be shown again.
            </div>
            <div className="flex items-center gap-2">
              <code className="flex-1 rounded bg-white border border-green-200 px-3 py-2 text-sm font-mono text-green-900 break-all">
                {newSecret}
              </code>
              <button
                onClick={handleCopy}
                className="rounded-md border border-green-300 bg-white px-3 py-2 text-sm text-green-700 hover:bg-green-100 flex items-center gap-1.5"
              >
                <Copy size={14} />
                {copied ? "Copied!" : "Copy"}
              </button>
            </div>
          </div>
        )}

        {showCreate && (
          <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
            <h3 className="text-sm font-semibold mb-3">New API Key</h3>
            <form
              onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}
              className="flex flex-col gap-3"
            >
              {createError && (
                <div className="flex items-center gap-2 text-sm text-red-600">
                  <AlertCircle size={14} /> {createError}
                </div>
              )}
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                placeholder="Key name (e.g. CI/CD pipeline)"
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                >
                  {createMutation.isPending ? "Creating…" : "Create"}
                </button>
                <button
                  type="button"
                  onClick={() => setShowCreate(false)}
                  className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50"
                >
                  Cancel
                </button>
              </div>
            </form>
          </div>
        )}

        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="border-b border-gray-200 bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Name</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Created</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Last Used</th>
                <th className="px-4 py-3 w-12"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && (
                <tr>
                  <td colSpan={4} className="px-4 py-6 text-center text-gray-400">Loading…</td>
                </tr>
              )}
              {!isLoading && keys.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-6 text-center text-gray-400">No API keys.</td>
                </tr>
              )}
              {keys.map((k) => (
                <tr key={k.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium">{k.name}</td>
                  <td className="px-4 py-3 text-gray-500">{new Date(k.createdAt).toLocaleString()}</td>
                  <td className="px-4 py-3 text-gray-500">
                    {k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleString() : "Never"}
                  </td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => {
                        if (confirm(`Delete API key "${k.name}"?`)) {
                          deleteMutation.mutate(k.id);
                        }
                      }}
                      className="rounded p-1 text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                    >
                      <Trash2 size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </AdminLayout>
  );
}
