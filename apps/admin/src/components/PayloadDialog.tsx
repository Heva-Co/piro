import { useMemo, useState } from "react";
import { Check, Copy } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { hljs } from "@/lib/highlight";
import "@/lib/highlight.css";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  description?: string;
  payload: string;
}

/** Formats a raw JSON string for display, falling back to the raw text if it doesn't parse. */
function formatPayload(payload: string): string {
  try {
    return JSON.stringify(JSON.parse(payload), null, 2);
  } catch {
    return payload;
  }
}

/** Read-only dialog for viewing a raw JSON payload (webhook body, config JSON, etc.), with copy and syntax highlighting. */
export function PayloadDialog(props: Props) {
  const { open, onOpenChange, title = "Payload", description = "Exact data received, unmodified.", payload } = props;
  const [copied, setCopied] = useState(false);
  const formatted = formatPayload(payload);
  const highlighted = useMemo(() => hljs.highlight(formatted, { language: "json" }).value, [formatted]);

  function handleCopy() {
    navigator.clipboard.writeText(payload);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <div className="relative">
          <pre
            className="max-h-[60vh] overflow-auto rounded-lg border bg-muted/30 p-3 text-xs font-mono whitespace-pre-wrap break-all"
            dangerouslySetInnerHTML={{ __html: highlighted }}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            className="absolute top-2 right-2"
            onClick={handleCopy}
          >
            {copied ? <Check size={14} /> : <Copy size={14} />}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
