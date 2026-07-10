import { useState } from "react";
import { Copy, Shield } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { TestButton } from "@/components/TestButton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { UpsertOidcProvider } from "@/lib/api";

const ROLES = ["Owner", "Admin", "Member", "Viewer"];

interface Props {
  initial: UpsertOidcProvider;
  onSave: (data: UpsertOidcProvider) => void;
  onCancel: () => void;
  saving: boolean;
  testResult: { success: boolean; message: string } | null;
  onTest: (authority: string) => void;
  testing: boolean;
}

export function SsoProviderForm({ initial, onSave, onCancel, saving, testResult, onTest, testing }: Props) {
  const [form, setForm] = useState(initial);
  const isEdit = !!initial.id && initial.id === form.id && form.id !== "";

  function set(key: keyof UpsertOidcProvider, value: string | boolean) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  const redirectUri = form.redirectUri || `${window.location.origin}/admin/auth/oidc/callback`;

  return (
    <div>
      <div className="mb-5">
        <p className="text-sm text-muted-foreground">
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
            <Input
              value={form.id}
              onChange={(e) => set("id", e.target.value.toLowerCase().replace(/\s+/g, "-"))}
              placeholder="google"
              disabled={isEdit}
            />
            <p className="text-xs text-muted-foreground">Lowercase slug, e.g. "google"</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Display Name</label>
            <Input
              value={form.displayName}
              onChange={(e) => set("displayName", e.target.value)}
              placeholder="Google"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Authority URL</label>
          <Input
            value={form.authority}
            onChange={(e) => set("authority", e.target.value)}
            placeholder="https://accounts.google.com"
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
          <Input
            value={form.clientId}
            onChange={(e) => set("clientId", e.target.value)}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Client Secret</label>
          <Input
            type="password"
            value={form.clientSecret ?? ""}
            onChange={(e) => set("clientSecret", e.target.value)}
            placeholder={isEdit ? "········ (saved — leave blank to keep)" : ""}
            autoComplete="new-password"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Redirect URI</label>
          <div className="flex gap-2">
            <Input readOnly value={redirectUri} className="flex-1 bg-muted text-muted-foreground" />
            <Button
              type="button"
              variant="outline"
              size="icon"
              onClick={() => navigator.clipboard.writeText(redirectUri)}
              title="Copy"
            >
              <Copy size={14} />
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">Register this in your provider's allowed redirect URIs.</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">Scopes</label>
          <div className="relative">
            <Input
              value={form.scopes}
              onChange={(e) => set("scopes", e.target.value)}
              placeholder="openid, profile, email"
              className="pr-10"
            />
            <Shield size={16} className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          </div>
          <p className="text-xs text-muted-foreground">Comma-separated list of OAuth2 scopes.</p>
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
          onClick={() => onTest(form.authority)}
          loading={testing}
          disabled={!form.authority}
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
