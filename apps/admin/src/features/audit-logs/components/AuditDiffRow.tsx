import type { DiffLine } from "../lib/diff";

interface Props {
  line: DiffLine;
}

/**
 * The one or two table rows a single property contributes to a unified diff: a `-` row when it had a
 * previous value, a `+` row when it has a new one. An unchanged property renders as one neutral row.
 */
function AuditDiffRow(props: Props) {
  const { line } = props;

  if (line.kind === "unchanged") {
    return (
      <tr className="border-b border-border/50 last:border-0">
        <td className="w-8 select-none px-2 py-1 text-center text-muted-foreground/50">&nbsp;</td>
        <td className="w-52 px-2 py-1 align-top text-muted-foreground">{line.property}</td>
        <td className="px-2 py-1 break-all text-muted-foreground">{line.after}</td>
      </tr>
    );
  }

  return (
    <>
      {line.before !== null && (
        <tr className="bg-destructive/10">
          <td className="w-8 select-none px-2 py-1 text-center text-destructive">-</td>
          <td className="w-52 px-2 py-1 align-top text-foreground">{line.property}</td>
          <td className="px-2 py-1 break-all text-foreground">{line.before}</td>
        </tr>
      )}
      {line.after !== null && (
        <tr className="bg-emerald-500/10">
          <td className="w-8 select-none px-2 py-1 text-center text-emerald-600 dark:text-emerald-400">+</td>
          <td className="w-52 px-2 py-1 align-top text-foreground">{line.property}</td>
          <td className="px-2 py-1 break-all text-foreground">{line.after}</td>
        </tr>
      )}
    </>
  );
}

export default AuditDiffRow;
