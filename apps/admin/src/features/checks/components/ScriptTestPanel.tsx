import { useState } from "react";
import { FlaskConical, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { checksApi, type ScriptTestResult } from "@/lib/actions/checks";

interface Props {
    serviceSlug: string;
    checkSlug: string;
    /** Returns the current (possibly unsaved) config as a JSON string, so the operator tests live edits. */
    getTypeDataJson: () => string;
}

const OUTCOME_STYLE: Record<string, string> = {
    Up: "bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-400",
    Down: "bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-400",
    Error: "bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-400",
};

/**
 * Dry-runs a Script check against the live target and shows the raw verdict + captured console.log,
 * without persisting a datapoint or firing an alert (RFC 0010). Distinct from the detail page's
 * "Run now", which triggers the real, persisted scheduled run. The panel shows the raw outcome
 * (Up/Down/Error), never a severity — severity is the alert policy's decision once the check is saved.
 */
function ScriptTestPanel(props: Props) {
    const { serviceSlug, checkSlug, getTypeDataJson } = props;
    const [result, setResult] = useState<ScriptTestResult | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [running, setRunning] = useState(false);

    async function runTest() {
        setRunning(true);
        setError(null);
        try {
            setResult(await checksApi.test(serviceSlug, checkSlug, getTypeDataJson()));
        } catch (e) {
            setError(e instanceof Error ? e.message : "Test run failed.");
            setResult(null);
        } finally {
            setRunning(false);
        }
    }

    const dimensions = result?.dimensions ? Object.entries(result.dimensions) : [];

    return (
        <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
                <p className="text-xs text-muted-foreground">
                    Runs the script against the live target with console.log captured. Nothing is saved and no alert fires.
                </p>
                <Button size="sm" variant="outline" onClick={runTest} disabled={running}>
                    {running ? <Loader2 className="size-3.5 animate-spin" /> : <FlaskConical className="size-3.5" />}
                    Test
                </Button>
            </div>

            {error && (
                <div className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">
                    {error}
                </div>
            )}

            {result && (
                <div className="rounded-lg border bg-card p-4 flex flex-col gap-3">
                    <div className="flex items-center gap-3 flex-wrap">
                        <Badge className={OUTCOME_STYLE[result.outcome] ?? ""}>{result.outcome}</Badge>
                        {result.latencyMs != null && (
                            <span className="text-xs text-muted-foreground">{Math.round(result.latencyMs)} ms</span>
                        )}
                        {dimensions.map(([name, value]) => (
                            <span key={name} className="text-xs text-muted-foreground">
                                {name}: {value}
                            </span>
                        ))}
                    </div>

                    {result.message && <p className="text-sm">{result.message}</p>}

                    {result.logs.length > 0 && (
                        <pre className="max-h-48 overflow-auto rounded bg-muted p-3 text-xs font-mono whitespace-pre-wrap">
                            {result.logs.join("\n")}
                        </pre>
                    )}
                </div>
            )}
        </div>
    );
}

export default ScriptTestPanel;
