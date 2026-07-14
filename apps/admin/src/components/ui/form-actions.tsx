import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface FormActionsProps {
  onCancel: () => void;
  cancelLabel?: string;
  submitLabel: ReactNode;
  submitPendingLabel?: string;
  submitIcon?: ReactNode;
  isPending?: boolean;
  className?: string;
}

function FormActions(props: FormActionsProps) {
  const {
    onCancel,
    cancelLabel = "Cancel",
    submitLabel,
    submitPendingLabel,
    submitIcon,
    isPending = false,
    className,
  } = props;

  return (
    <div className={cn("flex items-center justify-between mt-6", className)}>
      <Button type="button" onClick={onCancel} variant="outline">
        {cancelLabel}
      </Button>
      <Button type="submit" disabled={isPending}>
        {submitIcon}
        {isPending && submitPendingLabel ? submitPendingLabel : submitLabel}
      </Button>
    </div>
  );
}
export default FormActions;
