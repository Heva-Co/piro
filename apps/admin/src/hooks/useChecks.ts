import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { checksApi, alertConfigsApi } from "@/lib/api";
import type { Check, CreateCheck, AlertConfig } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export function useAllChecks() {
  return useQuery({
    queryKey: QUERY_KEYS.CHECKS,
    queryFn: () => checksApi.listAll(),
    refetchInterval: 60_000,
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
    mutationFn: (data: CreateCheck) =>
      checksApi.create(serviceSlug, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE_CHECKS(serviceSlug) });
    },
  });
}

export function useUpdateCheck(serviceSlug: string, checkSlug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Omit<Check, "id" | "slug" | "currentStatus">>) =>
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
    mutationFn: (data: Omit<AlertConfig, "id">) =>
      alertConfigsApi.create(serviceSlug, checkSlug, data),
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
