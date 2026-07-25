import { useState, useEffect } from "react";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save } from "lucide-react";
import { useCheck, useUpdateCheck, useCheckTypeLabel } from "@/hooks/useChecks";
import { CheckGeneralSettingsFields } from "@/features/checks/components/shared/CheckGeneralSettingsFields";
import { checkConfigSchema, type CheckConfigFormValues } from "@/features/checks/validations";
import { CRON_PRESETS } from "@/constants/checks";

interface Props {
  serviceSlug: string;
  checkSlug: string;
}

function GeneralSettingsSection(props: Props) {
  const { serviceSlug, checkSlug } = props;

  const { data: check } = useCheck(serviceSlug, checkSlug);
  const updateCheck = useUpdateCheck(serviceSlug, checkSlug);
  const typeLabel = useCheckTypeLabel();
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");

  // This section edits only the type-general fields; per-type config lives in ConfigurationSection,
  // so `config` stays an empty object here (never read on this form).
  const methods = useForm<CheckConfigFormValues>({
    resolver: zodResolver(checkConfigSchema),
    defaultValues: {
      name: "",
      slug: "",
      description: "",
      cron: "* * * * *",
      showCustomCron: false,
      isActive: true,
      type: "HTTP",
      config: {},
      integrationId: "",
    },
  });

  useEffect(() => {
    if (!check) return;
    const isPreset = CRON_PRESETS.some((p) => p.value === check.cron);
    methods.reset({
      name: check.name,
      slug: check.slug,
      description: check.description ?? "",
      cron: check.cron ?? "* * * * *",
      showCustomCron: !isPreset,
      isActive: check.isActive,
      type: check.type,
      config: {},
      integrationId: check.integrationId != null ? String(check.integrationId) : "",
    });
  }, [check, methods]);

  async function handleSave(values: CheckConfigFormValues) {
    setError("");
    try {
      await updateCheck.mutateAsync({
        name: values.name,
        description: values.description || undefined,
        cron: values.cron,
        isActive: values.isActive,
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("Failed to save changes.");
    }
  }

  const typeNode = (
    <input
      value={check ? typeLabel(check.type) : ""}
      readOnly
      className="rounded-lg border bg-muted px-3 py-2 text-sm text-muted-foreground outline-none h-9 w-full"
    />
  );

  return (
    <FormProvider {...methods}>
      <form
        onSubmit={methods.handleSubmit(handleSave, () => setError("Please fix the highlighted fields before saving."))}
        className="flex flex-col gap-5"
      >
        {error && (
          <div className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}
        <CheckGeneralSettingsFields typeNode={typeNode} />
        <div className="flex justify-end">
          <button
            type="submit"
            disabled={updateCheck.isPending || !methods.formState.isDirty}
            className="flex items-center gap-2 rounded-lg bg-foreground text-background px-4 py-2 text-sm font-medium hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition-opacity"
          >
            <Save size={14} />
            {saved ? "Saved!" : updateCheck.isPending ? "Saving…" : "Save changes"}
          </button>
        </div>
      </form>
    </FormProvider>
  );
}

export default GeneralSettingsSection;
