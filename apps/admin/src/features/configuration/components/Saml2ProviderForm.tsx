import { useRef, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import axios from "axios";
import { Copy, Upload } from "lucide-react";
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
import { samlApi, type UpsertSaml2Provider } from "@/lib/actions/saml";

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

/** Field-level validation. The certificate is required only when creating; on edit a blank value keeps the stored one. */
function buildSchema(isEdit: boolean) {
  return z.object({
    id: z.string().trim().min(1, "Provider ID is required"),
    displayName: z.string().trim().min(1, "Display name is required"),
    idpEntityId: z.string().trim().min(1, "IdP entity ID is required"),
    idpSsoUrl: z.string().trim().min(1, "IdP SSO URL is required").url("Must be a valid URL"),
    idpSigningCertificate: isEdit
      ? z.string().optional()
      : z.string().trim().min(1, "IdP signing certificate is required"),
    spEntityId: z.string().optional(),
    allowedDomains: z.string().optional(),
    defaultRole: z.string(),
    isEnabled: z.boolean(),
  });
}

function Saml2ProviderForm(props: Props) {
  const { initial, onSave, onCancel, saving, testResult, onTest, testing } = props;
  const isEdit = !!initial.id && initial.id !== "";

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [metadataError, setMetadataError] = useState("");
  const [parsing, setParsing] = useState(false);

  const schema = buildSchema(isEdit);
  type FormValues = z.infer<typeof schema>;

  const {
    register,
    control,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: initial as FormValues,
  });

  const idValue = watch("id");
  const spEntityIdValue = watch("spEntityId");

  // The IdP posts assertions to this SP endpoint; it's fixed by the backend route.
  const acsUrl = `${window.location.origin}/api/v1/auth/saml/acs`;
  const spEntityId = spEntityIdValue?.trim() || `${window.location.origin}/saml/metadata`;

  async function handleMetadataFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = ""; // allow re-selecting the same file
    if (!file) return;

    setMetadataError("");
    setParsing(true);
    try {
      const xml = await file.text();
      const parsed = await samlApi.parseMetadata(xml);
      setValue("idpEntityId", parsed.idpEntityId, { shouldValidate: true, shouldDirty: true });
      setValue("idpSsoUrl", parsed.idpSsoUrl, { shouldValidate: true, shouldDirty: true });
      setValue("idpSigningCertificate", parsed.idpSigningCertificate, { shouldValidate: true, shouldDirty: true });
    } catch (err) {
      const message =
        axios.isAxiosError(err) && err.response?.data?.title
          ? err.response.data.title
          : "Could not read this metadata file.";
      setMetadataError(message);
    } finally {
      setParsing(false);
    }
  }

  function submit(values: FormValues) {
    onSave(values as UpsertSaml2Provider);
  }

  return (
    <form onSubmit={handleSubmit(submit)}>

      <div className="rounded-xl border border-dashed bg-muted/30 px-6 py-4 mb-4 flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-medium">Import from IdP metadata</p>
          <p className="text-xs text-muted-foreground mt-0.5">
            Upload the IdP metadata XML to auto-fill the entity ID, SSO URL, and signing certificate.
          </p>
          {metadataError && <p className="text-xs text-destructive mt-1.5">{metadataError}</p>}
        </div>
        <input
          ref={fileInputRef}
          type="file"
          accept=".xml,application/xml,text/xml"
          className="hidden"
          onChange={handleMetadataFile}
        />
        <Button
          type="button"
          variant="outline"
          className="shrink-0"
          disabled={parsing}
          onClick={() => fileInputRef.current?.click()}
        >
          <Upload size={14} />
          {parsing ? "Reading…" : "Choose metadata file"}
        </Button>
      </div>

      <div className="rounded-xl border bg-card p-6 flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Provider ID</label>
            <Input
              {...register("id")}
              onChange={(e) =>
                setValue("id", e.target.value.toLowerCase().replace(/\s+/g, "-"), { shouldValidate: true })
              }
              placeholder="okta"
              disabled={isEdit}
            />
            {errors.id ? (
              <p className="text-xs text-destructive">{errors.id.message}</p>
            ) : (
              <p className="text-xs text-muted-foreground">Lowercase slug, e.g. "okta"</p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Display Name</label>
            <Input {...register("displayName")} placeholder="Okta" />
            {errors.displayName && <p className="text-xs text-destructive">{errors.displayName.message}</p>}
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">IdP Entity ID (Issuer)</label>
          <Input {...register("idpEntityId")} placeholder="https://idp.example.com/metadata" />
          {errors.idpEntityId ? (
            <p className="text-xs text-destructive">{errors.idpEntityId.message}</p>
          ) : (
            <p className="text-xs text-muted-foreground">The issuer/entity ID advertised by your IdP.</p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">IdP SSO URL</label>
          <Input {...register("idpSsoUrl")} placeholder="https://idp.example.com/sso/saml" />
          {errors.idpSsoUrl ? (
            <p className="text-xs text-destructive">{errors.idpSsoUrl.message}</p>
          ) : (
            <p className="text-xs text-muted-foreground">The SingleSignOnService URL (HTTP-Redirect binding).</p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium">IdP Signing Certificate</label>
          <Textarea
            {...register("idpSigningCertificate")}
            placeholder={
              isEdit
                ? "········ (saved — leave blank to keep)"
                : "-----BEGIN CERTIFICATE-----\n…\n-----END CERTIFICATE-----"
            }
            rows={5}
            className="font-mono text-xs"
          />
          {errors.idpSigningCertificate ? (
            <p className="text-xs text-destructive">{errors.idpSigningCertificate.message}</p>
          ) : (
            <p className="text-xs text-muted-foreground">
              The IdP's public signing certificate (PEM or base64). Used to verify assertion signatures.
            </p>
          )}
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
          <Input {...register("spEntityId")} placeholder={spEntityId} />
          <p className="text-xs text-muted-foreground">
            Identifier Piro advertises to the IdP. Blank = <span className="font-mono">{spEntityId}</span>
          </p>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Allowed Email Domains</label>
            <Input {...register("allowedDomains")} placeholder="example.com, another.org" />
            <p className="text-xs text-muted-foreground">Comma-separated. Blank = allow all.</p>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium">Default Role</label>
            <Controller
              control={control}
              name="defaultRole"
              render={({ field }) => (
                <Select value={field.value} onValueChange={(v) => v && field.onChange(v)}>
                  <SelectTrigger>
                    <SelectValue>{field.value}</SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {ROLES.map((r) => (
                      <SelectItem key={r} value={r}>{r}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            <p className="text-xs text-muted-foreground">Assigned to new users on first sign-in.</p>
          </div>
        </div>

        <Controller
          control={control}
          name="isEnabled"
          render={({ field }) => (
            <label className="flex items-center gap-2.5">
              <Switch checked={field.value} onCheckedChange={field.onChange} />
              <span className="text-sm font-medium">Enabled</span>
            </label>
          )}
        />

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
          onClick={() => onTest(idValue)}
          loading={testing}
          disabled={!isEdit}
          label="Validate Configuration"
        />
        <div className="flex items-center gap-2">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? "Saving…" : "Save Provider"}
          </Button>
        </div>
      </div>
    </form>
  );
}

export default Saml2ProviderForm;
