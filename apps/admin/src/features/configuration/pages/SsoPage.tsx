import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import axios from "axios";
import { Pencil, Plus } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { oidcApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

export default function SsoPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [ssoModeError, setSsoModeError] = useState("");

  const { data: providers = [], isLoading: loadingProviders } = useQuery({
    queryKey: QUERY_KEYS.OIDC_CONFIGS,
    queryFn: oidcApi.list,
  });

  const { data: ssoMode } = useQuery({
    queryKey: QUERY_KEYS.OIDC_SSO_MODE,
    queryFn: oidcApi.getSsoMode,
  });

  const ssoOnly = ssoMode?.ssoOnly ?? false;
  const hasEnabledProvider = providers.some((p) => p.isEnabled);

  const ssoModeMutation = useMutation({
    mutationFn: (v: boolean) => oidcApi.setSsoMode(v),
    onSuccess: () => {
      setSsoModeError("");
      qc.invalidateQueries({ queryKey: QUERY_KEYS.OIDC_SSO_MODE });
    },
    onError: (err: unknown) => {
      const message =
        axios.isAxiosError(err) && err.response?.data?.title
          ? err.response.data.title
          : "Failed to update SSO-only mode.";
      setSsoModeError(message);
    },
  });

  return (
    <div className="max-w-4xl">
      <PageHeader breadcrumbs={[{ label: "Single Sign-On" }]} />
      <p className="text-muted-foreground text-sm -mt-4 mb-6">
        Configure identity providers so your team can sign in with their existing accounts.{" "}
        <a
          href="https://openid.net/developers/how-connect-works/"
          target="_blank"
          rel="noopener noreferrer"
          className="underline"
        >
          Learn how to set up SSO →
        </a>
      </p>

      {/* SSO-only mode — only shown once at least one provider is configured */}
      {hasEnabledProvider && (
        <div className="rounded-xl border bg-card px-6 py-4 mb-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-semibold">SSO-only mode</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                Disables password-based sign-in for all users. Only SSO providers configured below will work.
              </p>
            </div>
            <div className="flex items-center gap-3 shrink-0 ml-6">
              <Switch checked={ssoOnly} onCheckedChange={(v) => ssoModeMutation.mutate(v)} />
              <span className="text-sm text-muted-foreground w-16">
                {ssoOnly ? "Enabled" : "Disabled"}
              </span>
            </div>
          </div>
          {ssoModeError && (
            <p className="text-xs text-destructive mt-2">{ssoModeError}</p>
          )}
        </div>
      )}

      <Tabs defaultValue="oidc">
        <TabsList>
          <TabsTrigger value="oidc">OIDC / OAuth2</TabsTrigger>
          <TabsTrigger value="saml">SAML 2.0</TabsTrigger>
        </TabsList>

        <TabsContent value="oidc" className="mt-4">
          <div className="flex items-center justify-between mb-3">
            <p className="text-sm text-muted-foreground">
              OpenID Connect providers (Google, Microsoft, Okta, …)
            </p>
            <Button type="button" onClick={() => navigate(ROUTES.CONFIG.SSO_NEW)}>
              <Plus size={14} /> Add Provider
            </Button>
          </div>

          <div className="rounded-xl border bg-card divide-y">
            {loadingProviders ? (
              <div className="px-6 py-8 text-sm text-muted-foreground">Loading…</div>
            ) : providers.length === 0 ? (
              <div className="px-6 py-8 text-sm text-muted-foreground text-center">
                No providers configured yet.
              </div>
            ) : (
              providers.map((p) => (
                <div key={p.id} className="flex items-center justify-between px-6 py-4">
                  <div>
                    <p className="text-sm font-medium">{p.displayName}</p>
                    <p className="text-xs text-muted-foreground">{p.authority}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                        p.isEnabled
                          ? "bg-foreground text-background"
                          : "border text-muted-foreground"
                      }`}
                    >
                      {p.isEnabled ? "Enabled" : "Disabled"}
                    </span>
                    <span className="text-sm text-muted-foreground">{p.defaultRole}</span>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => navigate(ROUTES.CONFIG.SSO_DETAIL(p.id))}
                    >
                      <Pencil size={14} />
                    </Button>
                  </div>
                </div>
              ))
            )}
          </div>
        </TabsContent>

        <TabsContent value="saml" className="mt-4">
          <div className="rounded-xl border bg-card px-6 py-12 text-center text-sm text-muted-foreground">
            SAML 2.0 support is coming soon.
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
