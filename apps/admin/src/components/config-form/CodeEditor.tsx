import { useEffect, useMemo, useState } from "react";
import CodeMirror from "@uiw/react-codemirror";
import { EditorView } from "@codemirror/view";
import { javascript } from "@codemirror/lang-javascript";
import { autocompletion, type CompletionSource } from "@codemirror/autocomplete";
import { cn } from "@/lib/utils";

interface Props {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
    /** Optional context-aware completions (e.g. the piro:http API for the Script check). */
    completionSource?: CompletionSource;
}

/**
 * A JavaScript code editor (CodeMirror 6) for `ConfigFieldType.Code` fields — today the Script check's
 * script body (RFC 0010). Styled to match the shadcn Textarea (rounded border, transparent surface,
 * focus ring) so it reads as a native form control, and theme-aware via the admin's `.dark` class.
 */
function CodeEditor(props: Props) {
    const { value, onChange, placeholder, completionSource } = props;
    const [isDark, setIsDark] = useState(() => document.documentElement.classList.contains("dark"));

    useEffect(() => {
        const root = document.documentElement;
        const observer = new MutationObserver(() => setIsDark(root.classList.contains("dark")));
        observer.observe(root, { attributes: true, attributeFilter: ["class"] });
        return () => observer.disconnect();
    }, []);

    // Make CodeMirror blend into the shadcn control: transparent surface (the wrapper paints the bg),
    // no editor outline (the wrapper owns the focus ring), and muted, thin gutter.
    const surface = useMemo(
        () =>
            EditorView.theme({
                "&": { backgroundColor: "transparent", fontSize: "0.8125rem" },
                "&.cm-focused": { outline: "none" },
                ".cm-scroller": { fontFamily: "var(--font-mono, ui-monospace, monospace)", lineHeight: "1.6" },
                ".cm-gutters": { backgroundColor: "transparent", border: "none", color: "var(--muted-foreground)" },
                ".cm-activeLineGutter, .cm-activeLine": { backgroundColor: "color-mix(in oklab, var(--muted) 40%, transparent)" },
                ".cm-content": { padding: "8px 0" },
                "&.cm-editor": { borderRadius: "inherit" },
            }),
        []
    );

    const extensions = useMemo(() => {
        const exts = [javascript(), surface];
        if (completionSource) exts.push(autocompletion({ override: [completionSource] }));
        return exts;
    }, [surface, completionSource]);

    return (
        <div
            className={cn(
                "rounded-lg border border-input bg-transparent dark:bg-input/30 overflow-hidden transition-colors",
                "focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50"
            )}
        >
            <CodeMirror
                value={value}
                onChange={onChange}
                placeholder={placeholder}
                theme={isDark ? "dark" : "light"}
                extensions={extensions}
                minHeight="220px"
                basicSetup={{
                    lineNumbers: true,
                    bracketMatching: true,
                    closeBrackets: true,
                    // When a custom source is supplied, disable basicSetup's built-in completion so ours
                    // is the sole provider (otherwise both run and the generic word-completer adds noise).
                    autocompletion: !completionSource,
                    highlightActiveLine: true,
                    foldGutter: false,
                }}
            />
        </div>
    );
}

export default CodeEditor;
