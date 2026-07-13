import { useState, useEffect } from "react";
import { useNavigate, Navigate } from "react-router-dom";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Check } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { setupApi } from "@/lib/actions/setup";
import { authApi } from "@/lib/api";
import { setStoredAuth } from "@/lib/axios";
import { ROUTES } from "@/constants/routes";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/ui/password-input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { TimezonePicker } from "@/components/TimezonePicker";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

type Step = 1 | 2 | 3;

const schema = z
  .object({
    siteName: z.string().min(1, "Site name is required"),
    siteUrl: z.union([z.url("Enter a valid URL"), z.literal("")]),

    name: z.string().min(1, "Full name is required"),
    email: z.email("Enter a valid email"),
    password: z.string().min(8, "Must be at least 8 characters"),
    confirmPassword: z.string().min(1, "Confirm your password"),
    timeZone: z.string().min(1, "Time zone is required"),

    provider: z.enum(["smtp", "resend"]),
    smtpHost: z.string(),
    smtpPort: z.number(),
    smtpUsername: z.string(),
    smtpPassword: z.string(),
    smtpFrom: z.string(),
    smtpUseSsl: z.boolean(),
    resendApiKey: z.string(),
    resendFrom: z.string(),
    verificationCode: z.string(),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: "Passwords don't match",
    path: ["confirmPassword"],
  })
  .refine(
    (values) =>
      values.provider === "smtp"
        ? values.smtpHost.trim() !== "" && values.smtpFrom.trim() !== ""
        : values.resendApiKey.trim() !== "" && values.resendFrom.trim() !== "",
    {
      message: "Email configuration is required to complete setup.",
      path: ["smtpHost"],
    }
  );

type FormValues = z.infer<typeof schema>;

const STEP_FIELDS: Record<Step, (keyof FormValues)[]> = {
  1: ["siteName", "siteUrl"],
  2: ["name", "email", "password", "confirmPassword", "timeZone"],
  3: [],
};

function emailConfigFromValues(values: FormValues) {
  return values.provider === "smtp"
    ? {
        provider: "smtp",
        smtpHost: values.smtpHost,
        smtpPort: values.smtpPort,
        smtpUsername: values.smtpUsername || null,
        smtpPassword: values.smtpPassword || null,
        smtpFrom: values.smtpFrom,
        smtpUseSsl: values.smtpUseSsl,
        resendApiKey: null,
        resendFrom: null,
      }
    : {
        provider: "resend",
        smtpHost: null,
        smtpPort: null,
        smtpUsername: null,
        smtpPassword: null,
        smtpFrom: null,
        smtpUseSsl: null,
        resendApiKey: values.resendApiKey,
        resendFrom: values.resendFrom,
      };
}

