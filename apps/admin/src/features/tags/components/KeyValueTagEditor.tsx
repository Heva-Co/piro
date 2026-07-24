import { useRef, useState, type KeyboardEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { X } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Command, CommandList, CommandItem, CommandEmpty } from "@/components/ui/command";
import { tagsApi } from "@/lib/actions/tags";
import type { Tag } from "@/lib/actions/tags";
import { QUERY_KEYS } from "@/constants/api";
import { MAX_TAGS_PER_ENTITY, isSystemKey, validateUserKey, validateValue } from "@/features/tags/validations";

interface Props {
  /** The full tag set (user + reconciled system). System tags render read-only; user tags are editable chips. */
  tags: Tag[];
  /** Called with the edited USER-tag set only (system rows are never emitted). */
  onChange: (userTags: Tag[]) => void;
  /** Key validator; defaults to user-tag rules. Required-worker-tags pass a looser one that allows piro:*. */
  validateKey?: (key: string) => string | null;
  /** Placeholder shown in the input. */
  placeholder?: string;
  /**
   * When true (default), reconciled piro:* tags render as read-only chips and are never edited/emitted.
   * Required-worker-tags set this false: there, a piro:* key (e.g. piro:region) is an editable requirement.
   */
  systemReadOnly?: boolean;
}

/**
 * Splits a raw "key=value" (or key-only) entry. The first '=' separates key from value. '=' (not ':') is
 * the separator because a key may itself contain a colon (e.g. the system key "piro:region"), so splitting
 * on ':' would mangle it. A value may contain further '=' characters.
 */
function parseEntry(raw: string): { key: string; value: string | null } {
  const trimmed = raw.trim();
  const eq = trimmed.indexOf("=");
  if (eq < 0) return { key: trimmed, value: null };
  return { key: trimmed.slice(0, eq).trim(), value: trimmed.slice(eq + 1).trim() || null };
}

function KeyValueTagEditor(props: Props) {
  const {
    tags, onChange, validateKey = validateUserKey,
    placeholder = "add a tag, e.g. team=payments", systemReadOnly = true,
  } = props;
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [focused, setFocused] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const systemTags = systemReadOnly ? tags.filter((t) => isSystemKey(t.key)) : [];
  const userTags = systemReadOnly ? tags.filter((t) => !isSystemKey(t.key)) : tags;

  const { data: keySuggestions = [] } = useQuery({
    queryKey: QUERY_KEYS.TAG_KEYS(""),
    queryFn: () => tagsApi.keys(),
  });

  // Suggest only while typing the KEY portion (before any '='), matching what's typed, minus keys already used.
  const typingKey = !text.includes("=");
  const usedKeys = new Set(userTags.map((t) => t.key));
  const matches = typingKey
    ? keySuggestions.filter((k) => k.startsWith(text.trim()) && !usedKeys.has(k) && k !== text.trim())
    : [];
  const showDropdown = focused && text.trim().length > 0 && matches.length > 0;

  function commit(raw: string) {
    const value0 = raw.trim();
    if (!value0) return;
    if (userTags.length >= MAX_TAGS_PER_ENTITY) {
      setError(`Maximum ${MAX_TAGS_PER_ENTITY} tags reached.`);
      return;
    }
    const { key, value } = parseEntry(value0);
    const keyError = validateKey(key);
    if (keyError) { setError(keyError); return; }
    const valueError = validateValue(value);
    if (valueError) { setError(valueError); return; }

    // A key is unique per entity: replace an existing chip with the same key rather than duplicating.
    const next = userTags.filter((t) => t.key !== key);
    next.push({ key, value });
    onChange(next);
    setText("");
    setError(null);
  }

  function pickKey(key: string) {
    // Insert "key=" and keep focus so the user types the value; key-only tags just press Enter again.
    setText(`${key}=`);
    setError(null);
    inputRef.current?.focus();
  }

  function removeUserTag(key: string) {
    onChange(userTags.filter((t) => t.key !== key));
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault();
      commit(text);
    } else if (e.key === " " && text.includes("=")) {
      // Space commits once a full key=value is typed, so it doesn't interrupt typing the key or a
      // key-only tag (which is committed with Enter instead).
      e.preventDefault();
      commit(text);
    } else if (e.key === "Backspace" && text.length === 0 && userTags.length > 0) {
      removeUserTag(userTags[userTags.length - 1].key);
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="relative">
        <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-input bg-transparent p-2 focus-within:ring-1 focus-within:ring-ring">
          {systemTags.map((t) => (
            <Badge key={t.key} variant="secondary" className="font-mono" title="System tag (read-only)">
              {t.value != null ? `${t.key}=${t.value}` : t.key}
            </Badge>
          ))}
          {userTags.map((t) => (
            <Badge key={t.key} variant="outline" className="font-mono gap-1 pr-1">
              {t.value != null ? `${t.key}=${t.value}` : t.key}
              <button
                type="button"
                onClick={() => removeUserTag(t.key)}
                className="rounded-full p-0.5 hover:bg-muted"
                aria-label={`Remove ${t.key}`}
              >
                <X size={12} />
              </button>
            </Badge>
          ))}
          <input
            ref={inputRef}
            type="text"
            value={text}
            onChange={(e) => { setText(e.target.value); setError(null); }}
            onKeyDown={handleKeyDown}
            onFocus={() => setFocused(true)}
            // Delay so a click on a suggestion registers before the dropdown closes; commit on blur.
            onBlur={() => { setTimeout(() => setFocused(false), 120); commit(text); }}
            placeholder={userTags.length === 0 && systemTags.length === 0 ? placeholder : ""}
            className="flex-1 min-w-32 self-center bg-transparent py-1 font-mono text-sm text-foreground placeholder:text-muted-foreground outline-none"
          />
        </div>

        {showDropdown && (
          <Command className="absolute z-50 mt-1 w-full rounded-md border shadow-md">
            <CommandList>
              <CommandEmpty>No matching keys.</CommandEmpty>
              {matches.slice(0, 8).map((k) => (
                <CommandItem key={k} value={k} onSelect={() => pickKey(k)} className="font-mono">
                  {k}
                </CommandItem>
              ))}
            </CommandList>
          </Command>
        )}
      </div>

      {error && <p className="text-xs text-destructive">{error}</p>}
      <p className="text-xs text-muted-foreground">
        Type <span className="font-mono">key=value</span> (or just a key) and press Enter. Backspace removes the last tag.
      </p>
    </div>
  );
}

export default KeyValueTagEditor;
