import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { PageHeader } from "@/components/PageHeader";
import Saml2ProviderForm from "../components/Saml2ProviderForm";
import { samlApi, type UpsertSaml2Provider } from "@/lib/actions/saml";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const EMPTY_FORM: UpsertSaml2Provider = {
  id: "",
  displayName: "",
  idpEntityId: "",
  idpSsoUrl: "",
  idpSigningCertificate: "",
  spEntityId: "",
  allowedDomains: "",
  defaultRole: "Viewer",
  isEnabled: true,
};

function Saml2ProviderFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [testing, setTesting] = useState(false);

  const { data: providers = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.SAML_CONFIGS,
    queryFn: samlApi.list,
  });

  const provider = isEdit ? providers.find((p) => p.id === id) : undefined;

  const upsertMutation = useMutation({
    mutationFn: (data: UpsertSaml2Provider) => samlApi.upsert(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.SAML_CONFIGS });
      navigate(ROUTES.CONFIG.SSO);
    },
  });

  async function handleTest(providerId: string) {
    if (!providerId) return;
    setTesting(true);
    setTestResult(null);
    try {
      const result = await samlApi.test(providerId);
      setTestResult(result);
    } catch (err) {
      const message =
        axios.isAxiosError(err) && err.response?.data?.message
          ? err.response.data.message
          : "Validation failed.";
      setTestResult({ success: false, message });
    } finally {
      setTesting(false);
    }
  }

  if (isEdit && isLoading) {
    return (
      <div className="max-w-4xl">
        <div className="text-sm text-muted-foreground">Loading…</div>
      </div>
    );
  }

  if (isEdit && !provider) {
    return (
      <div className="max-w-4xl">
        <div className="text-sm text-destructive">Provider not found.</div>
      </div>
    );
  }

  const initial: UpsertSaml2Provider = provider
    ? {
        id: provider.id,
        displayName: provider.displayName,
        idpEntityId: provider.idpEntityId,
        idpSsoUrl: provider.idpSsoUrl,
        idpSigningCertificate: "",
        spEntityId: provider.spEntityId ?? "",
        allowedDomains: provider.allowedDomains ?? "",
        defaultRole: provider.defaultRole,
        isEnabled: provider.isEnabled,
      }
    : EMPTY_FORM;

  return (
    <div className="max-w-4xl">
      <PageHeader
        breadcrumbs={[
          { label: "Single Sign-On", onClick: () => navigate(ROUTES.CONFIG.SSO) },
          { label: isEdit ? "Edit SAML Provider" : "Add SAML Provider" },
        ]}
      />
      <Saml2ProviderForm
        initial={initial}
        onSave={(data) => upsertMutation.mutate(data)}
        onCancel={() => navigate(ROUTES.CONFIG.SSO)}
        saving={upsertMutation.isPending}
        testResult={testResult}
        onTest={handleTest}
        testing={testing}
      />
    </div>
  );
}

export default Saml2ProviderFormPage;
