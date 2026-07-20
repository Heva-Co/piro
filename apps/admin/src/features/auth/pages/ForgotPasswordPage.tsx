import { useState } from "react";
import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Mail, CheckCircle } from "lucide-react";
import { authApi } from "@/lib/actions/auth";
import { ROUTES } from "@/constants/routes";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import AuthCardShell from "@/features/auth/components/AuthCardShell";
import { Alert, AlertTitle } from "@/components/ui/alert";

const schema = z.object({
  email: z.email("Enter a valid email"),
});

type FormValues = z.infer<typeof schema>;

function ForgotPasswordPage() {
  const [submitted, setSubmitted] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: "" },
  });

  async function onSubmit(values: FormValues) {
    // The endpoint always returns 200 to avoid revealing whether the email exists.
    // Even on an unexpected error we show the same confirmation, never leaking state.
    try {
      await authApi.forgotPassword({ email: values.email });
    } finally {
      setSubmitted(true);
    }
  }

  if (submitted) {
    return (
      <AuthCardShell title="Check your email">
        <Alert variant="success">
          <CheckCircle />
          <AlertTitle>
            If an account exists for that address, we've sent a password reset link. Check your
            email.
          </AlertTitle>
        </Alert>
        <p className="text-xs text-muted-foreground">
          Reset emails require email to be configured. If you don't receive one, contact your
          administrator.
        </p>
        <Link
          to={ROUTES.AUTH.SIGN_IN}
          className="text-sm text-muted-foreground hover:text-foreground underline underline-offset-4"
        >
          Back to sign in
        </Link>
      </AuthCardShell>
    );
  }

  return (
    <AuthCardShell
      title="Forgot your password?"
      description="Enter your email and we'll send you a link to reset it."
    >
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

        <Button type="submit" disabled={isSubmitting} className="mt-2 w-full">
          {isSubmitting ? "Sending…" : "Send reset link"}
        </Button>

        <Link
          to={ROUTES.AUTH.SIGN_IN}
          className="text-center text-sm text-muted-foreground hover:text-foreground underline underline-offset-4"
        >
          Back to sign in
        </Link>
      </form>
    </AuthCardShell>
  );
}

export default ForgotPasswordPage;
