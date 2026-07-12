import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { servicesApi } from "@/lib/api";
import type { Service } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export function useServices(params?: { page?: number; pageSize?: number; search?: string }) {
  return useQuery({
    queryKey: [...QUERY_KEYS.SERVICES, params],
    queryFn: () => servicesApi.list(params),
  });
}

// TODO(#149): dropdowns/pickers below request a large page instead of true "all services" —
// switch to infinite scroll or a dedicated search-as-you-type picker once services can exceed ~1000.
export function useAllServices() {
  return useQuery({
    queryKey: [...QUERY_KEYS.SERVICES, "all"],
    queryFn: () => servicesApi.list({ pageSize: 1000 }),
    select: (data) => data.items,
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
    mutationFn: (data: Omit<Service, "currentStatus">) => servicesApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICES });
    },
  });
}

export function useUpdateService(slug: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Omit<Service, "slug" | "currentStatus">>) =>
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
