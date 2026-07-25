import { cn } from "@/lib/utils";

interface Props {
  children: React.ReactNode;
  className?: string;
}

/**
 * Top-level wrapper for a page's content. Sits directly inside the layout's
 * scrollable <main> (which already provides page padding), so this only owns
 * the page's own width/flow — not outer spacing. Use it instead of a bare
 * <div> at the root of a page component.
 */
function PageContainer(props: Props) {
  const { children, className } = props;
  return <div className={cn("w-full", className)}>{children}</div>;
}

export default PageContainer;
