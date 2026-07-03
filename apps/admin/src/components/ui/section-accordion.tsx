import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from "@/components/ui/accordion";
import { cn } from "@/lib/utils";

interface SectionAccordionProps {
  title: React.ReactNode;
  children?: React.ReactNode;
  defaultOpen?: boolean;
  titleClassName?: string;
  /** Marks the section as upcoming — disables interaction and shows a "Soon" badge */
  upcomming?: boolean;
}

export function SectionAccordion({
  title,
  children,
  defaultOpen = false,
  titleClassName,
  upcomming = false,
}: SectionAccordionProps) {
  return (
    <Accordion
      defaultValue={defaultOpen && !upcomming ? ["item"] : []}
      className="border-b border-border"
    >
      <AccordionItem value="item" disabled={upcomming} className="border-none">
        <AccordionTrigger
          className={cn(
            "py-4 hover:no-underline",
            upcomming && "cursor-default opacity-50 pointer-events-none",
            titleClassName
          )}
        >
          <span className="text-sm font-semibold flex items-center gap-2">
            {title}
            {upcomming && (
              <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                Soon
              </span>
            )}
          </span>
        </AccordionTrigger>
        {!upcomming && (
          <AccordionContent className="pb-6">
            {children}
          </AccordionContent>
        )}
      </AccordionItem>
    </Accordion>
  );
}
