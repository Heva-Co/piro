import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tooltip, TooltipTrigger, TooltipContent } from "@/components/ui/tooltip";
import { useService, useUpdateService } from "@/hooks/useServices";
import { escalationApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { Save, TriangleAlert } from "lucide-react";
import { useEffect, useState } from "react";

const NO_POLICY = "__none__";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100, "Max 100 characters"),
  description: z.string().max(500, "Max 500 characters").optional(),
  displayOrder: z.number().int("Must be an integer").min(0, "Must be 0 or greater"),
  isHidden: z.boolean(),
  escalationPolicyId: z.string(),
});

type FormValues = z.infer<typeof schema>;

function GeneralSettingsSection({ slug }: { slug: string }) {
  const { data: service } = useService(slug);
  const updateService = useUpdateService(slug);
  const [saved, setSaved] = useState(false);

  const { data: policiesPage } = useQuery({
    queryKey: QUERY_KEYS.ESCALATION_POLICIES,
    queryFn: () => escalationApi.list({ pageSize: 200 }),
  });
  const policies = policiesPage?.items ?? [];

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: service?.name ?? "",
      description: service?.description ?? "",
      displayOrder: service?.displayOrder ?? 0,
      isHidden: service?.isHidden ?? false,
      escalationPolicyId: service?.escalationPolicyId != null ? String(service.escalationPolicyId) : NO_POLICY,
    },
  });

  useEffect(() => {
    if (service) {
      reset({
        name: service.name,
        description: service.description ?? "",
        displayOrder: service.displayOrder,
        isHidden: service.isHidden,
        escalationPolicyId: service.escalationPolicyId != null ? String(service.escalationPolicyId) : NO_POLICY,
      });
    }
  }, [service, reset]);

  async function onSubmit(values: FormValues) {
    // null clears the assignment; a number (re)assigns it. Always sent — this form always has an opinion.
    const escalationPolicyId = values.escalationPolicyId === NO_POLICY ? null : Number(values.escalationPolicyId);
    await updateService.mutateAsync({
      name: values.name,
      description: values.description || undefined,
      displayOrder: values.displayOrder,
      isHidden: values.isHidden,
      escalationPolicyId,
    });
    setSaved(true);
    reset(values);
    setTimeout(() => setSaved(false), 3000);
  }

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      className="flex flex-col gap-5"
    >
      {updateService.isError && (
        <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          Failed to save changes.
        </div>
      )}

      {/* Name + Slug */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">
            Name <span className="text-destructive">*</span>
          </label>
          <Input {...register("name")} aria-invalid={!!errors.name} />
          {errors.name && (
            <p className="text-xs text-destructive">{errors.name.message}</p>
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Slug</label>
          <Input value={slug} disabled />
          <p className="text-xs text-muted-foreground">Cannot be changed after creation</p>
        </div>
      </div>

      {/* Description */}
      <div className="flex flex-col gap-1.5">
        <label className="text-sm font-semibold">Description</label>
        <Textarea
          {...register("description")}
          rows={3}
          placeholder="A brief description of this service"
          aria-invalid={!!errors.description}
        />
        {errors.description && (
          <p className="text-xs text-destructive">{errors.description.message}</p>
        )}
      </div>

      {/* Display Order + Hidden toggle */}
      <div className="grid grid-cols-2 gap-4">
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-semibold">Display Order</label>
          <Controller
            name="displayOrder"
            control={control}
            render={({ field }) => (
              <Input
                type="number"
                value={field.value}
                onChange={(e) => field.onChange(e.target.valueAsNumber)}
                aria-invalid={!!errors.displayOrder}
              />
            )}
          />
          {errors.displayOrder ? (
            <p className="text-xs text-destructive">{errors.displayOrder.message}</p>
          ) : (
            <p className="text-xs text-muted-foreground">Lower numbers appear first</p>
          )}
        </div>
        <div className="flex flex-col gap-2">
          <label className="text-sm font-semibold">Hidden from Public Page</label>
          <div className="flex items-center gap-2.5">
            <Controller
              name="isHidden"
              control={control}
              render={({ field }) => (
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              )}
            />
            <span className="text-sm">
              <Controller
                name="isHidden"
                control={control}
                render={({ field }) => <>{field.value ? "Hidden" : "Visible"}</>}
              />
            </span>
          </div>
          <p className="text-xs text-muted-foreground">
            Hidden services won't appear on the status page
          </p>
        </div>
      </div>

      {/* Escalation policy */}
      <div className="flex flex-col gap-1.5 w-1/2">
        <label className="text-sm font-semibold flex items-center gap-1.5">
          Escalation Policy
          <Controller
            name="escalationPolicyId"
            control={control}
            render={({ field }) => (
              <>
                {field.value === NO_POLICY && (
                  <Tooltip>
                    <TooltipTrigger render={<span className="inline-flex" />}>
                      <TriangleAlert size={14} className="text-amber-500" />
                    </TooltipTrigger>
                    <TooltipContent>
                      No escalation policy assigned — if an alert fires for one of this service's
                      checks, on-call won't be notified.
                    </TooltipContent>
                  </Tooltip>
                )}
              </>
            )}
          />
        </label>
        <Controller
          name="escalationPolicyId"
          control={control}
          render={({ field }) => {
            const selectedName = policies.find((p) => String(p.id) === field.value)?.name;
            return (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="None">
                    {field.value === NO_POLICY ? "None" : selectedName ?? "None"}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_POLICY}>None</SelectItem>
                  {policies.map((p) => (
                    <SelectItem key={p.id} value={String(p.id)}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            );
          }}
        />
        <p className="text-xs text-muted-foreground">
          On-call escalation used when an alert fires for one of this service's checks
        </p>
      </div>

      {/* Save */}
      <div className="flex justify-end">
        <Button
          type="submit"
          disabled={updateService.isPending || !isDirty}
        >
          <Save size={14} />
          {saved ? "Saved!" : updateService.isPending ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}

export default GeneralSettingsSection;
