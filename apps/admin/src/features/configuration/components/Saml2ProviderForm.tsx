import { useState } from "react";
import { Copy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { TestButton } from "@/components/TestButton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { UpsertSaml2Provider } from "@/lib/actions/saml";

const ROLES = ["Owner", "Admin", "Member", "Viewer"];

interface Props {
  initial: UpsertSaml2Provider;
  onSave: (data: UpsertSaml2Provider) => void;
  onCancel: () => void;
  saving: boolean;
  testResult: { success: boolean; message: string } | null;
  onTest: (providerId: string) => void;
  testing: boolean;
}

function Saml2ProviderForm(props: Props) {
  const { initial, onSave, onCancel, saving, testResult, onTest, testing } = props;
  const [form, setForm] = useState(initial);
  const isEdit = !!initial.id && initial.id === form.id && form.id !== "";

  function set(key: keyof UpsertSaml2Provider, value: string | boolean) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  // The IdP posts assertions to this SP endpoint; it's fixed by the backend route.
  const acsUrl = `${window.location.origin}/api/v1/auth/saml/acs`;
  const spEntityId = form.spEntityId || `${window.location.origin}/saml/metadata`;

  return (
    <div>
      <div className="mb-5">
        <p className="text-sm text-muted-foreground">
          Works with any SAML 2.0 identity provider (Okta, Keycloak, Microsoft Entra, Google Workspace).
          Register the ACS URL and entity ID below in your IdP, then paste its metadata here.
        </p>
      </div>

      <div className="rounded-xl border bg-card p-6 flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Provider ID</label>
            <Input
              value={form.id}
              onChange={(e) => set("id", e.target.value.toLowerCase().replace(/\s+/g, "-"))}
              placeholder="okta"
              disabled={isEdit}
            />
            <p className="text-xs text-muted-foreground">Lowercase slug, e.g. "okta"</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Display Name</label>
            <Input
              value={form.displayName}
              onChange={(e) => set("displayName", e.target.value)}
              placeholder="Okta"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">IdP Entity ID (Issuer)</label>
          <Input
            value={form.idpEntityId}
            onChange={(e) => set("idpEntityId", e.target.value)}
            placeholder="https://idp.example.com/metadata"
          />
          <p className="text-xs text-muted-foreground">The issuer/entity ID advertised by your IdP.</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">IdP SSO URL</label>
          <Input
            value={form.idpSsoUrl}
            onChange={(e) => set("idpSsoUrl", e.target.value)}
            placeholder="https://idp.example.com/sso/saml"
          />
          <p className="text-xs text-muted-foreground">The SingleSignOnService URL (HTTP-Redirect binding).</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">IdP Signing Certificate</label>
          <Textarea
            value={form.idpSigningCertificate ?? ""}
            onChange={(e) => set("idpSigningCertificate", e.target.value)}
            placeholder={
              isEdit
                ? "········ (saved — leave blank to keep)"
                : "-----BEGIN CERTIFICATE-----\n…\n-----END CERTIFICATE-----"
            }
            rows={5}
            className="font-mono text-xs"
          />
          <p className="text-xs text-muted-foreground">
            The IdP's public signing certificate (PEM or base64). Used to verify assertion signatures.
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">ACS URL (Reply URL)</label>
          <div className="flex gap-2">
            <Input readOnly value={acsUrl} className="flex-1 bg-muted text-muted-foreground" />
            <Button
              type="button"
              variant="outline"
              size="icon"
              onClick={() => navigator.clipboard.writeText(acsUrl)}
              title="Copy"
            >
              <Copy size={14} />
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">Register this as the Assertion Consumer Service URL in your IdP.</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">SP Entity ID</label>
          <Input
            value={form.spEntityId ?? ""}
            onChange={(e) => set("spEntityId", e.target.value)}
            placeholder={spEntityId}
          />
          <p className="text-xs text-muted-foreground">
            Identifier Piro advertises to the IdP. Blank = <span className="font-mono">{spEntityId}</span>
          </p>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Allowed Email Domains</label>
            <Input
              value={form.allowedDomains ?? ""}
              onChange={(e) => set("allowedDomains", e.target.value)}
              placeholder="example.com, another.org"
            />
            <p className="text-xs text-muted-foreground">Comma-separated. Blank = allow all.</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Default Role</label>
            <Select value={form.defaultRole} onValueChange={(v) => v && set("defaultRole", v)}>
              <SelectTrigger>
                <SelectValue>{form.defaultRole}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {ROLES.map((r) => (
                  <SelectItem key={r} value={r}>{r}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">Assigned to new users on first sign-in.</p>
          </div>
        </div>

        <label className="flex items-center gap-2.5">
          <Switch checked={form.isEnabled} onCheckedChange={(v) => set("isEnabled", v)} />
          <span className="text-sm font-medium">Enabled</span>
        </label>

        {testResult && (
          <div
            className={`rounded-lg border px-4 py-3 text-sm ${
              testResult.success
                ? "border-green-500/30 bg-green-500/10 text-green-600 dark:text-green-400"
                : "border-destructive/20 bg-destructive/5 text-destructive"
            }`}
          >
            {testResult.message}
          </div>
        )}
      </div>

      <div className="flex items-center justify-between mt-4">
        <TestButton
          onClick={() => onTest(form.id)}
          loading={testing}
          disabled={!isEdit}
          label="Validate Configuration"
        />
        <div className="flex items-center gap-2">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="button" onClick={() => onSave(form)} disabled={saving}>
            {saving ? "Saving…" : "Save Provider"}
          </Button>
        </div>
      </div>
    </div>
  );
}

export default Saml2ProviderForm;
