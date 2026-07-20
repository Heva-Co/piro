import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import axios from "axios";
import { toast } from "react-toastify";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usersApi } from "@/lib/api";
import { useCreatePostmortem } from "@/hooks/usePostmortems";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(255, "Max 255 characters"),
  reviewOwnerUserId: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function apiErrorMessage(err: unknown, fallback: string) {
  return (axios.isAxiosError(err) && (err.response?.data?.title || err.response?.data?.detail)) || fallback;
}

function PostmortemFormPage() {
  const navigate = useNavigate();
  const createPostmortem = useCreatePostmortem();
  const { data: users } = useQuery({ queryKey: QUERY_KEYS.USERS, queryFn: () => usersApi.list() });

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", reviewOwnerUserId: "" },
  });

  async function onSubmit(values: FormValues) {
    try {
      const created = await createPostmortem.mutateAsync({
        name: values.name,
        reviewOwnerUserId: values.reviewOwnerUserId ? Number(values.reviewOwnerUserId) : null,
        impactStartAt: null,
        impactEndAt: null,
      });
      toast.success("Postmortem created.");
      navigate(ROUTES.POSTMORTEMS.DETAIL(created.id));
    } catch (err) {
      toast.error(apiErrorMessage(err, "Failed to create postmortem."));
    }
  }

  return (
    <>
      <PageHeader
        breadcrumbs={[
          { label: "Postmortems", onClick: () => navigate(ROUTES.POSTMORTEMS.LIST) },
          { label: "New" },
        ]}
      />

      <div className="max-w-2xl rounded-xl border bg-card p-8">
        <h1 className="text-lg font-bold mb-6">New Postmortem</h1>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-5">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">
              Report name <span className="text-destructive">*</span>
            </label>
            <Input
              {...register("name")}
              placeholder="Q3 database outage review"
              aria-invalid={!!errors.name}
            />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-semibold">Owner of the review process</label>
            <Controller
              control={control}
              name="reviewOwnerUserId"
              render={({ field }) => (
                <Select
                  value={field.value || "none"}
                  onValueChange={(v) => field.onChange(!v || v === "none" ? "" : v)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Unassigned" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Unassigned</SelectItem>
                    {(users ?? []).map((u) => (
                      <SelectItem key={u.id} value={String(u.id)}>
                        {u.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            <p className="text-xs text-muted-foreground">
              The person accountable for running the review and driving action items to done.
            </p>
          </div>

          <div className="flex items-center gap-3 pt-2">
            <Button type="submit" disabled={createPostmortem.isPending}>
              {createPostmortem.isPending ? "Creating…" : "Create postmortem"}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate(ROUTES.POSTMORTEMS.LIST)}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </>
  );
}

export default PostmortemFormPage;
