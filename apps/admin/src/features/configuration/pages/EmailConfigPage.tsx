import { useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import axios from "axios";
import { toast } from "sonner";
import { Save } from "lucide-react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { TestButton } from "@/components/TestButton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Card, CardContent } from "@/components/ui/card";
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

interface EmailConfigFormValues {
  provider: Provider;
  smtpHost: string;
  smtpPort: number;
  smtpUsername: string;
  smtpPassword: string;
  smtpFrom: string;
  smtpUseTls: boolean;
  resendApiKey: string;
  resendFrom: string;
}

const DEFAULT_VALUES: EmailConfigFormValues = {
  provider: "smtp",
  smtpHost: "",
  smtpPort: 587,
  smtpUsername: "",
  smtpPassword: "",
  smtpFrom: "",
  smtpUseTls: true,
  resendApiKey: "",
  resendFrom: "",
};

export default function EmailConfigPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEYS.EMAIL_CONFIG,
    queryFn: () => emailApi.get(),
  });

  const { register, control, watch, reset, handleSubmit } = useForm<EmailConfigFormValues>({
    defaultValues: DEFAULT_VALUES,
  });

  const provider = watch("provider");

  useEffect(() => {
    if (!data) return;
    reset({
      provider: (data.provider as Provider) || "smtp",
      smtpHost: data.smtpHost ?? "",
      smtpPort: data.smtpPort ?? 587,
      smtpUsername: data.smtpUsername ?? "",
      // Secrets are never returned by the API — the field always starts empty.
      // An empty value on save means "keep the existing secret" (see emailApi.update).
      smtpPassword: "",
      smtpFrom: data.smtpFrom ?? "",
      smtpUseTls: data.smtpUseTls ?? true,
      resendApiKey: "",
      resendFrom: data.resendFrom ?? "",
    });
  }, [data, reset]);

  const saveMutation = useMutation({
    mutationFn: (values: EmailConfigFormValues) =>
      emailApi.update(
        values.provider === "smtp"
          ? {
              provider: "smtp",
              smtpHost: values.smtpHost,
              smtpPort: Number(values.smtpPort) || 587,
              smtpUsername: values.smtpUsername || undefined,
              smtpPassword: values.smtpPassword || undefined,
              smtpFrom: values.smtpFrom,
              smtpUseTls: values.smtpUseTls,
            }
          : {
              provider: "resend",
              resendApiKey: values.resendApiKey || undefined,
              resendFrom: values.resendFrom,
            }
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: QUERY_KEYS.EMAIL_CONFIG }),
  });

  function handleSave(values: EmailConfigFormValues) {
    toast.promise(saveMutation.mutateAsync(values), {
      loading: "Saving configuration…",
      success: "Configuration saved.",
      error: "Failed to save configuration.",
    });
  }

  const testMutation = useMutation({
    mutationFn: () => emailApi.test(),
  });

  function handleTest() {
    toast.promise(testMutation.mutateAsync(), {
      loading: "Sending test email…",
      success: "Test email sent to your account.",
      error: (err: unknown) =>
        axios.isAxiosError(err) && (err.response?.data?.detail || err.response?.data?.title)
          ? (err.response.data.detail ?? err.response.data.title)
          : "Failed to send test email.",
    });
  }

  return (
    <div className="max-w-2xl">
      <PageHeader breadcrumbs={[{ label: "Email" }]} />
      <p className="text-muted-foreground text-sm -mt-4 mb-6">
        Configure the email provider used for notifications and invitations
      </p>

      {isLoading ? (
        <div className="rounded-xl border bg-card p-8 text-sm text-muted-foreground">Loading…</div>
      ) : (
        <form onSubmit={handleSubmit(handleSave)}>
          <Card>
            <CardContent className="flex flex-col gap-6">
              {/* Provider */}
              <div className="flex flex-col gap-2">
                <Label>Provider</Label>
                <Controller
                  control={control}
                  name="provider"
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={(v) => v && field.onChange(v)}>
                      <SelectTrigger className="w-60">
                        <SelectValue>{field.value === "smtp" ? "SMTP" : "Resend"}</SelectValue>
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="smtp">SMTP</SelectItem>
                        <SelectItem value="resend">Resend</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
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
                      <Label>Host <span className="text-destructive">*</span></Label>
                      <Input {...register("smtpHost")} placeholder="smtp.example.com" />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <Label>Port <span className="text-destructive">*</span></Label>
                      <Input {...register("smtpPort", { valueAsNumber: true })} type="number" placeholder="587" />
                    </div>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label>Username</Label>
                    <Input {...register("smtpUsername")} placeholder="user@example.com" autoComplete="off" />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label>Password</Label>
                    <Input
                      {...register("smtpPassword")}
                      type="password"
                      placeholder={data?.hasSmtpPassword ? "········ (saved — leave blank to keep)" : "SMTP password"}
                      autoComplete="new-password"
                    />
                    {data?.hasSmtpPassword && (
                      <p className="text-xs text-muted-foreground">Leave blank to keep the existing password.</p>
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label>From address <span className="text-destructive">*</span></Label>
                    <Input {...register("smtpFrom")} placeholder="Piro <no-reply@example.com>" />
                  </div>

                  <Controller
                    control={control}
                    name="smtpUseTls"
                    render={({ field }) => (
                      <label className="flex items-center gap-2 text-sm">
                        <Switch checked={field.value} onCheckedChange={field.onChange} />
                        Use SSL/TLS
                      </label>
                    )}
                  />
                </>
              )}

              {/* ── Resend fields ── */}
              {provider === "resend" && (
                <>
                  <div className="flex flex-col gap-1.5">
                    <Label>API Key <span className="text-destructive">*</span></Label>
                    <Input
                      {...register("resendApiKey")}
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
                    <Label>From address <span className="text-destructive">*</span></Label>
                    <Input {...register("resendFrom")} placeholder="Piro <no-reply@yourdomain.com>" />
                    <p className="text-xs text-muted-foreground">Must be a verified domain in Resend.</p>
                  </div>
                </>
              )}

              {/* Actions */}
              <div className="flex items-center justify-between pt-2 border-t border-border">
                <TestButton
                  onClick={handleTest}
                  loading={testMutation.isPending}
                  disabled={saveMutation.isPending}
                  label="Send Test Email"
                  loadingLabel="Sending…"
                />
                <Button type="submit" disabled={saveMutation.isPending || testMutation.isPending}>
                  <Save size={14} />
                  {saveMutation.isPending ? "Saving…" : "Save"}
                </Button>
              </div>
            </CardContent>
          </Card>
        </form>
      )}
    </div>
  );
}
