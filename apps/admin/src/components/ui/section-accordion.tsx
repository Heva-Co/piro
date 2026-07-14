import { useState } from "react";
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from "@/components/ui/accordion";
import { cn } from "@/lib/utils";

interface SectionAccordionProps {
  title: React.ReactNode;
  /** Optional subtitle rendered below the title in the trigger */
  description?: string;
  /** Optional icon rendered to the left of the title */
  icon?: React.ReactNode;
  /** Buttons shown in the trigger row — only visible when the section is expanded */
  actions?: React.ReactNode;
  children?: React.ReactNode;
  defaultOpen?: boolean;
  titleClassName?: string;
  /** Marks the section as upcoming — disables interaction and shows a "Soon" badge */
  upcomming?: boolean;
  /**
   * Opts out of the standard bordered card box (rounded-xl border bg-card p-6) wrapping children.
   * Cards are the default — set this for sections that bring their own layout instead
   * (tables, logs, etc.).
   */
  disableCard?: boolean;
}

export function SectionAccordion({
  title,
  description,
  icon,
  actions,
  children,
  defaultOpen = false,
  titleClassName,
  upcomming = false,
  disableCard = false,
}: SectionAccordionProps) {
  const [open, setOpen] = useState(defaultOpen && !upcomming);

  return (
    <Accordion
      value={open ? ["item"] : []}
      onValueChange={(v) => setOpen(v.includes("item"))}
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
          <span className="flex flex-col gap-0.5 text-left flex-1 min-w-0">
            <span className="text-sm font-semibold flex items-center gap-2">
              {icon}
              {title}
              {upcomming && (
                <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                  Soon
                </span>
              )}
            </span>
            {description && (
              <span className="text-xs text-muted-foreground font-normal">{description}</span>
            )}
          </span>
          {actions && open && (
            <span
              className="flex items-center gap-2 mr-3"
              onClick={(e) => e.stopPropagation()}
            >
              {actions}
            </span>
          )}
        </AccordionTrigger>
        {!upcomming && (
          <AccordionContent className="pb-6">
            {disableCard ? children : (
              <div className="rounded-xl border bg-card p-6 flex flex-col gap-5">
                {children}
              </div>
            )}
          </AccordionContent>
        )}
      </AccordionItem>
    </Accordion>
  );
}
