import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { alertsApi } from "@/lib/actions/alerts";
import { checksApi, checkTypesApi } from "@/lib/actions/checks";
import type { CreateCheckRequest, UpdateCheckRequest } from "@/lib/actions/checks";
import { alertConfigsApi } from "@/lib/actions/alert-configs";
import type { CreateAlertConfigRequest, UpdateAlertConfigRequest } from "@/lib/actions/alert-configs";
import { QUERY_KEYS } from "@/constants/api";

export function useAllChecks() {
  return useQuery({
    queryKey: QUERY_KEYS.CHECKS,
    queryFn: () => checksApi.listAll(),
    refetchInterval: 60_000,
  });
}

// Loads the backend's check-type metadata (RFC 0011 — no hardcoded table).
// The query is deduped by react-query, so multiple callers share one fetch.
export function useCheckTypes() {
  return useQuery({
    queryKey: QUERY_KEYS.CHECK_TYPES,
    queryFn: checkTypesApi.list,
  });
}

// Resolves a raw check-type discriminator (e.g. "GCP_CloudRunJob") to its human display name.
export function useCheckTypeLabel() {
  const { data: checkTypes = [] } = useCheckTypes();
  return (type: string) => checkTypes.find((t) => t.type === type)?.displayName ?? type;
}

// Resolves the full metadata (config schema, dimensions, required integration, …) for one
// check type. Returns undefined until the manifest loads or when `type` is not yet known.
export function useCheckTypeMeta(type: string | undefined) {
  const { data: checkTypes = [] } = useCheckTypes();
  return type ? checkTypes.find((t) => t.type === type) : undefined;
}

export function useAllAlerts(params?: { page?: number; pageSize?: number; from?: string; to?: string; activeOnly?: boolean }) {
  return useQuery({
    queryKey: [...QUERY_KEYS.ALERTS, params],
    queryFn: () => alertsApi.list(params),
    refetchInterval: 60_000,
  });
}

export function useAlert(id: number | string | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.ALERT(id ?? ""),
    queryFn: () => alertsApi.get(id!),
    enabled: id != null,
  });
}

export function useChecks(serviceSlug: string) {
  return useQuery({
    queryKey: QUERY_KEYS.SERVICE_CHECKS(serviceSlug),
    queryFn: () => checksApi.listForService(serviceSlug),
    enabled: !!serviceSlug,
  });
}

export function useCheck(serviceSlug: string, checkSlug: string) {
  return useQuery({
    queryKey: QUERY_KEYS.SERVICE_CHECK(serviceSlug, checkSlug),
    queryFn: () => checksApi.get(serviceSlug, checkSlug),
    enabled: !!serviceSlug && !!checkSlug,
  });
}

export function useCreateCheck(serviceSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateCheckRequest) => checksApi.create(serviceSlug, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE_CHECKS(serviceSlug) });
    },
  });
}

export function useUpdateCheck(serviceSlug: string, checkSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<UpdateCheckRequest>) =>
      checksApi.update(serviceSlug, checkSlug, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE_CHECKS(serviceSlug) });
      queryClient.invalidateQueries({
        queryKey: QUERY_KEYS.SERVICE_CHECK(serviceSlug, checkSlug),
      });
    },
  });
}

export function useDeleteCheck(serviceSlug: string, checkSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => checksApi.delete(serviceSlug, checkSlug),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE_CHECKS(serviceSlug) });
    },
  });
}

export function useRunCheck(serviceSlug: string, checkSlug: string) {
  return useMutation({
    mutationFn: () => checksApi.run(serviceSlug, checkSlug),
  });
}

export function useCheckLogs(serviceSlug: string, checkSlug: string) {
  return useQuery({
    queryKey: QUERY_KEYS.CHECK_LOGS(serviceSlug, checkSlug),
    queryFn: () => checksApi.logs(serviceSlug, checkSlug),
    enabled: !!serviceSlug && !!checkSlug,
  });
}

export function useCheckHistory(serviceSlug: string, checkSlug: string, days = 14) {
  return useQuery({
    queryKey: [...QUERY_KEYS.CHECK_LOGS(serviceSlug, checkSlug), "history", days],
    queryFn: () => checksApi.history(serviceSlug, checkSlug, days),
    enabled: !!serviceSlug && !!checkSlug,
  });
}

export function useAlertConfigs(serviceSlug: string, checkSlug: string) {
  return useQuery({
    queryKey: QUERY_KEYS.ALERT_CONFIGS(serviceSlug, checkSlug),
    queryFn: () => alertConfigsApi.list(serviceSlug, checkSlug),
    enabled: !!serviceSlug && !!checkSlug,
  });
}

export function useCreateAlertConfig(serviceSlug: string, checkSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateAlertConfigRequest) =>
      alertConfigsApi.create(serviceSlug, checkSlug, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: QUERY_KEYS.ALERT_CONFIGS(serviceSlug, checkSlug),
      });
    },
  });
}

export function useUpdateAlertConfig(serviceSlug: string, checkSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: number | string; data: Partial<UpdateAlertConfigRequest> }) =>
      alertConfigsApi.update(serviceSlug, checkSlug, id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: QUERY_KEYS.ALERT_CONFIGS(serviceSlug, checkSlug),
      });
    },
  });
}

export function useDeleteAlertConfig(serviceSlug: string, checkSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number | string) => alertConfigsApi.delete(serviceSlug, checkSlug, id),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: QUERY_KEYS.ALERT_CONFIGS(serviceSlug, checkSlug),
      });
    },
  });
}
