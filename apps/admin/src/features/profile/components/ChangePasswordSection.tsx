import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { KeyRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PasswordInput } from "@/components/ui/password-input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import { profileApi } from "@/lib/actions/profile";

const schema = z
  .object({
    currentPassword: z.string().min(1, "Current password is required"),
    newPassword: z.string().min(8, "Must be at least 8 characters"),
    confirmPassword: z.string().min(1, "Confirm your new password"),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords don't match",
    path: ["confirmPassword"],
  });

type FormValues = z.infer<typeof schema>;

const DEFAULT_VALUES: FormValues = { currentPassword: "", newPassword: "", confirmPassword: "" };

export function ChangePasswordSection() {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: DEFAULT_VALUES,
  });

  const changePassword = useMutation({
    mutationFn: (values: FormValues) =>
      profileApi.changePassword({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      }),
    onSuccess: () => reset(DEFAULT_VALUES),
  });

  function onSubmit(values: FormValues) {
    toast.promise(changePassword.mutateAsync(values), {
      loading: "Updating password…",
      success: "Password updated.",
      error: (err) =>
        err?.response?.data?.title ?? "Failed to update password — check your current password.",
    });
  }

  return (
    <Card>
      <CardContent className="px-6 py-5">
        <div className="mb-4">
          <h2 className="text-sm font-semibold flex items-center gap-2">
            <KeyRound size={14} />
            Password
          </h2>
          <p className="text-xs text-muted-foreground mt-0.5">Change the password used to sign in.</p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Current password</Label>
            <PasswordInput autoComplete="current-password" {...register("currentPassword")} />
            {errors.currentPassword && (
              <p className="text-xs text-destructive">{errors.currentPassword.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>New password</Label>
            <PasswordInput autoComplete="new-password" {...register("newPassword")} />
            {errors.newPassword && (
              <p className="text-xs text-destructive">{errors.newPassword.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>Confirm new password</Label>
            <PasswordInput autoComplete="new-password" {...register("confirmPassword")} />
            {errors.confirmPassword && (
              <p className="text-xs text-destructive">{errors.confirmPassword.message}</p>
            )}
          </div>

          <div className="flex justify-end pt-1">
            <Button type="submit" disabled={changePassword.isPending}>
              {changePassword.isPending ? "Updating…" : "Update password"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
