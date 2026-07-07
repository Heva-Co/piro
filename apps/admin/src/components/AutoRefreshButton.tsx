import { useState } from "react";
import { RefreshCw } from "lucide-react";

interface AutoRefreshButtonProps {
  onRefetch: () => void;
}

export function AutoRefreshButton({ onRefetch }: AutoRefreshButtonProps) {
  const [spinning, setSpinning] = useState(false);

  function handleClick() {
    setSpinning(true);
    onRefetch();
    setTimeout(() => setSpinning(false), 800);
  }

  return (
    <button
      onClick={handleClick}
      disabled={spinning}
      className="flex items-center gap-1.5 rounded-md border border-border bg-background px-3 py-1.5 text-sm font-medium text-muted-foreground hover:bg-muted transition-colors disabled:opacity-50"
    >
      <RefreshCw size={14} className={spinning ? "animate-spin" : ""} />
      Refresh
    </button>
  );
}
