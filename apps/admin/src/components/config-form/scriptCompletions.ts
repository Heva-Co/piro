import type { CompletionContext, CompletionResult, Completion } from "@codemirror/autocomplete";
import { snippetCompletion } from "@codemirror/autocomplete";

/**
 * Lightweight, context-aware autocomplete for the Script check editor (RFC 0010). Not a full TS
 * language service — a hand-authored CompletionSource that surfaces exactly the `piro:http` surface and
 * the check() return shape the operator works with, so it stays dependency-light. Heuristics look at the
 * text just before the cursor rather than a parsed AST, which is enough for this small, fixed API.
 */

// r.<here> — the http.get response object
const RESPONSE_MEMBERS: Completion[] = [
    { label: "statusCode", type: "property", info: "HTTP status code (number)" },
    { label: "body", type: "property", info: "Raw response body (string)" },
    { label: "json", type: "property", info: "Parsed JSON body, or null when the body isn't JSON" },
    { label: "headers", type: "property", info: "Response headers, keyed by name (string values)" },
];

// http.<here>
const HTTP_MEMBERS: Completion[] = [
    snippetCompletion("get(${url})", {
        label: "get",
        type: "method",
        detail: "(url, options?)",
        info: "GET a URL. Returns { statusCode, body, json, headers }. options: { headers?, timeoutMs? }",
    }),
];

// Return-object keys inside `return { <here> }`
const RETURN_KEYS: Completion[] = [
    { label: "up", type: "property", info: "true → Up, false → Down (required)" },
    { label: "message", type: "property", info: "Human-readable detail for a Down/Error (optional)" },
    { label: "dimensions", type: "property", info: "Numeric metrics for the alert policy to threshold, e.g. { Severity: 1 }" },
];

// Top-level identifiers / starters
const TOP_LEVEL: Completion[] = [
    snippetCompletion("import http from 'piro:http';", { label: "import http", type: "keyword", info: "Import the piro:http module" }),
    snippetCompletion(
        "export function check() {\n\tconst r = http.get('${url}');\n\treturn { up: r.statusCode === 200 };\n}",
        { label: "check", type: "function", info: "The check() entry point Piro calls" }
    ),
    { label: "http", type: "variable", info: "The imported piro:http module" },
];

export function scriptCompletionSource(context: CompletionContext): CompletionResult | null {
    const before = context.state.sliceDoc(0, context.pos);

    // `http.` or `something.get(...).` → module methods
    if (/\bhttp\.\w*$/.test(before)) {
        const word = context.matchBefore(/\w*$/);
        return { from: word?.from ?? context.pos, options: HTTP_MEMBERS, validFor: /^\w*$/ };
    }

    // `<ident>.` where the ident was assigned from an http.get(...) call → response members.
    const memberMatch = before.match(/\b([A-Za-z_$][\w$]*)\.\w*$/);
    if (memberMatch) {
        const varName = memberMatch[1];
        const assignedFromGet = new RegExp(`\\b(const|let|var)\\s+${varName}\\s*=\\s*http\\.get\\b`).test(before)
            || new RegExp(`\\b${varName}\\s*=\\s*http\\.get\\b`).test(before);
        if (assignedFromGet || varName === "r") {
            const word = context.matchBefore(/\w*$/);
            return { from: word?.from ?? context.pos, options: RESPONSE_MEMBERS, validFor: /^\w*$/ };
        }
    }

    // Inside a `return { ... }` — offer the verdict keys. Rough: the nearest `return {` isn't closed yet.
    const lastReturn = before.lastIndexOf("return");
    if (lastReturn !== -1) {
        const tail = before.slice(lastReturn);
        const opens = (tail.match(/{/g) ?? []).length;
        const closes = (tail.match(/}/g) ?? []).length;
        if (opens > closes) {
            const word = context.matchBefore(/\w*$/);
            if (word && (word.text.length > 0 || context.explicit)) {
                return { from: word.from, options: RETURN_KEYS, validFor: /^\w*$/ };
            }
        }
    }

    // Otherwise, only offer top-level starters on an explicit trigger or a word start (avoid noise).
    const word = context.matchBefore(/[A-Za-z_$][\w$]*$/);
    if (!word && !context.explicit) return null;
    return { from: word?.from ?? context.pos, options: TOP_LEVEL, validFor: /^[A-Za-z_$][\w$]*$/ };
}
