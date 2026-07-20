import type { ReactNode } from "react";
import { Card, CardContent } from "@/components/ui/card";

interface Props {
  title: string;
  description?: ReactNode;
  children: ReactNode;
}

/**
 * Centered card auth shell shared by the public auth pages (sign-in, forgot/reset
 * password). Mirrors the layout SignInPage renders inline so each page only supplies
 * its own heading copy and form body.
 */
function AuthCardShell(props: Props) {
  const { title, description, children } = props;

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <Card className="w-full max-w-md p-8">
        <CardContent className="flex flex-col gap-6 p-0">
          <div className="flex flex-col gap-1">
            <h1 className="text-2xl font-bold">{title}</h1>
            {description && <p className="text-sm text-muted-foreground">{description}</p>}
          </div>
          {children}
        </CardContent>
      </Card>
    </div>
  );
}

export default AuthCardShell;
