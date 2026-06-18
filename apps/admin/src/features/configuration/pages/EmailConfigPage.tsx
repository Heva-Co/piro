import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle, AlertCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { emailApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { useAuth } from "@/hooks/useAuth";

export default function EmailConfigPage() {
  const qc = useQueryClient();
  const { user } = useAuth();
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.EMAIL_CONFIG,
    queryFn: emailApi.get,
  });

  const [host, setHost] = useState("");
  const [port, setPort] = useState(587);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [from, setFrom] = useState("");
  const [useSsl, setUseSsl] = useState(false);

  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");
  const [testResult, setTestResult] = useState<"success" | "error" | null>(null);
  const [testing, setTesting] = useState(false);

  useEffect(() => {
    if (data) {
      setHost(data.host ?? "");
      setPort(data.port ?? 587);
      setUsername(data.username ?? "");
      setFrom(data.from ?? "");
      setUseSsl(data.useSsl ?? false);
    }
  }, [data]);

  const updateMutation = useMutation({
    mutationFn: () =>
      emailApi.update({ host, port, username, ...(password ? { password } : {}), from, useSsl }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.EMAIL_CONFIG });
      setSuccess(true);
      setError("");
      setTimeout(() => setSuccess(false), 3000);
    },
    onError: () => setError("Failed to save email configuration."),
  });

  async function handleTest() {
    const to = prompt("Send test email to:", user?.email ?? "");
    if (!to) return;
    setTesting(true);
    setTestResult(null);
    try {
      await emailApi.test(to);
      setTestResult("success");
    } catch {
      setTestResult("error");
    } finally {
      setTesting(false);
    }
  }

  return (
    <AdminLayout title="Email Configuration">
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
                <CheckCircle size={16} /> Test email sent successfully.
              </div>
            )}
            {testResult === "error" && (
              <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                <AlertCircle size={16} /> Test email failed. Check your configuration.
              </div>
            )}

            <div className="flex gap-3">
              <div className="flex flex-col gap-1.5 flex-1">
                <label className="text-sm font-medium">SMTP Host</label>
                <input
                  type="text"
                  value={host}
                  onChange={(e) => setHost(e.target.value)}
                  required
                  placeholder="smtp.example.com"
                  className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>
              <div className="flex flex-col gap-1.5 w-28">
                <label className="text-sm font-medium">Port</label>
                <input
                  type="number"
                  value={port}
                  onChange={(e) => setPort(Number(e.target.value))}
                  required
                  className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Username</label>
              <input
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Password</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Leave blank to keep existing"
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">From Address</label>
              <input
                type="email"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
                required
                placeholder="noreply@example.com"
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <label className="flex items-center gap-2 cursor-pointer">
              <div className="relative">
                <input
                  type="checkbox"
                  checked={useSsl}
                  onChange={(e) => setUseSsl(e.target.checked)}
                  className="sr-only peer"
                />
                <div className="w-9 h-5 rounded-full bg-gray-200 peer-checked:bg-indigo-600 transition-colors" />
                <div className="absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform peer-checked:translate-x-4" />
              </div>
              <span className="text-sm font-medium">Use SSL/TLS</span>
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
                {testing ? "Sending…" : "Send Test Email"}
              </button>
            </div>
          </form>
        )}
      </div>
    </AdminLayout>
  );
}
