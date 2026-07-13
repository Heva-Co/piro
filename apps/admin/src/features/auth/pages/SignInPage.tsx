import { useState, useEffect } from "react";
import { useNavigate, useSearchParams, Navigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Mail, CheckCircle } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { setupApi } from "@/lib/api";
import { ROUTES } from "@/constants/routes";
import { ENDPOINTS } from "@/constants/api";
import axios from "axios";
import api from "@/lib/axios";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/ui/password-input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";

interface OidcProvider {
  id: string;
  displayName: string;
}

const schema = z.object({
  email: z.email("Enter a valid email"),
  password: z.string().min(1, "Password is required"),
});

type FormValues = z.infer<typeof schema>;

export default function SignInPage() {
  const { isAuthenticated, isLoading, login } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const setupQuery = useQuery({
    queryKey: ["setup-status"],
    queryFn: () => setupApi.status(),
    staleTime: 60_000,
    retry: false,
  });

  const [oidcProviders, setOidcProviders] = useState<OidcProvider[]>([]);
  const [ssoOnly, setSsoOnly] = useState(false);

  const invited = searchParams.has("invited");
  const oidcError = searchParams.has("oidc_error");
  const from = searchParams.get("from") ?? ROUTES.DASHBOARD;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  useEffect(() => {
    api
      .get<OidcProvider[]>(ENDPOINTS.AUTH.OIDC_PROVIDERS)
      .then((r) => setOidcProviders(r.data))
      .catch(() => {});
    api
      .get<{ ssoOnly: boolean }>(ENDPOINTS.AUTH.OIDC_SSO_MODE)
      .then((r) => setSsoOnly(r.data.ssoOnly))
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (oidcError) toast.error("SSO sign-in failed. Please try again or use email and password.");
  }, [oidcError]);

  if (setupQuery.data && !setupQuery.data.isComplete) {
    return <Navigate to={ROUTES.SETUP} replace />;
  }

  if (!isLoading && isAuthenticated) {
    return <Navigate to={from} replace />;
  }

  async function onSubmit(values: FormValues) {
    try {
      await login(values.email, values.password);
      navigate(from, { replace: true });
    } catch (err) {
      const msg =
        axios.isAxiosError(err) && err.response?.data?.title
          ? err.response.data.title
          : "Invalid email or password.";
      toast.error(msg);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-md p-8">
        <CardContent className="flex flex-col gap-6 p-0">
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

          {!ssoOnly && (
            <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="email">Email</Label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
                  <Input
                    id="email"
                    type="email"
                    placeholder="you@example.com"
                    autoComplete="email"
                    className="pl-9"
                    {...register("email")}
                  />
                </div>
                {errors.email && <p className="text-xs text-destructive">{errors.email.message}</p>}
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="password">Password</Label>
                <PasswordInput
                  id="password"
                  placeholder="••••••••"
                  autoComplete="current-password"
                  {...register("password")}
                />
                {errors.password && (
                  <p className="text-xs text-destructive">{errors.password.message}</p>
                )}
              </div>

              <Button type="submit" disabled={isSubmitting} className="mt-2 w-full">
                {isSubmitting ? "Signing in…" : "Sign In"}
              </Button>
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
        </CardContent>
      </Card>
    </div>
  );
}
