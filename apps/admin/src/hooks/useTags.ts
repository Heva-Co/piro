import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { tagsApi } from "@/lib/actions/tags";
import type { ReplaceTagsRequest } from "@/lib/actions/tags";
import { QUERY_KEYS } from "@/constants/api";

export function useServiceTags(id: number | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.SERVICE_TAGS(id ?? 0),
    queryFn: () => tagsApi.getServiceTags(id!),
    enabled: id != null,
  });
}

export function useReplaceServiceTags(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceTagsRequest) => tagsApi.replaceServiceTags(id, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE_TAGS(id) }),
  });
}

export function useToggleServiceSystemTag(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ key, assigned }: { key: string; assigned: boolean }) =>
      assigned ? tagsApi.assignServiceSystemTag(id, key) : tagsApi.unassignServiceSystemTag(id, key),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: QUERY_KEYS.SERVICE_TAGS(id) }),
  });
}

export function useToggleCheckSystemTag(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ key, assigned }: { key: string; assigned: boolean }) =>
      assigned ? tagsApi.assignCheckSystemTag(id, key) : tagsApi.unassignCheckSystemTag(id, key),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: QUERY_KEYS.CHECK_TAGS(id) }),
  });
}

export function useCheckTags(id: number | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.CHECK_TAGS(id ?? 0),
    queryFn: () => tagsApi.getCheckTags(id!),
    enabled: id != null,
  });
}

export function useReplaceCheckTags(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceTagsRequest) => tagsApi.replaceCheckTags(id, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: QUERY_KEYS.CHECK_TAGS(id) }),
  });
}

export function useWorkerTags(id: string | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.WORKER_TAGS(id ?? ""),
    queryFn: () => tagsApi.getWorkerTags(id!),
    enabled: !!id,
  });
}

export function useReplaceWorkerTags(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceTagsRequest) => tagsApi.replaceWorkerTags(id, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: QUERY_KEYS.WORKER_TAGS(id) }),
  });
}

export function useRequiredWorkerTags(id: number | undefined) {
  return useQuery({
    queryKey: QUERY_KEYS.CHECK_REQUIRED_WORKER_TAGS(id ?? 0),
    queryFn: () => tagsApi.getRequiredWorkerTags(id!),
    enabled: id != null,
  });
}

export function useReplaceRequiredWorkerTags(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceTagsRequest) => tagsApi.replaceRequiredWorkerTags(id, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: QUERY_KEYS.CHECK_REQUIRED_WORKER_TAGS(id) }),
  });
}