export default function SetupPage() {
  const navigate = useNavigate();
  const { isAuthenticated, loginWithTokens } = useAuth();

  const [setupDone, setSetupDone] = useState(false);
  const [step, setStep] = useState<Step>(1);
  const [codeSent, setCodeSent] = useState(false);
  const [codeVerified, setCodeVerified] = useState(false);
  const [sendingCode, setSendingCode] = useState(false);
  const [confirmingCode, setConfirmingCode] = useState(false);

  useEffect(() => {
    setupApi
      .status()
      .then((s) => {
        if (s.isComplete) setSetupDone(true);
      })
      .catch(() => {});
  }, []);

  const {
    register,
    control,
    handleSubmit,
    trigger,
    watch,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      siteName: "Piro",
      siteUrl: window.location.origin,
      name: "",
      email: "",
      password: "",
      confirmPassword: "",
      timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      provider: "smtp",
      smtpHost: "",
      smtpPort: 587,
      smtpUsername: "",
      smtpPassword: "",
      smtpFrom: "",
      smtpUseSsl: true,
      resendApiKey: "",
      resendFrom: "",
      verificationCode: "",
    },
  });

  const provider = watch("provider");

  if (setupDone && isAuthenticated) {
    return <Navigate to={ROUTES.DASHBOARD} replace />;
  }

  function resetVerification() {
    setCodeSent(false);
    setCodeVerified(false);
  }

  async function goNext() {
    const valid = await trigger(STEP_FIELDS[step]);
    if (valid) setStep((s) => (s + 1) as Step);
  }

  async function sendCode() {
    const valid =
      provider === "smtp"
        ? await trigger(["smtpHost", "smtpFrom"])
        : await trigger(["resendApiKey", "resendFrom"]);
    if (!valid) return;

    setSendingCode(true);
    try {
      const values = getValues();
      await setupApi.sendEmailTestCode({ email: values.email, ...emailConfigFromValues(values) });
      setCodeSent(true);
      setCodeVerified(false);
      toast.success(`Verification code sent to ${values.email}.`);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Failed to send verification code.";
      toast.error(msg);
    } finally {
      setSendingCode(false);
    }
  }

  async function confirmCode() {
    const values = getValues();
    if (!values.verificationCode.trim()) {
      toast.error("Enter the code sent to your email.");
      return;
    }

    setConfirmingCode(true);
    try {
      await setupApi.confirmEmailTestCode({
        email: values.email,
        code: values.verificationCode,
        config: emailConfigFromValues(values),
      });
      setCodeVerified(true);
      toast.success("Email configuration verified.");
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Invalid or expired code.";
      toast.error(msg);
    } finally {
      setConfirmingCode(false);
    }
  }

  async function onSubmit(values: FormValues) {
    if (!codeVerified) {
      toast.error("Verify your email configuration before finishing setup.");
      return;
    }

    try {
      const isSmtp = values.provider === "smtp";
      await setupApi.complete({
        name: values.name,
        email: values.email,
        password: values.password,
        timeZone: values.timeZone,
        siteTitle: values.siteName,
        siteUrl: values.siteUrl,
        emailProvider: values.provider,
        emailVerificationCode: values.verificationCode,
        emailHost: isSmtp ? values.smtpHost : null,
        emailPort: isSmtp ? values.smtpPort || 587 : null,
        emailUsername: isSmtp ? values.smtpUsername || null : null,
        emailPassword: isSmtp ? values.smtpPassword || null : null,
        emailFrom: isSmtp ? values.smtpFrom || null : null,
        emailUseSsl: isSmtp ? values.smtpUseSsl : null,
        resendApiKey: isSmtp ? null : values.resendApiKey,
        resendFrom: isSmtp ? null : values.resendFrom || null,
      });

      const { accessToken, refreshToken, expiresIn, user } = await authApi.signIn(
        values.email,
        values.password
      );
      const auth = { accessToken, refreshToken, expiresAt: Date.now() + expiresIn * 1000 };
      setStoredAuth(auth);
      loginWithTokens(auth, user);

      navigate(ROUTES.DASHBOARD, { replace: true });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Setup failed. Please try again.";
      toast.error(msg);
    }
  }

  const steps = [
    { n: 1, label: "General" },
    { n: 2, label: "Account" },
    { n: 3, label: "Email" },
  ];

  return (
    <div className="min-h-screen flex items-center justify-center px-4 bg-background">
      <div className="w-full max-w-md flex flex-col gap-6">
        <div className="text-center">
          <a href="/" className="text-2xl font-bold tracking-tight">
            Piro
          </a>
          <p className="text-muted-foreground text-sm mt-1">Initial setup</p>
        </div>

        {/* Step indicator */}
        <div className="flex items-center justify-center gap-2">
          {steps.map((s, i) => (
            <div key={s.n} className="flex items-center gap-2">
              <div className="flex items-center gap-1.5 text-xs">
                <span
                  className={`size-5 rounded-full flex items-center justify-center text-[10px] font-semibold transition-colors ${
                    step >= s.n
                      ? "bg-primary text-primary-foreground"
                      : "bg-muted text-muted-foreground"
                  }`}
                >
                  {step > s.n ? <Check size={10} /> : s.n}
                </span>
                <span className={step === s.n ? "font-medium" : "text-muted-foreground"}>
                  {s.label}
                </span>
              </div>
              {i < steps.length - 1 && <div className="h-px w-6 bg-border" />}
            </div>
          ))}
        </div>

        <div className="rounded-xl border border-border bg-card shadow-sm p-6">
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
              {/* ── Step 1: General config ── */}
              {step === 1 && (
                <>
                  <div>
                    <h2 className="text-sm font-semibold mb-0.5">General configuration</h2>
                    <p className="text-xs text-muted-foreground">
                      Basic info about your status page.
                    </p>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="siteName">Site name</Label>
                    <Input id="siteName" placeholder="Piro" {...register("siteName")} />
                    {errors.siteName && (
                      <p className="text-xs text-destructive">{errors.siteName.message}</p>
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="siteUrl">Site URL</Label>
                    <Input
                      id="siteUrl"
                      type="url"
                      placeholder="https://status.yourdomain.com"
                      {...register("siteUrl")}
                    />
                    {errors.siteUrl ? (
                      <p className="text-xs text-destructive">{errors.siteUrl.message}</p>
                    ) : (
                      <p className="text-xs text-muted-foreground">
                        Used for OIDC redirect URIs and email links.
                      </p>
                    )}
                  </div>

                  <Button type="button" onClick={goNext} className="mt-2 w-full">
                    Continue
                  </Button>
                </>
              )}

              {/* ── Step 2: Admin account ── */}
              {step === 2 && (
                <>
                  <div>
                    <h2 className="text-sm font-semibold mb-0.5">Owner account</h2>
                    <p className="text-xs text-muted-foreground">
                      Create the first administrator account.
                    </p>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="name">Full name</Label>
                    <Input id="name" placeholder="Jane Smith" autoComplete="name" {...register("name")} />
                    {errors.name && (
                      <p className="text-xs text-destructive">{errors.name.message}</p>
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="email">Email</Label>
                    <Input
                      id="email"
                      type="email"
                      placeholder="you@example.com"
                      autoComplete="email"
                      {...register("email", { onChange: resetVerification })}
                    />
                    {errors.email && (
                      <p className="text-xs text-destructive">{errors.email.message}</p>
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="password">Password</Label>
                    <PasswordInput
                      id="password"
                      placeholder="Minimum 8 characters"
                      autoComplete="new-password"
                      {...register("password")}
                    />
                    {errors.password && (
                      <p className="text-xs text-destructive">{errors.password.message}</p>
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="confirmPassword">Confirm password</Label>
                    <PasswordInput
                      id="confirmPassword"
                      placeholder="Repeat your password"
                      autoComplete="new-password"
                      {...register("confirmPassword")}
                    />
                    {errors.confirmPassword && (
                      <p className="text-xs text-destructive">{errors.confirmPassword.message}</p>
                    )}
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="timeZone">Time zone</Label>
                    <Controller
                      name="timeZone"
                      control={control}
                      render={({ field }) => (
                        <TimezonePicker value={field.value} onChange={field.onChange} />
                      )}
                    />
                    {errors.timeZone && (
                      <p className="text-xs text-destructive">{errors.timeZone.message}</p>
                    )}
                  </div>

                  <div className="flex gap-2 mt-2">
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setStep(1)}
                      className="flex-1"
                    >
                      Back
                    </Button>
                    <Button type="button" onClick={goNext} className="flex-1">
                      Continue
                    </Button>
                  </div>
                </>
              )}

              {/* ── Step 3: Email config + verification ── */}
              {step === 3 && (
                <>
                  <div>
                    <h2 className="text-sm font-semibold mb-0.5">Email provider</h2>
                    <p className="text-xs text-muted-foreground">
                      Required — Piro needs a working mailbox to send invites and alerts.
                      We'll send a code to <strong>{getValues("email")}</strong> to confirm it works.
                    </p>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label>Provider</Label>
                    <Controller
                      name="provider"
                      control={control}
                      render={({ field }) => (
                        <Select
                          value={field.value}
                          onValueChange={(v) => {
                            field.onChange(v);
                            resetVerification();
                          }}
                        >
                          <SelectTrigger className="w-full">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="smtp">SMTP</SelectItem>
                            <SelectItem value="resend">Resend</SelectItem>
                          </SelectContent>
                        </Select>
                      )}
                    />
                  </div>

                  {provider === "smtp" && (
                    <>
                      <div className="grid grid-cols-2 gap-2">
                        <div className="flex flex-col gap-1.5">
                          <Label htmlFor="smtpHost">Host</Label>
                          <Input
                            id="smtpHost"
                            placeholder="smtp.example.com"
                            {...register("smtpHost", { onChange: resetVerification })}
                          />
                        </div>
                        <div className="flex flex-col gap-1.5">
                          <Label htmlFor="smtpPort">Port</Label>
                          <Input
                            id="smtpPort"
                            type="number"
                            placeholder="587"
                            {...register("smtpPort", { valueAsNumber: true, onChange: resetVerification })}
                          />
                        </div>
                      </div>
                      <div className="flex flex-col gap-1.5">
                        <Label htmlFor="smtpUsername">Username</Label>
                        <Input
                          id="smtpUsername"
                          placeholder="user@example.com"
                          autoComplete="off"
                          {...register("smtpUsername", { onChange: resetVerification })}
                        />
                      </div>
                      <div className="flex flex-col gap-1.5">
                        <Label htmlFor="smtpPassword">Password</Label>
                        <PasswordInput
                          id="smtpPassword"
                          placeholder="SMTP password"
                          autoComplete="new-password"
                          {...register("smtpPassword", { onChange: resetVerification })}
                        />
                      </div>
                      <div className="flex flex-col gap-1.5">
                        <Label htmlFor="smtpFrom">From address</Label>
                        <Input
                          id="smtpFrom"
                          placeholder="Piro <no-reply@example.com>"
                          {...register("smtpFrom", { onChange: resetVerification })}
                        />
                        {errors.smtpHost && (
                          <p className="text-xs text-destructive">{errors.smtpHost.message}</p>
                        )}
                      </div>
                      <div className="flex items-center gap-2">
                        <Controller
                          name="smtpUseSsl"
                          control={control}
                          render={({ field }) => (
                            <Switch
                              checked={field.value}
                              onCheckedChange={(v) => {
                                field.onChange(v);
                                resetVerification();
                              }}
                            />
                          )}
                        />
                        <Label className="mb-0!">Use SSL/TLS</Label>
                      </div>
                    </>
                  )}

                  {provider === "resend" && (
                    <>
                      <div className="flex flex-col gap-1.5">
                        <Label htmlFor="resendApiKey">API Key</Label>
                        <PasswordInput
                          id="resendApiKey"
                          placeholder="re_..."
                          autoComplete="new-password"
                          {...register("resendApiKey", { onChange: resetVerification })}
                        />
                      </div>
                      <div className="flex flex-col gap-1.5">
                        <Label htmlFor="resendFrom">From address</Label>
                        <Input
                          id="resendFrom"
                          placeholder="Piro <no-reply@yourdomain.com>"
                          {...register("resendFrom", { onChange: resetVerification })}
                        />
                        {errors.smtpHost && (
                          <p className="text-xs text-destructive">{errors.smtpHost.message}</p>
                        )}
                      </div>
                    </>
                  )}

                  {!codeVerified && (
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={sendCode}
                      disabled={sendingCode}
                      className="w-full"
                    >
                      {sendingCode ? "Sending…" : codeSent ? "Resend code" : "Send verification code"}
                    </Button>
                  )}

                  {codeSent && !codeVerified && (
                    <div className="flex flex-col gap-1.5">
                      <Label htmlFor="verificationCode">Verification code</Label>
                      <div className="flex gap-2">
                        <Input
                          id="verificationCode"
                          placeholder="123456"
                          maxLength={6}
                          {...register("verificationCode")}
                        />
                        <Button type="button" onClick={confirmCode} disabled={confirmingCode}>
                          {confirmingCode ? "Verifying…" : "Verify"}
                        </Button>
                      </div>
                    </div>
                  )}

                  {codeVerified && (
                    <p className="flex items-center gap-1.5 text-sm text-green-700 dark:text-green-400">
                      <Check size={14} />
                      Email configuration verified.
                    </p>
                  )}

                  <div className="flex gap-2 mt-2">
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setStep(2)}
                      disabled={isSubmitting}
                      className="flex-1"
                    >
                      Back
                    </Button>
                    <Button type="submit" disabled={isSubmitting || !codeVerified} className="flex-1">
                      {isSubmitting ? "Setting up…" : "Finish setup"}
                    </Button>
                  </div>
                </>
              )}
          </form>
        </div>
      </div>
    </div>
  );
}
