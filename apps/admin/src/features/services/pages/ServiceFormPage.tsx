import { useNavigate } from "react-router-dom";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { useCreateService } from "@/hooks/useServices";
import { ROUTES } from "@/constants/routes";
import { slugify } from "@/utils/slugify";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100, "Max 100 characters"),
  slug: z
    .string()
    .min(1, "Slug is required")
    .max(100, "Max 100 characters")
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Only lowercase letters, numbers and hyphens"),
  description: z.string().max(500, "Max 500 characters").optional(),
  isHidden: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

export default function ServiceFormPage() {
  const navigate = useNavigate();
  const createService = useCreateService();

  const {
    register,
    handleSubmit,
    setValue,
    control,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", slug: "", description: "", isHidden: false },
  });

  function handleNameChange(e: React.ChangeEvent<HTMLInputElement>) {
    const name = e.target.value;
    setValue("name", name, { shouldValidate: true });
    setValue("slug", slugify(name), { shouldValidate: false });
  }

  async function onSubmit(values: FormValues) {
    const service = await createService.mutateAsync({
      slug: values.slug,
      name: values.name,
      description: values.description || undefined,
      isHidden: values.isHidden,
    } as Parameters<typeof createService.mutateAsync>[0]);
    navigate(ROUTES.SERVICES.DETAIL(service.slug));
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Services", onClick: () => navigate(ROUTES.SERVICES.LIST) },
          { label: "New" },
        ]}
      />

      <div className="max-w-2xl rounded-xl border bg-card p-8">
        <h1 className="text-lg font-bold mb-6">New Service</h1>

        {createService.isError && (
          <div className="mb-4 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            Failed to create service.
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-5">
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">
                Name <span className="text-destructive">*</span>
              </label>
              <Input
                {...register("name")}
                onChange={handleNameChange}
                placeholder="My Service"
                aria-invalid={!!errors.name}
              />
              {errors.name && (
                <p className="text-xs text-destructive">{errors.name.message}</p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold">
                Slug <span className="text-destructive">*</span>
              </label>
              <Input
                {...register("slug")}
                placeholder="my-service"
                className="font-mono"
                aria-invalid={!!errors.slug}
              />
              {errors.slug && (
                <p className="text-xs text-destructive">{errors.slug.message}</p>
              )}
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">Description</label>
            <Textarea
              {...register("description")}
              rows={3}
              placeholder="Optional description"
              aria-invalid={!!errors.description}
            />
            {errors.description && (
              <p className="text-xs text-destructive">{errors.description.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold">Hidden from Public Page</label>
            <div className="flex items-center gap-2.5">
              <Controller
                name="isHidden"
                control={control}
                render={({ field }) => (
                  <>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                    <span className="text-sm">{field.value ? "Hidden" : "Visible"}</span>
                  </>
                )}
              />
            </div>
            <p className="text-xs text-muted-foreground">
              Hidden services won't appear on the status page
            </p>
          </div>

          <div className="ml-auto flex items-center gap-3 pt-1">
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate(ROUTES.SERVICES.LIST)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={createService.isPending}>
              {createService.isPending ? "Creating…" : "Create Service"}
            </Button>
          </div>
        </form>
      </div>
    </>
  );
}
