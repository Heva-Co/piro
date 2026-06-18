import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Copy, Pencil, Plus, Shield } from "lucide-react";
import { AdminLayout } from "@/components/AdminLayout";
import { oidcApi, type OidcProviderConfig, type UpsertOidcProvider } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

const ROLES = ["Owner", "Admin", "Member", "Viewer"];
const DEFAULT_SCOPES = "openid, profile, email";

type Tab = "oidc" | "saml";

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${
        checked ? "bg-foreground" : "bg-input"
      }`}
    >
      <span
        className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-background shadow-lg ring-0 transition-transform ${
          checked ? "translate-x-5" : "translate-x-0"
        }`}
      />
    </button>
  );
}

const EMPTY_FORM: UpsertOidcProvider = {
  id: "",
  displayName: "",
  authority: "",
  clientId: "",
  clientSecret: "",
  redirectUri: "",
  scopes: DEFAULT_SCOPES,
  allowedDomains: "",
  defaultRole: "Viewer",
  isEnabled: true,
};

function ProviderForm({
  initial,
  onSave,
  onCancel,
  saving,
  testResult,
  onTest,
  testing,
}: {
  initial: UpsertOidcProvider;
  onSave: (data: UpsertOidcProvider) => void;
  onCancel: () => void;
  saving: boolean;
  testResult: { success: boolean; message: string } | null;
  onTest: (id: string) => void;
  testing: boolean;
}) {
  const [form, setForm] = useState(initial);
  const isEdit = !!initial.id && initial.id === form.id && form.id !== "";

  function set(key: keyof UpsertOidcProvider, value: string | boolean) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  const redirectUri = form.redirectUri || `${window.location.origin}/admin/auth/oidc/callback`;

  return (
    <div>
      <button
        type="button"
        onClick={onCancel}
        className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground mb-4"
      >
        <ArrowLeft size={14} /> Back
      </button>

      <div className="mb-5">
        <h2 className="text-lg font-semibold">{isEdit ? "Edit" : "Add"} OIDC Provider</h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          Works with any standard OIDC/OAuth2 provider.{" "}
          <a
            href="https://openid.net/developers/how-connect-works/"
            target="_blank"
            rel="noopener noreferrer"
            className="underline"
          >
            Provider setup guides →
          </a>
        </p>
      </div>

      <div className="rounded-xl border bg-card p-6 flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Provider ID</label>
            <input
              value={form.id}
              onChange={(e) => set("id", e.target.value.toLowerCase().replace(/\s+/g, "-"))}
              placeholder="google"
              disabled={isEdit}
              className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring disabled:opacity-60"
            />
            <p className="text-xs text-muted-foreground">Lowercase slug, e.g. "google"</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Display Name</label>
            <input
              value={form.displayName}
              onChange={(e) => set("displayName", e.target.value)}
              placeholder="Google"
              className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Authority URL</label>
          <input
            value={form.authority}
            onChange={(e) => set("authority", e.target.value)}
            placeholder="https://accounts.google.com"
            className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
          <p className="text-xs text-muted-foreground">
            Discovery document:{" "}
            {form.authority
              ? `${form.authority.replace(/\/$/, "")}/.well-known/openid-configuration`
              : "https://.../.well-known/openid-configuration"}
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Client ID</label>
          <input
            value={form.clientId}
            onChange={(e) => set("clientId", e.target.value)}
            className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Client Secret</label>
          <input
            type="password"
            value={form.clientSecret ?? ""}
            onChange={(e) => set("clientSecret", e.target.value)}
            placeholder={isEdit ? "········ (saved — leave blank to keep)" : ""}
            autoComplete="new-password"
            className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Redirect URI</label>
          <div className="flex gap-2">
            <input
              readOnly
              value={redirectUri}
              className="flex-1 rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none"
            />
            <button
              type="button"
              onClick={() => navigator.clipboard.writeText(redirectUri)}
              className="rounded-lg border px-3 py-2 hover:bg-muted transition-colors"
              title="Copy"
            >
              <Copy size={14} />
            </button>
          </div>
          <p className="text-xs text-muted-foreground">Register this in your provider's allowed redirect URIs.</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Scopes</label>
          <div className="relative">
            <input
              value={form.scopes}
              onChange={(e) => set("scopes", e.target.value)}
              placeholder="openid, profile, email"
              className="w-full rounded-lg border bg-background px-3 py-2 pr-10 text-sm outline-none focus:ring-2 focus:ring-ring"
            />
            <Shield size={16} className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          </div>
          <p className="text-xs text-muted-foreground">Comma-separated list of OAuth2 scopes.</p>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Allowed Email Domains</label>
            <input
              value={form.allowedDomains ?? ""}
              onChange={(e) => set("allowedDomains", e.target.value)}
              placeholder="example.com, another.org"
              className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
            />
            <p className="text-xs text-muted-foreground">Comma-separated. Blank = allow all.</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Default Role</label>
            <select
              value={form.defaultRole}
              onChange={(e) => set("defaultRole", e.target.value)}
              className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring"
            >
              {ROLES.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
            <p className="text-xs text-muted-foreground">Assigned to new users on first sign-in.</p>
          </div>
        </div>

        <label className="flex items-center gap-2.5">
          <Toggle checked={form.isEnabled} onChange={(v) => set("isEnabled", v)} />
          <span className="text-sm font-medium">Enabled</span>
        </label>

        {testResult && (
          <div
            className={`rounded-lg border px-4 py-3 text-sm ${
              testResult.success
                ? "border-green-200 bg-green-50 text-green-700"
                : "border-destructive/20 bg-destructive/5 text-destructive"
            }`}
          >
            {testResult.message}
          </div>
        )}
      </div>

      <div className="flex items-center justify-between mt-4">
        <button
          type="button"
          onClick={() => onTest(form.id)}
          disabled={testing || !form.id}
          className="text-sm font-medium hover:underline disabled:opacity-50"
        >
          {testing ? "Testing…" : "Test Connection"}
        </button>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted transition-colors"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => onSave(form)}
            disabled={saving}
            className="rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
          >
            {saving ? "Saving…" : "Save Provider"}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function SsoPage() {
  const qc = useQueryClient();
  const [tab, setTab] = useState<Tab>("oidc");
  const [formState, setFormState] = useState<UpsertOidcProvider | null>(null);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [testing, setTesting] = useState(false);

  const { data: providers = [], isLoading: loadingProviders } = useQuery({
    queryKey: QUERY_KEYS.OIDC_CONFIGS,
    queryFn: oidcApi.list,
  });

  const { data: ssoMode } = useQuery({
    queryKey: QUERY_KEYS.OIDC_SSO_MODE,
    queryFn: oidcApi.getSsoMode,
  });

  const ssoOnly = ssoMode?.ssoOnly ?? false;

  const ssoModeMutation = useMutation({
    mutationFn: (v: boolean) => oidcApi.setSsoMode(v),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.OIDC_SSO_MODE }),
  });

  const upsertMutation = useMutation({
    mutationFn: (data: UpsertOidcProvider) => oidcApi.upsert(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.OIDC_CONFIGS });
      setFormState(null);
      setTestResult(null);
    },
  });

  async function handleTest(providerId: string) {
    if (!providerId) return;
    setTesting(true);
    setTestResult(null);
    try {
      const result = await oidcApi.test(providerId);
      setTestResult(result);
    } catch {
      setTestResult({ success: false, message: "Connection test failed." });
    } finally {
      setTesting(false);
    }
  }

  function openEdit(p: OidcProviderConfig) {
    setFormState({
      id: p.id,
      displayName: p.displayName,
      authority: p.authority,
      clientId: p.clientId,
      clientSecret: "",
      redirectUri: p.redirectUri ?? "",
      scopes: p.scopes,
      allowedDomains: p.allowedDomains ?? "",
      defaultRole: p.defaultRole,
      isEnabled: p.isEnabled,
    });
    setTestResult(null);
  }

  return (
    <AdminLayout title="Single Sign-On">
      <div className="max-w-4xl">
        {formState ? (
          <ProviderForm
            initial={formState}
            onSave={(data) => upsertMutation.mutate(data)}
            onCancel={() => { setFormState(null); setTestResult(null); }}
            saving={upsertMutation.isPending}
            testResult={testResult}
            onTest={handleTest}
            testing={testing}
          />
        ) : (
          <>
            <div className="mb-6">
              <h1 className="text-2xl font-bold">Single Sign-On</h1>
              <p className="text-muted-foreground text-sm mt-1">
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
            </div>

            {/* SSO-only mode */}
            <div className="rounded-xl border bg-card px-6 py-4 flex items-center justify-between mb-4">
              <div>
                <p className="text-sm font-semibold">SSO-only mode</p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Disables password-based sign-in for all users. Only SSO providers configured below will work.
                </p>
              </div>
              <div className="flex items-center gap-3 shrink-0 ml-6">
                <Toggle
                  checked={ssoOnly}
                  onChange={(v) => ssoModeMutation.mutate(v)}
                />
                <span className="text-sm text-muted-foreground w-16">
                  {ssoOnly ? "Enabled" : "Disabled"}
                </span>
              </div>
            </div>

            {/* Tabs */}
            <div className="flex gap-1 mb-4">
              {(["oidc", "saml"] as Tab[]).map((t) => (
                <button
                  key={t}
                  type="button"
                  onClick={() => setTab(t)}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                    tab === t
                      ? "bg-foreground text-background"
                      : "border hover:bg-muted"
                  }`}
                >
                  {t === "oidc" ? "OIDC / OAuth2" : "SAML 2.0"}
                </button>
              ))}
            </div>

            {tab === "oidc" && (
              <>
                <div className="flex items-center justify-between mb-3">
                  <p className="text-sm text-muted-foreground">
                    OpenID Connect providers (Google, Microsoft, Okta, …)
                  </p>
                  <button
                    type="button"
                    onClick={() => setFormState({ ...EMPTY_FORM })}
                    className="flex items-center gap-1.5 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 transition-opacity"
                  >
                    <Plus size={14} /> Add Provider
                  </button>
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
                          <button
                            type="button"
                            onClick={() => openEdit(p)}
                            className="rounded-md p-1.5 hover:bg-muted transition-colors"
                          >
                            <Pencil size={14} />
                          </button>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </>
            )}

            {tab === "saml" && (
              <div className="rounded-xl border bg-card px-6 py-12 text-center text-sm text-muted-foreground">
                SAML 2.0 support is coming soon.
              </div>
            )}
          </>
        )}
      </div>
    </AdminLayout>
  );
}
