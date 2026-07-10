import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { AlertCircle, CheckCircle, Save } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { TestButton } from "@/components/TestButton";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
    // Secrets are never returned by the API — the field always starts empty.
    // An empty value on save means "keep the existing secret" (see emailApi.update).
    setSmtpPassword("");
    setResendApiKey("");
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
              resendApiKey: resendApiKey || undefined,
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
    onError: (err: unknown) => {
      const message =
        axios.isAxiosError(err) && (err.response?.data?.detail || err.response?.data?.title)
          ? (err.response.data.detail ?? err.response.data.title)
          : "Failed to send test email.";
      setTestError(message);
    },
  });

  return (
    <div className="max-w-2xl">
      <PageHeader breadcrumbs={[{ label: "Email" }]} />
      <p className="text-muted-foreground text-sm -mt-4 mb-6">
        Configure the email provider used for notifications and invitations
      </p>

      {isLoading ? (
        <div className="rounded-xl border bg-card p-8 text-sm text-muted-foreground">Loading…</div>
      ) : (
        <div className="rounded-xl border bg-card p-8 flex flex-col gap-6">
          {/* Provider */}
          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold">Provider</label>
            <Select value={provider} onValueChange={(v) => v && setProvider(v as Provider)}>
              <SelectTrigger className="w-60">
                <SelectValue>{provider === "smtp" ? "SMTP" : "Resend"}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="smtp">SMTP</SelectItem>
                <SelectItem value="resend">Resend</SelectItem>
              </SelectContent>
            </Select>
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
                  <Input
                    value={smtpHost}
                    onChange={(e) => setSmtpHost(e.target.value)}
                    placeholder="smtp.example.com"
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-sm font-semibold">
                    Port <span className="text-destructive">*</span>
                  </label>
                  <Input
                    value={smtpPort}
                    onChange={(e) => setSmtpPort(e.target.value === "" ? "" : Number(e.target.value))}
                    type="number"
                    placeholder="587"
                  />
                </div>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">Username</label>
                <Input
                  value={smtpUsername}
                  onChange={(e) => setSmtpUsername(e.target.value)}
                  placeholder="user@example.com"
                  autoComplete="off"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">Password</label>
                <Input
                  value={smtpPassword}
                  onChange={(e) => setSmtpPassword(e.target.value)}
                  type="password"
                  placeholder={data?.hasSmtpPassword ? "········ (saved — leave blank to keep)" : "SMTP password"}
                  autoComplete="new-password"
                />
                {data?.hasSmtpPassword && (
                  <p className="text-xs text-muted-foreground">Leave blank to keep the existing password.</p>
                )}
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">
                  From address <span className="text-destructive">*</span>
                </label>
                <Input
                  value={smtpFrom}
                  onChange={(e) => setSmtpFrom(e.target.value)}
                  placeholder={`Piro <no-reply@example.com>`}
                />
              </div>

              <label className="flex items-center gap-2 text-sm">
                <Switch checked={smtpUseTls} onCheckedChange={setSmtpUseTls} />
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
                <Input
                  value={resendApiKey}
                  onChange={(e) => setResendApiKey(e.target.value)}
                  type="password"
                  placeholder={data?.hasResendApiKey ? "········ (saved — leave blank to keep)" : "re_..."}
                  autoComplete="new-password"
                />
                {data?.hasResendApiKey && (
                  <p className="text-xs text-muted-foreground">Leave blank to keep the existing key.</p>
                )}
                <p className="text-xs text-muted-foreground">Found in your Resend dashboard under API Keys.</p>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-semibold">
                  From address <span className="text-destructive">*</span>
                </label>
                <Input
                  value={resendFrom}
                  onChange={(e) => setResendFrom(e.target.value)}
                  placeholder={`Piro <no-reply@yourdomain.com>`}
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
            <TestButton
              onClick={() => testMutation.mutate()}
              loading={testMutation.isPending}
              disabled={saveMutation.isPending}
              label="Send Test Email"
              loadingLabel="Sending…"
            />
            <Button
              type="button"
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending || testMutation.isPending}
            >
              <Save size={14} />
              {saveMutation.isPending ? "Saving…" : "Save"}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
