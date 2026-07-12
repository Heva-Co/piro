import { useMemo, useState } from "react";
import { Button } from "../ui/button";
import { Input } from "../ui/input";
import { cn } from "@/lib/utils";

interface Props {
    objectName: string;
    objectId: string;
    onDelete: () => Promise<void>;
    /** "delete" (default) permanently removes the object; "cancel" stops/deactivates it without removing the record. */
    variant?: "delete" | "cancel";
}

const VARIANT_STYLES = {
    delete: {
        verb: "Delete",
        description: "Permanently delete",
        failure: "Failed to delete.",
        container: "border-destructive/30 bg-destructive/5",
        errorText: "text-destructive",
        ring: "focus-visible:ring-destructive/50",
        button: "bg-destructive text-white hover:bg-destructive/70",
    },
    cancel: {
        verb: "Cancel",
        description: "Cancel",
        failure: "Failed to cancel.",
        container: "border-amber-500/30 bg-amber-500/5",
        errorText: "text-amber-600 dark:text-amber-400",
        ring: "focus-visible:ring-amber-500/50",
        button: "bg-amber-500 text-white hover:bg-amber-500/70",
    },
};

function DangerZone(props: Props) {
    const { objectName, objectId, variant = "delete" } = props;
    const style = VARIANT_STYLES[variant];

    const [confirm, setConfirm] = useState("");
    const [isSubmitting, setSubmitting] = useState(false);
    const [error, setError] = useState("");

    async function handleAction() {
        if (confirm !== objectId) return;
        setSubmitting(true);
        setError("");
        try {
            await props.onDelete();
        } catch {
            setError(style.failure);
            setSubmitting(false);
        }
    }

    const isDisabled = useMemo(() => {
        return confirm !== objectId || isSubmitting;
    }, [confirm, isSubmitting, objectId]);

    return (
        <div className={cn("rounded-xl border p-6 flex flex-col gap-4", style.container)}>
            <p className="text-sm">
                {style.description} this {objectName}. Type{" "}
                <code className="font-mono font-semibold">{objectId}</code> to confirm.
            </p>
            {error && <p className={cn("text-sm", style.errorText)}>{error}</p>}
            <div className="flex items-center gap-3">
                <Input value={confirm}
                    onChange={(e) => setConfirm(e.target.value)}
                    className={cn("w-64", style.ring)} />
                <Button onClick={handleAction}
                    disabled={isDisabled}
                    className={style.button}>
                    {isSubmitting ? `${style.verb}ing…` : `${style.verb} ${objectName}`}
                </Button>
            </div>
        </div>
    );
}

export default DangerZone;