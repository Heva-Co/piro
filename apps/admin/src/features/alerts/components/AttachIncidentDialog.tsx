import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { Incident } from "@/lib/actions/incidents";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  openIncidents: Incident[];
  selectedIncidentId: string;
  onSelect: (id: string) => void;
  onAttach: () => void;
  isPending: boolean;
}

function AttachIncidentDialog(props: Props) {
  const { open, onOpenChange, openIncidents, selectedIncidentId, onSelect, onAttach, isPending } = props;

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) onOpenChange(false); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Attach to incident</DialogTitle>
          <DialogDescription>Select an open incident to attach this alert's service to.</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          {openIncidents.length === 0 ? (
            <p className="text-sm text-muted-foreground">No open incidents.</p>
          ) : (
            <Select value={selectedIncidentId} onValueChange={(v) => onSelect(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select incident…" />
              </SelectTrigger>
              <SelectContent>
                {openIncidents.map((inc) => (
                  <SelectItem key={inc.id} value={String(inc.id)}>
                    #{inc.id} — {inc.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!selectedIncidentId || isPending} onClick={onAttach}>
            {isPending ? "Attaching…" : "Attach"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default AttachIncidentDialog;
