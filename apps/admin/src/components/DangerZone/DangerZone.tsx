import { Input } from "@base-ui/react";
import { useMemo, useState } from "react";
import { Button } from "../ui/button";

interface Props {
    objectName: string;
    objectId: string;
    onDelete: () => Promise<void>
}

function DangerZone(props: Props) {
    const { objectName, objectId } = props;

    const [confirm, setConfirm] = useState("");
    const [isDeleting, setDeleting] = useState(false);
    const [error, setError] = useState("");

    async function handleDelete() {
        if (confirm !== objectId) return;
        setDeleting(true);
        setError("");
        try {
            await props.onDelete();
        } catch {
            setError("Failed to delete.");
            setDeleting(false);
        }
    }

    const isDisabled = useMemo(() => {
        return confirm !== objectId || isDeleting
    }, [confirm, isDeleting, objectId])

    return (
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 flex flex-col gap-4">
            <p className="text-sm">
                Permanently delete this {objectName}. Type{" "}
                <code className="font-mono font-semibold">{objectId}</code> to confirm.
            </p>
            {error && <p className="text-sm text-destructive">{error}</p>}
            <div className="flex items-center gap-3">
                <Input value={confirm}
                    onValueChange={(v) => setConfirm(v)}
                    className="rounded-lg border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-destructive w-64" />
                <Button onClick={handleDelete}
                    disabled={isDisabled}
                    variant="destructive">
                    {isDeleting ? "Deleting…" : `Delete ${objectName}`}
                </Button>
            </div>
        </div>
    );
}

export default DangerZone;