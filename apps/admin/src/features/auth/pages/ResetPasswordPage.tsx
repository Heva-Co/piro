import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { AlertCircle } from "lucide-react";
import axios from "axios";
import { authApi } from "@/lib/actions/auth";
import { ROUTES } from "@/constants/routes";
import { Button } from "@/components/ui/button";
import { PasswordInput } from "@/components/ui/password-input";
import { Label } from "@/components/ui/label";
import AuthCardShell from "@/features/auth/components/AuthCardShell";
import { Alert, AlertTitle, AlertDescription } from "@/components/ui/alert";

const schema = z
  .object({
    newPassword: z.string().min(8, "Password must be at least 8 characters"),
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

type FormValues = z.infer<typeof schema>;

function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  const userId = searchParams.get("userId");

  const [error, setError] = useState("");

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  if (!token || !userId) {
    return (
      <AuthCardShell title="Invalid reset link">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>This password reset link is missing information or is malformed.</AlertTitle>
        </Alert>
        <Link
          to={ROUTES.AUTH.FORGOT_PASSWORD}
          className="text-sm text-muted-foreground hover:text-foreground underline underline-offset-4"
        >
          Request a new link
        </Link>
      </AuthCardShell>
    );
  }

  async function onSubmit(values: FormValues) {
    setError("");
    try {
      await authApi.resetPassword({
        userId: Number(userId),
        token: token!,
        newPassword: values.newPassword,
      });
      toast.success("Password reset. Signing you in on this device will require your new password.");
      navigate(ROUTES.AUTH.SIGN_IN, { replace: true });
    } catch (err) {
      const data = axios.isAxiosError(err) ? err.response?.data : undefined;
      const msg = data?.detail ?? data?.title ?? "Invalid or expired reset link.";
      setError(msg);
    }
  }

  return (
    <AuthCardShell
      title="Reset your password"
      description="Choose a new password for your account."
    >
      {error && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>{error}</AlertTitle>
          <AlertDescription>
            <Link
              to={ROUTES.AUTH.FORGOT_PASSWORD}
              className="underline underline-offset-4 hover:text-foreground"
            >
              Request a new link
            </Link>
          </AlertDescription>
        </Alert>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="newPassword">New password</Label>
          <PasswordInput
            id="newPassword"
            placeholder="At least 8 characters"
            autoComplete="new-password"
            {...register("newPassword")}
          />
          {errors.newPassword && (
            <p className="text-xs text-destructive">{errors.newPassword.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="confirmPassword">Confirm password</Label>
          <PasswordInput
            id="confirmPassword"
            placeholder="Repeat your new password"
            autoComplete="new-password"
            {...register("confirmPassword")}
          />
          {errors.confirmPassword && (
            <p className="text-xs text-destructive">{errors.confirmPassword.message}</p>
          )}
        </div>

        <p className="text-xs text-muted-foreground">
          Resetting your password signs you out on all other devices.
        </p>

        <Button type="submit" disabled={isSubmitting} className="mt-2 w-full">
          {isSubmitting ? "Resetting…" : "Reset password"}
        </Button>
      </form>
    </AuthCardShell>
  );
}

export default ResetPasswordPage;
