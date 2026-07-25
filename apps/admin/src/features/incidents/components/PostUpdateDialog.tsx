import { AlertCircle, AlertTriangle } from "lucide-react";
import { Globe, Lock } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import type { IncidentVisibilityKey } from "@/constants/incidents";

const STATUS_SELECT_LABEL: Record<string, string> = {
  __NO_CHANGE__: "No status change",
  INVESTIGATING: "Investigating",
  IDENTIFIED: "Identified",
  MONITORING: "Monitoring",
  RESOLVED: "Resolved",
};

const STATUS_FLOW_ORDER = ["INVESTIGATING", "IDENTIFIED", "MONITORING", "RESOLVED"];

export const NO_STATUS_CHANGE = "__NO_CHANGE__";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  isPublic: boolean;
  currentStatusUpper: string;
  error: string;
  body: string;
  onBodyChange: (value: string) => void;
  status: string;
  onStatusChange: (value: string) => void;
  visibility: IncidentVisibilityKey;
  onVisibilityChange: (value: IncidentVisibilityKey) => void;
  isSubmitting: boolean;
  submitDisabled: boolean;
  onSubmit: () => void;
}

function PostUpdateDialog(props: Props) {
  const {
    open, onOpenChange, isPublic, currentStatusUpper, error,
    body, onBodyChange, status, onStatusChange, visibility, onVisibilityChange,
    isSubmitting, submitDisabled, onSubmit,
  } = props;

  const isBackwardStatusChange =
    status !== NO_STATUS_CHANGE &&
    STATUS_FLOW_ORDER.indexOf(status) < STATUS_FLOW_ORDER.indexOf(currentStatusUpper);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Post Update</DialogTitle>
          <DialogDescription>Add a status update to this incident's timeline.</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          {error && (
            <div className="flex items-center gap-2 text-sm text-destructive">
              <AlertCircle size={14} /> {error}
            </div>
          )}
          <Select value={status} onValueChange={(v) => v && onStatusChange(v)}>
            <SelectTrigger className="w-56">
              <SelectValue>{(value: string | null) => STATUS_SELECT_LABEL[value ?? ""] ?? value}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NO_STATUS_CHANGE}>No status change</SelectItem>
              {STATUS_FLOW_ORDER.map((s) => (
                <SelectItem key={s} value={s} disabled={s === currentStatusUpper}>
                  {STATUS_SELECT_LABEL[s]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {isBackwardStatusChange && (
            <div className="flex items-center gap-2 rounded-lg bg-yellow-500/10 border border-yellow-500/30 px-3 py-2 text-xs text-yellow-700 dark:text-yellow-500">
              <AlertTriangle size={14} className="shrink-0" />
              <span>
                This moves the incident backward, from <strong>{STATUS_SELECT_LABEL[currentStatusUpper]}</strong> to{" "}
                <strong>{STATUS_SELECT_LABEL[status]}</strong>.
              </span>
            </div>
          )}
          <MarkdownEditor
            value={body}
            onChange={onBodyChange}
            placeholder="Describe the current situation… (optional — you can post a status change alone)"
          />
          {isPublic ? (
            <label className="flex items-center gap-2 text-xs text-muted-foreground cursor-pointer select-none">
              <Switch
                checked={visibility === "Public"}
                onCheckedChange={(checked) => onVisibilityChange(checked ? "Public" : "Private")}
              />
              {visibility === "Public" ? (
                <span className="flex items-center gap-1 text-foreground font-medium"><Globe size={12} /> Visible on status page</span>
              ) : (
                <span className="flex items-center gap-1"><Lock size={12} /> Internal only</span>
              )}
            </label>
          ) : (
            <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Lock size={12} /> This incident is private — updates stay internal until published.
            </span>
          )}
        </div>
        <DialogFooter>
          <Button onClick={onSubmit} disabled={isSubmitting || submitDisabled}>
            {isSubmitting ? "Posting…" : "Post Update"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default PostUpdateDialog;
