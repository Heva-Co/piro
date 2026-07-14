import { useEffect, useState } from "react";
import { AlertTriangle } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

const CONFIRM_DELAY_SECONDS = 3;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: React.ReactNode;
  cancelLabel?: string;
  confirmLabel: string;
  confirmPendingLabel?: string;
  onConfirm: () => void;
  isPending?: boolean;
}

function ConfirmButton(props: {
  confirmLabel: string;
  confirmPendingLabel?: string;
  onConfirm: () => void;
  isPending: boolean;
}) {
  const { confirmLabel, confirmPendingLabel, onConfirm, isPending } = props;
  const [secondsLeft, setSecondsLeft] = useState(CONFIRM_DELAY_SECONDS);

  useEffect(() => {
    if (secondsLeft <= 0) return;
    const timeout = setTimeout(() => setSecondsLeft((s) => s - 1), 1000);
    return () => clearTimeout(timeout);
  }, [secondsLeft]);

  const waiting = secondsLeft > 0;

  return (
    <Button type="button" onClick={onConfirm} disabled={isPending || waiting}>
      {isPending && confirmPendingLabel
        ? confirmPendingLabel
        : waiting
          ? `${confirmLabel} (${secondsLeft})`
          : confirmLabel}
    </Button>
  );
}

/** A confirm dialog for actions that are allowed but worth a second thought — not a destructive/delete confirmation. */
export function WarningConfirmDialog(props: Props) {
  const {
    open,
    onOpenChange,
    title,
    description,
    cancelLabel = "Go back",
    confirmLabel,
    confirmPendingLabel,
    onConfirm,
    isPending = false,
  } = props;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle size={18} className="text-amber-500 shrink-0" />
            {title}
          </DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {cancelLabel}
          </Button>
          {open && (
            <ConfirmButton
              key="confirm-countdown"
              confirmLabel={confirmLabel}
              confirmPendingLabel={confirmPendingLabel}
              onConfirm={onConfirm}
              isPending={isPending}
            />
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
