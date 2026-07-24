import { Switch } from "@/components/ui/switch";
import type { Tag } from "@/lib/actions/tags";

/**
 * The assignable system tags an operator may toggle (RFC 0008 §4.2). Mirrors the backend SystemTags
 * catalog's Assignable entries; v1 ships only the key-only flag piro:3rd-party. A future valued flag
 * (e.g. piro:environment with AllowedValues) would render as a select here instead of a switch.
 */
const ASSIGNABLE_FLAGS: { key: string; label: string; description: string }[] = [
  {
    key: "piro:3rd-party",
    label: "Third-party dependency",
    description: "This is an external vendor service (e.g. Stripe, Twilio) monitored alongside your own.",
  },
];

interface Props {
  /** The entity's current tags (used only to read whether each flag is present). */
  tags: Tag[];
  disabled?: boolean;
  onToggle: (key: string, assigned: boolean) => void;
}

function AssignableSystemTags(props: Props) {
  const { tags, disabled = false, onToggle } = props;
  const present = new Set(tags.map((t) => t.key));

  return (
    <div className="flex flex-col gap-3">
      {ASSIGNABLE_FLAGS.map((flag) => (
        <div key={flag.key} className="flex items-start gap-3">
          <Switch
            checked={present.has(flag.key)}
            disabled={disabled}
            onCheckedChange={(v) => onToggle(flag.key, v)}
          />
          <div className="flex flex-col">
            <span className="text-sm font-medium">{flag.label}</span>
            <span className="text-xs text-muted-foreground">{flag.description}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

export default AssignableSystemTags;
