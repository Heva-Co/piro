import { FlaskConical } from "lucide-react";
import { Button, type buttonVariants } from "@/components/ui/button";
import type { VariantProps } from "class-variance-authority";

interface Props {
  onClick: () => void;
  loading: boolean;
  disabled?: boolean;
  /** Shown when idle. Defaults to "Test Connection". */
  label?: string;
  /** Shown while loading. Defaults to "Testing…". */
  loadingLabel?: string;
  variant?: VariantProps<typeof buttonVariants>["variant"];
  className?: string;
}

export function TestButton({
  onClick,
  loading,
  disabled,
  label = "Test Connection",
  loadingLabel = "Testing…",
  variant = "outline",
  className,
}: Props) {
  return (
    <Button
      type="button"
      variant={variant}
      onClick={onClick}
      disabled={loading || disabled}
      className={className}
    >
      <FlaskConical size={14} />
      {loading ? loadingLabel : label}
    </Button>
  );
}
