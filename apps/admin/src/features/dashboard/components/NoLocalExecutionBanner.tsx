import { AlertCircle } from "lucide-react";

function NoLocalExecutionBanner() {
  return (
    <div className="rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 flex items-center gap-3 mb-6">
      <AlertCircle size={16} className="text-amber-600 shrink-0" />
      <p className="text-sm text-amber-800">
        No default worker connected — non-multi-region checks are not executing. Go to <strong>Workers</strong> to register and connect a default worker.
      </p>
    </div>
  );
}

export default NoLocalExecutionBanner;
