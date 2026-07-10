import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { PageHeader } from "@/components/PageHeader";
import { SsoProviderForm } from "../components/SsoProviderForm";
import { oidcApi, type UpsertOidcProvider } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";
import { ROUTES } from "@/constants/routes";

const DEFAULT_SCOPES = "openid, profile, email";

const EMPTY_FORM: UpsertOidcProvider = {
  id: "",
  displayName: "",
  authority: "",
  clientId: "",
  clientSecret: "",
  redirectUri: "",
  scopes: DEFAULT_SCOPES,
  allowedDomains: "",
  defaultRole: "Viewer",
  isEnabled: true,
};

export default function SsoProviderFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id;
  const navigate = useNavigate();
  const qc = useQueryClient();

  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [testing, setTesting] = useState(false);

  const { data: providers = [], isLoading } = useQuery({
    queryKey: QUERY_KEYS.OIDC_CONFIGS,
    queryFn: oidcApi.list,
  });

  const provider = isEdit ? providers.find((p) => p.id === id) : undefined;

  const upsertMutation = useMutation({
    mutationFn: (data: UpsertOidcProvider) => oidcApi.upsert(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.OIDC_CONFIGS });
      navigate(ROUTES.CONFIG.SSO);
    },
  });

  async function handleTest(authority: string) {
    if (!authority) return;
    setTesting(true);
    setTestResult(null);
    try {
      const result = await oidcApi.test({ authority });
      setTestResult(result);
    } catch (err) {
      const message =
        axios.isAxiosError(err) && err.response?.data?.message
          ? err.response.data.message
          : "Connection test failed.";
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

  const initial: UpsertOidcProvider = provider
    ? {
        id: provider.id,
        displayName: provider.displayName,
        authority: provider.authority,
        clientId: provider.clientId,
        clientSecret: "",
        redirectUri: provider.redirectUri ?? "",
        scopes: provider.scopes,
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
          { label: isEdit ? "Edit Provider" : "Add Provider" },
        ]}
      />
      <SsoProviderForm
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
