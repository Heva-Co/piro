import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { oidcApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export default function SsoPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.OIDC_CONFIGS,
    queryFn: oidcApi.get,
  });

  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [ssoOnly, setSsoOnly] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");
  const [testResult, setTestResult] = useState<"success" | "error" | null>(null);
  const [testing, setTesting] = useState(false);

  useEffect(() => {
    if (data) {
      setClientId(data.clientId ?? "");
      setSsoOnly(!data.isActive ? false : false); // isActive reflects ssoOnly via mode
    }
  }, [data]);

  const updateMutation = useMutation({
    mutationFn: () => oidcApi.update({ clientId, ...(clientSecret ? { clientSecret } : {}) }),
    onSuccess: async () => {
      await oidcApi.setSsoMode(ssoOnly ? "sso_only" : "normal");
      qc.invalidateQueries({ queryKey: QUERY_KEYS.OIDC_CONFIGS });
      setSuccess(true);
      setError("");
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: () => setError("Failed to save SSO configuration."),
  });

  async function handleTest() {
    setTesting(true);
    setTestResult(null);
    try {
      await oidcApi.test();
      setTestResult("success");
    } catch {
      setTestResult("error");
    } finally {
      setTesting(false);
    }
  }

  return (
    <AdminLayout title="SSO / OIDC Configuration">
      <div className="max-w-xl">
        {isLoading ? (
          <p className="text-gray-400">Loading…</p>
        ) : (
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
            {testResult === "success" && (
              <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
                <CheckCircle size={16} /> SSO connection test successful.
              </div>
            )}
            {testResult === "error" && (
              <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                <AlertCircle size={16} /> SSO connection test failed.
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Client ID</label>
              <input
                type="text"
                value={clientId}
                onChange={(e) => setClientId(e.target.value)}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Client Secret</label>
              <input
                type="password"
                value={clientSecret}
                onChange={(e) => setClientSecret(e.target.value)}
                placeholder="Leave blank to keep existing"
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <label className="flex items-center gap-2 cursor-pointer">
              <div className="relative">
                <input
                  type="checkbox"
                  checked={ssoOnly}
                  onChange={(e) => setSsoOnly(e.target.checked)}
                  className="sr-only peer"
                />
                <div className="w-9 h-5 rounded-full bg-gray-200 peer-checked:bg-indigo-600 transition-colors" />
                <div className="absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform peer-checked:translate-x-4" />
              </div>
              <span className="text-sm font-medium">SSO-only mode (disable password login)</span>
            </label>

            <div className="flex items-center gap-3">
              <button
                type="submit"
                disabled={updateMutation.isPending}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
              >
                {updateMutation.isPending ? "Saving…" : "Save Changes"}
              </button>
              <button
                type="button"
                onClick={handleTest}
                disabled={testing}
                className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
              >
                {testing ? "Testing…" : "Test Connection"}
              </button>
            </div>
          </form>
        )}
      </div>
    </AdminLayout>
  );
}
