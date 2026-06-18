import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { servicesApi } from "@/lib/api";
import type { Service } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export function useServices() {
  return useQuery({
    queryKey: QUERY_KEYS.SERVICES,
    queryFn: servicesApi.list,
  });
}

export function useService(slug: string) {
  return useQuery({
    queryKey: QUERY_KEYS.SERVICE(slug),
    queryFn: () => servicesApi.get(slug),
    enabled: !!slug,
  });
}

export function useCreateService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Omit<Service, "slug" | "status">) => servicesApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICES });
    },
  });
}

export function useUpdateService(slug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Omit<Service, "slug" | "status">>) =>
      servicesApi.update(slug, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICES });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE(slug) });
    },
  });
}

export function useDeleteService(slug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => servicesApi.delete(slug),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICES });
    },
  });
}
