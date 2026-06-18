import { useState, useEffect } from "react";
import { useNavigate, Navigate } from "react-router-dom";
import { AlertCircle } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { setupApi, emailApi, authApi } from "@/lib/api";
import { setStoredAuth } from "@/lib/axios";
import { ROUTES } from "@/constants/routes";

type Step = 1 | 2;
type EmailProvider = "smtp" | "resend";

export default function SetupPage() {
  const navigate = useNavigate();
  const { isAuthenticated, loginWithTokens } = useAuth();

  // Check if setup is already done
  const [setupDone, setSetupDone] = useState(false);
  useEffect(() => {
    setupApi.status().then((s) => {
      if (s.isComplete) setSetupDone(true);
    }).catch(() => {});
  }, []);

  const [step, setStep] = useState<Step>(1);

  // Step 1
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [step1Loading, setStep1Loading] = useState(false);
  const [step1Error, setStep1Error] = useState("");

  // Step 2
  const [provider, setProvider] = useState<EmailProvider>("smtp");
  const [smtpHost, setSmtpHost] = useState("");
  const [smtpPort, setSmtpPort] = useState<number | "">(587);
  const [smtpUsername, setSmtpUsername] = useState("");
  const [smtpPassword, setSmtpPassword] = useState("");
  const [smtpFrom, setSmtpFrom] = useState("");
  const [smtpUseSsl, setSmtpUseSsl] = useState(true);
  const [resendApiKey, setResendApiKey] = useState("");
  const [resendFrom, setResendFrom] = useState("");
  const [emailSaving, setEmailSaving] = useState(false);
  const [emailTesting, setEmailTesting] = useState(false);
  const [emailError, setEmailError] = useState("");
  const [emailSuccess, setEmailSuccess] = useState("");

  if (setupDone && isAuthenticated) {
    return <Navigate to={ROUTES.DASHBOARD} replace />;
  }

  async function handleStep1(e: React.FormEvent) {
    e.preventDefault();
    setStep1Error("");
    setStep1Loading(true);
    try {
      await setupApi.complete({ name, email, password });
      // Auto sign-in after setup
      const { accessToken, refreshToken, expiresIn, user } = await authApi.signIn(email, password);
      setStoredAuth({ accessToken, refreshToken, expiresAt: Date.now() + expiresIn * 1000 });
      loginWithTokens({ accessToken, refreshToken, expiresAt: Date.now() + expiresIn * 1000 }, user);
      setStep(2);
    } catch (err: unknown) {
      const msg =
        err instanceof Error ? err.message : "Setup failed. Please try again.";
      setStep1Error(msg);
    } finally {
      setStep1Loading(false);
    }
  }

  async function handleSaveEmail() {
    setEmailSaving(true);
    setEmailError("");
    setEmailSuccess("");
    try {
      const config =
        provider === "smtp"
          ? { host: smtpHost, port: Number(smtpPort) || 587, username: smtpUsername || undefined, from: smtpFrom, useSsl: smtpUseSsl }
          : { host: "api.resend.com", port: 443, username: resendApiKey, from: resendFrom, useSsl: true };
      await emailApi.update(config);
      navigate(ROUTES.DASHBOARD, { replace: true });
    } catch {
      setEmailError("Failed to save email configuration.");
    } finally {
      setEmailSaving(false);
    }
  }

  async function handleTestEmail() {
    setEmailTesting(true);
    setEmailError("");
    setEmailSuccess("");
    try {
      await emailApi.test(email);
      setEmailSuccess("Test email sent!");
    } catch {
      setEmailError("Failed to send test email.");
    } finally {
      setEmailTesting(false);
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-background">
      <div className="w-full max-w-sm flex flex-col gap-6">
        <div className="text-center">
          <a href="/" className="text-2xl font-bold tracking-tight">
            Piro
          </a>
          <p className="text-muted-foreground text-sm mt-1">
            {step === 1 ? "Create your owner account to get started" : "Configure your email provider"}
          </p>
        </div>

        {/* Step indicator */}
        <div className="flex items-center gap-2 justify-center">
          <div className="flex items-center gap-1.5 text-xs">
            <span
              className={`size-5 rounded-full flex items-center justify-center text-[10px] font-semibold ${
                step === 1 ? "bg-primary text-primary-foreground" : "bg-primary/20 text-primary"
              }`}
            >
              1
            </span>
            <span className={step === 1 ? "font-medium" : "text-muted-foreground"}>Account</span>
          </div>
          <div className="h-px w-6 bg-border" />
          <div className="flex items-center gap-1.5 text-xs">
            <span
              className={`size-5 rounded-full flex items-center justify-center text-[10px] font-semibold ${
                step === 2 ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"
              }`}
            >
              2
            </span>
            <span className={step === 2 ? "font-medium" : "text-muted-foreground"}>Email</span>
          </div>
        </div>

        <div className="rounded-2xl border bg-card p-6 shadow-sm">
          {step === 1 ? (
            <form onSubmit={handleStep1} className="flex flex-col gap-4">
              {step1Error && (
                <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
                  <AlertCircle className="size-4 mt-0.5 shrink-0" />
                  {step1Error}
                </div>
              )}

              <div className="flex flex-col gap-1.5">
                <label htmlFor="name" className="text-sm font-medium">
                  Your name
                </label>
                <input
                  id="name"
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Jane Smith"
                  required
                  autoComplete="name"
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label htmlFor="email" className="text-sm font-medium">
                  Email
                </label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  required
                  autoComplete="email"
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label htmlFor="password" className="text-sm font-medium">
                  Password
                </label>
                <input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Minimum 8 characters"
                  required
                  minLength={8}
                  autoComplete="new-password"
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <button
                type="submit"
                disabled={step1Loading}
                className="mt-2 w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {step1Loading ? "Creating account…" : "Create account & continue"}
              </button>
            </form>
          ) : (
            <div className="flex flex-col gap-4">
              {emailError && (
                <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
                  <AlertCircle className="size-4 mt-0.5 shrink-0" />
                  {emailError}
                </div>
              )}
              {emailSuccess && (
                <div className="rounded-lg border border-green-200 bg-green-50 dark:bg-green-950 dark:border-green-800 px-4 py-3 text-sm text-green-800 dark:text-green-300">
                  {emailSuccess}
                </div>
              )}

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-medium">Provider</label>
                <select
                  value={provider}
                  onChange={(e) => setProvider(e.target.value as EmailProvider)}
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                >
                  <option value="smtp">SMTP</option>
                  <option value="resend">Resend</option>
                </select>
              </div>

              {provider === "smtp" && (
                <>
                  <div className="grid grid-cols-2 gap-2">
                    <div className="flex flex-col gap-1.5">
                      <label className="text-sm font-medium">Host</label>
                      <input
                        value={smtpHost}
                        onChange={(e) => setSmtpHost(e.target.value)}
                        placeholder="smtp.example.com"
                        className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label className="text-sm font-medium">Port</label>
                      <input
                        value={smtpPort}
                        onChange={(e) =>
                          setSmtpPort(e.target.value === "" ? "" : Number(e.target.value))
                        }
                        type="number"
                        placeholder="587"
                        className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-medium">Username</label>
                    <input
                      value={smtpUsername}
                      onChange={(e) => setSmtpUsername(e.target.value)}
                      placeholder="user@example.com"
                      autoComplete="off"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-medium">Password</label>
                    <input
                      value={smtpPassword}
                      onChange={(e) => setSmtpPassword(e.target.value)}
                      type="password"
                      placeholder="SMTP password"
                      autoComplete="new-password"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-medium">From address</label>
                    <input
                      value={smtpFrom}
                      onChange={(e) => setSmtpFrom(e.target.value)}
                      placeholder="no-reply@example.com"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>

                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={smtpUseSsl}
                      onChange={(e) => setSmtpUseSsl(e.target.checked)}
                      className="size-4 rounded"
                    />
                    Use SSL/TLS
                  </label>
                </>
              )}

              {provider === "resend" && (
                <>
                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-medium">API Key</label>
                    <input
                      value={resendApiKey}
                      onChange={(e) => setResendApiKey(e.target.value)}
                      type="password"
                      placeholder="re_..."
                      autoComplete="new-password"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label className="text-sm font-medium">From address</label>
                    <input
                      value={resendFrom}
                      onChange={(e) => setResendFrom(e.target.value)}
                      placeholder="no-reply@yourdomain.com"
                      className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </>
              )}

              <button
                onClick={handleTestEmail}
                disabled={emailTesting || emailSaving}
                type="button"
                className="w-full rounded-lg border py-2.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
              >
                {emailTesting ? "Sending…" : "Send test email"}
              </button>

              <button
                onClick={handleSaveEmail}
                disabled={emailSaving || emailTesting}
                type="button"
                className="w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
              >
                {emailSaving ? "Saving…" : "Save & go to dashboard"}
              </button>

              <button
                onClick={() => navigate(ROUTES.DASHBOARD, { replace: true })}
                type="button"
                className="w-full text-center text-sm text-muted-foreground hover:text-foreground transition-colors"
              >
                Skip for now
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
