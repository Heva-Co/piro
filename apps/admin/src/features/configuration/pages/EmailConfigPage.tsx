import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { FlaskConical, AlertCircle, CheckCircle } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { emailApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

type Provider = "smtp" | "resend";

export default function EmailConfigPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.EMAIL_CONFIG,
    queryFn: () => emailApi.get(),
  });

  const [provider, setProvider] = useState<Provider>("smtp");

  // SMTP fields
  const [smtpHost, setSmtpHost] = useState("");
  const [smtpPort, setSmtpPort] = useState<number | "">(587);
  const [smtpUsername, setSmtpUsername] = useState("");
  const [smtpPassword, setSmtpPassword] = useState("");
  const [smtpFrom, setSmtpFrom] = useState("");
  const [smtpUseTls, setSmtpUseTls] = useState(true);

  // Resend fields
  const [resendApiKey, setResendApiKey] = useState("");
  const [resendFrom, setResendFrom] = useState("");

  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [testMsg, setTestMsg] = useState("");
  const [testError, setTestError] = useState("");

  useEffect(() => {
    if (!data) return;
    setProvider((data.provider as Provider) || "smtp");
    setSmtpHost(data.smtpHost ?? "");
    setSmtpPort(data.smtpPort ?? 587);
    setSmtpUsername(data.smtpUsername ?? "");
    setSmtpFrom(data.smtpFrom ?? "");
    setSmtpUseTls(data.smtpUseTls ?? true);
    setResendFrom(data.resendFrom ?? "");
    if (data.hasResendApiKey) setResendApiKey("re_••••••••");
  }, [data]);

  const saveMutation = useMutation({
    mutationFn: () =>
      emailApi.update(
        provider === "smtp"
          ? {
              provider: "smtp",
              smtpHost,
              smtpPort: Number(smtpPort) || 587,
              smtpUsername: smtpUsername || undefined,
              smtpPassword: smtpPassword || undefined,
              smtpFrom,
              smtpUseTls,
            }
          : {
              provider: "resend",
              resendApiKey: (resendApiKey && resendApiKey !== "re_*••••••••") ? resendApiKey : undefined,
              resendFrom,
            }
      ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.EMAIL_CONFIG });
      setSuccess("Configuration saved.");
      setError("");
      setTimeout(() => setSuccess(""), 3000);
    },
    onError: () => setError("Failed to save configuration."),
  });

  const testMutation = useMutation({
    mutationFn: () => emailApi.test(),
    onSuccess: () => {
      setTestMsg("Test email sent to your account.");
      setTestError("");
      setTimeout(() => setTestMsg(""), 4000);
    },
    onError: () => setTestError("Failed to send test email."),
  });

  return (
    <AdminLayout title="Email">
      <div className="max-w-2xl">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Email</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Configure the email provider used for notifications and invitations
          </p>
        </div>

        {isLoading ? (
          <div className="rounded-xl border bg-card p-8 text-sm text-muted-foreground">Loading…</div>
        ) : (
          <div className="rounded-xl border bg-card p-8 flex flex-col gap-6">
            {/* Provider */}
            <div className="flex flex-col gap-2">
              <label className="text-sm font-semibold">Provider</label>
              <select
                value={provider}
                onChange={(e) => setProvider(e.target.value as Provider)}
                className="w-60 rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="smtp">SMTP</option>
                <option value="resend">Resend</option>
              </select>
              <p className="text-xs text-muted-foreground">
                If no configuration is saved here, the app falls back to{" "}
                <code className="bg-muted px-1 rounded text-xs">Email:*</code> environment variables.
              </p>
            </div>

            {/* ── SMTP fields ── */}
            {provider === "smtp" && (
              <>
                <div className="grid grid-cols-2 gap-4">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-semibold">
                      Host <span className="text-destructive">*</span>
                    </label>
                    <input
                      value={smtpHost}
                      onChange={(e) => setSmtpHost(e.target.value)}
                      placeholder="smtp.example.com"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-semibold">
                      Port <span className="text-destructive">*</span>
                    </label>
                    <input
                      value={smtpPort}
                      onChange={(e) => setSmtpPort(e.target.value === "" ? "" : Number(e.target.value))}
                      type="number"
                      placeholder="587"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">Username</label>
                  <input
                    value={smtpUsername}
                    onChange={(e) => setSmtpUsername(e.target.value)}
                    placeholder="user@example.com"
                    autoComplete="off"
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">Password</label>
                  <input
                    value={smtpPassword}
                    onChange={(e) => setSmtpPassword(e.target.value)}
                    type="password"
                    placeholder={data?.hasSmtpPassword ? "········ (saved — leave blank to keep)" : "SMTP password"}
                    autoComplete="new-password"
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                  {data?.hasSmtpPassword && (
                    <p className="text-xs text-muted-foreground">Leave blank to keep the existing password.</p>
                  )}
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">
                    From address <span className="text-destructive">*</span>
                  </label>
                  <input
                    value={smtpFrom}
                    onChange={(e) => setSmtpFrom(e.target.value)}
                    placeholder={`Piro <no-reply@example.com>`}
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>

                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={smtpUseTls}
                    onChange={(e) => setSmtpUseTls(e.target.checked)}
                    className="size-4 rounded"
                  />
                  Use SSL/TLS
                </label>
              </>
            )}

            {/* ── Resend fields ── */}
            {provider === "resend" && (
              <>
                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">
                    API Key <span className="text-destructive">*</span>
                  </label>
                  <input
                    value={resendApiKey}
                    onChange={(e) => setResendApiKey(e.target.value)}
                    type="password"
                    placeholder="re_..."
                    autoComplete="new-password"
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                  <p className="text-xs text-muted-foreground">Found in your Resend dashboard under API Keys.</p>
                </div>

                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">
                    From address <span className="text-destructive">*</span>
                  </label>
                  <input
                    value={resendFrom}
                    onChange={(e) => setResendFrom(e.target.value)}
                    placeholder={`Piro <no-reply@yourdomain.com>`}
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                  />
                  <p className="text-xs text-muted-foreground">Must be a verified domain in Resend.</p>
                </div>
              </>
            )}

            {/* Feedback */}
            {error && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle size={15} /> {error}
              </div>
            )}
            {success && (
              <div className="flex items-center gap-2 text-sm text-green-600">
                <CheckCircle size={15} /> {success}
              </div>
            )}
            {testError && (
              <div className="flex items-center gap-2 text-sm text-destructive">
                <AlertCircle size={15} /> {testError}
              </div>
            )}
            {testMsg && (
              <div className="flex items-center gap-2 text-sm text-green-600">
                <CheckCircle size={15} /> {testMsg}
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-between pt-2 border-t border-border">
              <button
                type="button"
                onClick={() => testMutation.mutate()}
                disabled={testMutation.isPending || saveMutation.isPending}
                className="flex items-center gap-2 rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
              >
                <FlaskConical size={16} />
                {testMutation.isPending ? "Sending…" : "Send Test Email"}
              </button>
              <button
                type="button"
                onClick={() => saveMutation.mutate()}
                disabled={saveMutation.isPending || testMutation.isPending}
                className="rounded-lg bg-foreground text-background px-5 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {saveMutation.isPending ? "Saving…" : "Save"}
              </button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
