import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import axios from "axios";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import DynamicConfigForm from "@/components/config-form/DynamicConfigForm";
import type { ConfigFieldSchema } from "@/lib/actions/checks";
import {
  integrationActionsApi,
  type ActionContext,
  type IntegrationActionDescriptor,
  type IntegrationActionResult,
} from "@/lib/actions/integration-actions";

interface Props {
  descriptor: IntegrationActionDescriptor;
  context: ActionContext;
  targetId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onExecuted: (result: IntegrationActionResult) => void;
}

function apiErrorMessage(err: unknown, fallback: string): string {
  return (
    (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback
  );
}

/**
 * The dialog for a single integration action (RFC 0012 §4.6). If the action `supportsDraft`, it fetches
 * a server-built draft first and seeds the form with it; otherwise it opens empty. The form is rendered
 * generically from `descriptor.inputSchema` via the shared DynamicConfigForm, and on confirm it POSTs to
 * the execute endpoint — the same DataAnnotations validated the schema, so form and payload can't drift.
 */
function ActionDialog(props: Props) {
  const { descriptor, context, targetId, open, onOpenChange, onExecuted } = props;

  const [values, setValues] = useState<Record<string, unknown>>({});

  // Draft: only fetched when the action declares supportsDraft — that flag is how the UI decides.
  const { data: draft, isLoading: draftLoading } = useQuery({
    queryKey: ["integration-action-draft", descriptor.integrationId, descriptor.actionId, context, targetId],
    queryFn: () =>
      integrationActionsApi.draft(descriptor.integrationId, descriptor.actionId, context, targetId),
    enabled: open && descriptor.supportsDraft,
  });

  useEffect(() => {
    if (!open) return;
    setValues(descriptor.supportsDraft && draft ? { ...draft } : {});
  }, [open, draft, descriptor.supportsDraft]);

  const executeMutation = useMutation({
    mutationFn: () =>
      integrationActionsApi.execute(descriptor.integrationId, descriptor.actionId, {
        context,
        targetId,
        input: values,
      }),
    onSuccess: (result) => {
      toast.success(`${descriptor.label}: ${result.label} created`);
      onExecuted(result);
      onOpenChange(false);
    },
    onError: (err) => toast.error(apiErrorMessage(err, `${descriptor.label} failed.`)),
  });

  const waitingForDraft = descriptor.supportsDraft && draftLoading;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{descriptor.label}</DialogTitle>
          {descriptor.description && <DialogDescription>{descriptor.description}</DialogDescription>}
        </DialogHeader>

        {waitingForDraft ? (
          <p className="py-6 text-sm text-muted-foreground">Loading…</p>
        ) : (
          <DynamicConfigForm
            schema={descriptor.inputSchema as ConfigFieldSchema[]}
            values={values}
            onChange={setValues}
          />
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={() => executeMutation.mutate()}
            disabled={executeMutation.isPending || waitingForDraft}
          >
            {executeMutation.isPending ? "…" : descriptor.label}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default ActionDialog;
