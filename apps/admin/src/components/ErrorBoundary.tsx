import { Component, type ErrorInfo, type ReactNode } from "react";
import { AlertOctagon, RefreshCw, ChevronDown } from "lucide-react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  error: Error | null;
  componentStack: string | null;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null, componentStack: null };

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("[ErrorBoundary]", error, info.componentStack);
    this.setState({ componentStack: info.componentStack ?? null });
  }

  render() {
    if (this.state.error) {
      return this.props.fallback ?? (
        <ErrorFallback
          error={this.state.error}
          componentStack={this.state.componentStack}
          reset={() => this.setState({ error: null, componentStack: null })}
        />
      );
    }
    return this.props.children;
  }
}

export function ErrorFallback({
  error,
  componentStack,
  reset,
}: {
  error?: Error;
  componentStack?: string | null;
  reset?: () => void;
}) {
  const isDev = import.meta.env.DEV;

  return (
    <div className="min-h-[60vh] flex flex-col items-center justify-center gap-6 px-6 text-center">
      <div className="flex size-16 items-center justify-center rounded-2xl bg-destructive/10 text-destructive">
        <AlertOctagon size={30} strokeWidth={1.75} />
      </div>

      <div className="space-y-1.5">
        <h2 className="text-lg font-semibold text-foreground">Something went wrong</h2>
        <p className="text-sm text-muted-foreground max-w-sm">
          An unexpected error occurred while rendering this page.
        </p>
        {!isDev && error?.message && (
          <p className="mt-1 text-sm text-muted-foreground font-mono max-w-sm break-all">{error.message}</p>
        )}
      </div>

      {reset && (
        <button
          onClick={reset}
          className="flex items-center gap-2 rounded-lg bg-primary text-primary-foreground px-4 py-2 text-sm font-medium hover:bg-primary/90 transition-colors"
        >
          <RefreshCw size={14} />
          Try again
        </button>
      )}

      {isDev && error && (
        <details className="group w-full max-w-2xl text-left" open>
          <summary className="flex cursor-pointer items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors list-none">
            <ChevronDown size={13} className="transition-transform group-open:rotate-180" />
            {error.name}: {error.message}
          </summary>
          <div className="mt-2 rounded-lg border border-border bg-muted/40 overflow-hidden">
            {error.stack && (
              <pre className="max-h-64 overflow-auto px-3 py-2.5 text-left text-[11px] leading-relaxed text-destructive font-mono whitespace-pre-wrap break-all">
                {error.stack}
              </pre>
            )}
            {componentStack && (
              <>
                <div className="border-t border-border px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                  Component stack
                </div>
                <pre className="max-h-64 overflow-auto px-3 py-2.5 text-left text-[11px] leading-relaxed text-muted-foreground font-mono whitespace-pre-wrap break-all">
                  {componentStack}
                </pre>
              </>
            )}
          </div>
        </details>
      )}
    </div>
  );
}
