import { AlertCircle, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@/components/ui/select";
import { IMPACT_OPTIONS } from "@/constants/serviceStatus";
import type { Service } from "@/lib/actions/services";
import type { IncidentService } from "@/lib/actions/incidents";

export interface ServiceImpact {
  slug: string;
  impact: string;
}

interface Props {
  allServices: Service[];
  incidentServices: IncidentService[];
  serviceImpacts: ServiceImpact[];
  isResolved: boolean;
  impactError: string;
  hasImpactChanged: boolean;
  isSaving: boolean;
  onToggleService: (slug: string) => void;
  onSetImpact: (slug: string, impact: string) => void;
  onSave: () => void;
}

function IncidentImpactSection(props: Props) {
  const {
    allServices, incidentServices, serviceImpacts, isResolved,
    impactError, hasImpactChanged, isSaving, onToggleService, onSetImpact, onSave,
  } = props;

  function isSelected(slug: string) {
    return serviceImpacts.some((s) => s.slug === slug);
  }

  return (
    <div className="rounded-xl border border-border bg-card overflow-hidden">
      <div className="divide-y divide-border">
        {allServices.length === 0 ? (
          <p className="px-5 py-8 text-center text-sm text-muted-foreground">No services found.</p>
        ) : (
          allServices.map((svc) => {
            const selected = isSelected(svc.slug);
            const impact = serviceImpacts.find((s) => s.slug === svc.slug)?.impact ?? "DEGRADED";
            const triggeringCheckSlug = incidentServices.find((s) => s.serviceSlug === svc.slug)?.triggeringCheckSlug;
            return (
              <div key={svc.slug} className="flex items-center gap-4 px-5 py-3">
                <Checkbox
                  id={`svc-${svc.slug}`}
                  checked={selected}
                  onCheckedChange={() => onToggleService(svc.slug)}
                  disabled={isResolved}
                />
                <label htmlFor={`svc-${svc.slug}`} className="flex-1 text-sm font-medium cursor-pointer select-none">
                  {svc.name}
                  <span className="ml-2 text-xs text-muted-foreground font-normal">{svc.slug}</span>
                  {triggeringCheckSlug && (
                    <span className="ml-2 text-xs text-muted-foreground font-normal">
                      · triggered by <span className="font-mono">{triggeringCheckSlug}</span>
                    </span>
                  )}
                </label>
                {selected && (
                  <Select value={impact} onValueChange={(v) => v && onSetImpact(svc.slug, v)} disabled={isResolved}>
                    <SelectTrigger className="w-40 h-8 text-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {IMPACT_OPTIONS.map((opt) => (
                        <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            );
          })
        )}
      </div>

      {!isResolved && (
        <div className="flex items-center justify-between gap-4 px-5 py-3 border-t border-border bg-muted/30">
          <div>
            {impactError && (
              <p className="text-xs text-destructive flex items-center gap-1">
                <AlertCircle size={12} /> {impactError}
              </p>
            )}
          </div>
          <Button onClick={onSave} disabled={!hasImpactChanged || isSaving}>
            <Save size={14} />
            {isSaving ? "Saving…" : "Save Impact"}
          </Button>
        </div>
      )}
    </div>
  );
}

export default IncidentImpactSection;
