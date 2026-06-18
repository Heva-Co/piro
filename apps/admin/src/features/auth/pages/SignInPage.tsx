import { useState, useEffect } from "react";
import { useNavigate, useSearchParams, Navigate } from "react-router-dom";
import { Mail, Lock, Eye, EyeOff, AlertCircle, CheckCircle } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { ROUTES } from "@/constants/routes";
import { ENDPOINTS } from "@/constants/api";
import axios from "axios";

interface OidcProvider {
  id: string;
  displayName: string;
}

export default function SignInPage() {
  const { isAuthenticated, isLoading, login } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const [oidcProviders, setOidcProviders] = useState<OidcProvider[]>([]);
  const [ssoOnly, setSsoOnly] = useState(false);

  const invited = searchParams.has("invited");
  const oidcError = searchParams.has("oidc_error");
  const from = searchParams.get("from") ?? ROUTES.DASHBOARD;

  useEffect(() => {
    axios
      .get<OidcProvider[]>(ENDPOINTS.AUTH.OIDC_PROVIDERS)
      .then((r) => setOidcProviders(r.data))
      .catch(() => {});
    axios
      .get<{ ssoOnly: boolean }>(ENDPOINTS.AUTH.OIDC_SSO_MODE)
      .then((r) => setSsoOnly(r.data.ssoOnly))
      .catch(() => {});
  }, []);

  if (!isLoading && isAuthenticated) {
    return <Navigate to={from} replace />;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      await login(email, password);
      navigate(from, { replace: true });
    } catch (err) {
      const msg =
        axios.isAxiosError(err) && err.response?.data?.title
          ? err.response.data.title
          : "Invalid email or password.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-sm flex flex-col gap-6">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">Sign In</h1>
          <p className="text-sm text-muted-foreground">
            Enter your credentials to access the dashboard
          </p>
        </div>

        {invited && (
          <div className="flex items-start gap-2 rounded-lg border border-green-200 bg-green-50 dark:bg-green-950 dark:border-green-800 px-4 py-3 text-sm text-green-800 dark:text-green-300">
            <CheckCircle className="size-4 mt-0.5 shrink-0" />
            Account created! Sign in with your new credentials.
          </div>
        )}

        {oidcError && (
          <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
            <AlertCircle className="size-4 mt-0.5 shrink-0" />
            SSO sign-in failed. Please try again or use email and password.
          </div>
        )}

        {!ssoOnly && (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            {error && (
              <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
                <AlertCircle className="size-4 mt-0.5 shrink-0" />
                {error}
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <label htmlFor="email" className="text-sm font-medium">
                Email
              </label>
              <div className="flex items-center rounded-lg border bg-background focus-within:ring-2 focus-within:ring-ring overflow-hidden">
                <span className="px-3 text-muted-foreground">
                  <Mail className="size-4" />
                </span>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@example.com"
                  required
                  autoComplete="email"
                  className="flex-1 bg-transparent py-2 pr-3 text-sm outline-none"
                />
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="password" className="text-sm font-medium">
                Password
              </label>
              <div className="flex items-center rounded-lg border bg-background focus-within:ring-2 focus-within:ring-ring overflow-hidden">
                <span className="px-3 text-muted-foreground">
                  <Lock className="size-4" />
                </span>
                <input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  required
                  autoComplete="current-password"
                  className="flex-1 bg-transparent py-2 text-sm outline-none"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="px-3 text-muted-foreground hover:text-foreground transition-colors"
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="mt-2 w-full rounded-lg bg-primary text-primary-foreground py-2.5 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              {submitting ? "Signing in…" : "Sign In"}
            </button>
          </form>
        )}

        {ssoOnly && oidcProviders.length > 0 && (
          <p className="text-sm text-muted-foreground text-center py-2">
            Password sign-in is disabled for this instance. Use SSO below.
          </p>
        )}

        {oidcProviders.length > 0 && (
          <>
            {!ssoOnly && (
              <div className="relative">
                <div className="absolute inset-0 flex items-center">
                  <span className="w-full border-t" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-card px-2 text-muted-foreground">or continue with</span>
                </div>
              </div>
            )}
            <div className="flex flex-col gap-2">
              {oidcProviders.map((provider) => (
                <a
                  key={provider.id}
                  href={ENDPOINTS.AUTH.OIDC_START(provider.id)}
                  className="inline-flex items-center justify-center gap-2 rounded-md border border-input bg-background px-4 py-2 text-sm font-medium shadow-sm hover:bg-accent hover:text-accent-foreground transition-colors"
                >
                  {provider.displayName}
                </a>
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
