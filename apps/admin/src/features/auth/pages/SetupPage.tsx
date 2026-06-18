import { useState, useEffect } from "react";
import { useNavigate, Navigate } from "react-router-dom";
import { AlertCircle, Check } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { setupApi, emailApi, authApi, siteApi } from "@/lib/api";
import { setStoredAuth } from "@/lib/axios";
import { ROUTES } from "@/constants/routes";

type Step = 1 | 2 | 3;
type EmailProvider = "smtp" | "resend";

export default function SetupPage() {
  const navigate = useNavigate();
  const { isAuthenticated, loginWithTokens } = useAuth();

  const [setupDone, setSetupDone] = useState(false);
  useEffect(() => {
    setupApi.status().then((s) => {
      if (s.isComplete) setSetupDone(true);
    }).catch(() => {});
  }, []);

  const [step, setStep] = useState<Step>(1);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  // Step 1 — General config
  const [siteName, setSiteName] = useState("Piro");
  const [siteUrl, setSiteUrl] = useState(() => window.location.origin);

  // Step 2 — Email
  const [provider, setProvider] = useState<EmailProvider>("smtp");
  const [smtpHost, setSmtpHost] = useState("");
  const [smtpPort, setSmtpPort] = useState<number | "">(587);
  const [smtpUsername, setSmtpUsername] = useState("");
  const [smtpPassword, setSmtpPassword] = useState("");
  const [smtpFrom, setSmtpFrom] = useState("");
  const [smtpUseSsl, setSmtpUseSsl] = useState(true);
  const [resendApiKey, setResendApiKey] = useState("");
  const [resendFrom, setResendFrom] = useState("");

  // Step 3 — User
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  if (setupDone && isAuthenticated) {
    return <Navigate to={ROUTES.DASHBOARD} replace />;
  }

  function handleStep1(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setStep(2);
  }

  function handleStep2(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setStep(3);
  }

  async function handleStep3(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setSubmitting(true);
    try {
      // 1. Complete setup (creates owner user)
      await setupApi.complete({ name, email, password });

      // 2. Auto sign-in
      const { accessToken, refreshToken, expiresIn, user } = await authApi.signIn(email, password);
      const auth = { accessToken, refreshToken, expiresAt: Date.now() + expiresIn * 1000 };
      setStoredAuth(auth);
      loginWithTokens(auth, user);

      // 3. Save site config
      await siteApi.update({ title: siteName, url: siteUrl || undefined });

      // 4. Save email config (skip if empty)
      const hasEmailConfig = provider === "resend" ? resendApiKey : smtpHost;
      if (hasEmailConfig) {
        const emailConfig = provider === "smtp"
          ? { host: smtpHost, port: Number(smtpPort) || 587, username: smtpUsername || undefined, from: smtpFrom, useSsl: smtpUseSsl }
          : { host: "api.resend.com", port: 443, username: resendApiKey, from: resendFrom, useSsl: true };
        await emailApi.update(emailConfig);
      }

      navigate(ROUTES.DASHBOARD, { replace: true });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Setup failed. Please try again.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  }

  const steps = [
    { n: 1, label: "General" },
    { n: 2, label: "Email" },
    { n: 3, label: "Account" },
  ];

  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-background">
      <div className="w-full max-w-md flex flex-col gap-6">
        <div className="text-center">
          <a href="/" className="text-2xl font-bold tracking-tight">Piro</a>
          <p className="text-muted-foreground text-sm mt-1">Initial setup</p>
        </div>

        {/* Step indicator */}
        <div className="flex items-center justify-center gap-2">
          {steps.map((s, i) => (
            <div key={s.n} className="flex items-center gap-2">
              <div className="flex items-center gap-1.5 text-xs">
                <span className={`size-5 rounded-full flex items-center justify-center text-[10px] font-semibold transition-colors ${
                  step > s.n
                    ? "bg-primary text-primary-foreground"
                    : step === s.n
                    ? "bg-primary text-primary-foreground"
                    : "bg-muted text-muted-foreground"
                }`}>
                  {step > s.n ? <Check size={10} /> : s.n}
                </span>
                <span className={step === s.n ? "font-medium" : "text-muted-foreground"}>{s.label}</span>
              </div>
              {i < steps.length - 1 && <div className="h-px w-6 bg-border" />}
            </div>
          ))}
        </div>

        <div className="rounded-2xl border bg-card p-6 shadow-sm">
          {error && (
            <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive mb-4">
              <AlertCircle className="size-4 mt-0.5 shrink-0" />
              {error}
            </div>
          )}

          {/* ── Step 1: General config ── */}
          {step === 1 && (
            <form onSubmit={handleStep1} className="flex flex-col gap-4">
              <div>
                <h2 className="text-sm font-semibold mb-0.5">General configuration</h2>
                <p className="text-xs text-muted-foreground">Basic info about your status page.</p>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-medium">Site name</label>
                <input
                  value={siteName}
                  onChange={(e) => setSiteName(e.target.value)}
                  placeholder="Piro"
                  required
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="text-sm font-medium">Site URL</label>
                <input
                  value={siteUrl}
                  onChange={(e) => setSiteUrl(e.target.value)}
                  placeholder="https://status.yourdomain.com"
                  type="url"
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                />
                <p className="text-xs text-muted-foreground">Used for OIDC redirect URIs and email links.</p>
              </div>

              <button
                type="submit"
                className="mt-2 w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 transition-opacity"
              >
                Continue
              </button>
            </form>
          )}

          {/* ── Step 2: Email config ── */}
          {step === 2 && (
            <form onSubmit={handleStep2} className="flex flex-col gap-4">
              <div>
                <h2 className="text-sm font-semibold mb-0.5">Email provider</h2>
                <p className="text-xs text-muted-foreground">Used to send invite and alert emails. You can skip and configure later.</p>
              </div>

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
                        onChange={(e) => setSmtpPort(e.target.value === "" ? "" : Number(e.target.value))}
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

              <div className="flex gap-2 mt-2">
                <button
                  type="button"
                  onClick={() => setStep(1)}
                  className="flex-1 rounded-lg border py-2.5 text-sm font-medium hover:bg-muted transition-colors"
                >
                  Back
                </button>
                <button
                  type="submit"
                  className="flex-1 rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 transition-opacity"
                >
                  Continue
                </button>
              </div>

              <button
                type="button"
                onClick={() => setStep(3)}
                className="w-full text-center text-sm text-muted-foreground hover:text-foreground transition-colors"
              >
                Skip for now
              </button>
            </form>
          )}

          {/* ── Step 3: Admin account ── */}
          {step === 3 && (
            <form onSubmit={handleStep3} className="flex flex-col gap-4">
              <div>
                <h2 className="text-sm font-semibold mb-0.5">Owner account</h2>
                <p className="text-xs text-muted-foreground">Create the first administrator account.</p>
              </div>

              <div className="flex flex-col gap-1.5">
                <label htmlFor="name" className="text-sm font-medium">Full name</label>
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
                <label htmlFor="email" className="text-sm font-medium">Email</label>
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
                <label htmlFor="password" className="text-sm font-medium">Password</label>
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

              <div className="flex flex-col gap-1.5">
                <label htmlFor="confirmPassword" className="text-sm font-medium">Confirm password</label>
                <input
                  id="confirmPassword"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Repeat your password"
                  required
                  minLength={8}
                  autoComplete="new-password"
                  className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <div className="flex gap-2 mt-2">
                <button
                  type="button"
                  onClick={() => setStep(2)}
                  disabled={submitting}
                  className="flex-1 rounded-lg border py-2.5 text-sm font-medium hover:bg-muted disabled:opacity-50 transition-colors"
                >
                  Back
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="flex-1 rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
                >
                  {submitting ? "Setting up…" : "Finish setup"}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
