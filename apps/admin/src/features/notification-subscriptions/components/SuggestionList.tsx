interface Props {
  items: string[];
  onPick: (value: string) => void;
}

/**
 * A plain absolute-positioned suggestion dropdown for a single text input. Deliberately not built on
 * cmdk's Command: cmdk re-filters its items against its own internal query (empty here, since the input
 * lives outside it), which silently hid pre-filtered items. This list renders exactly what it's given.
 */
function SuggestionList(props: Props) {
  const { items, onPick } = props;
  return (
    <div className="absolute z-50 mt-1 w-full rounded-md border bg-popover shadow-md">
      {items.map((item) => (
        <button
          key={item}
          type="button"
          // onMouseDown (not onClick) so the pick registers before the input's onBlur closes the list.
          onMouseDown={(e) => { e.preventDefault(); onPick(item); }}
          className="block w-full px-3 py-1.5 text-left font-mono text-sm hover:bg-muted"
        >
          {item}
        </button>
      ))}
    </div>
  );
}

export default SuggestionList;
