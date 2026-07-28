import { useMutation } from "@tanstack/react-query";
import { Check, Mail } from "lucide-react";
import { Button } from "@/components/ui/button";
import { usersApi } from "@/lib/api";

interface Props {
  userId: number;
}

/**
 * Re-sends the invitation email for a user who never accepted theirs. Shown only on pending rows.
 * Lives inside a clickable row, so it stops propagation to avoid navigating to the user detail.
 */
function ResendInviteButton(props: Props) {
  const { userId } = props;

  const resend = useMutation({
    mutationFn: () => usersApi.resendInvite(userId),
  });

  const label = resend.isPending
    ? "Sending…"
    : resend.isSuccess
      ? "Sent"
      : resend.isError
        ? "Retry"
        : "Resend invite";

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={resend.isPending}
      title={resend.isError ? "Failed to resend the invitation. Click to try again." : undefined}
      onClick={(e) => {
        e.stopPropagation();
        resend.mutate();
      }}
    >
      {resend.isSuccess ? <Check size={14} /> : <Mail size={14} />}
      {label}
    </Button>
  );
}

export default ResendInviteButton;
