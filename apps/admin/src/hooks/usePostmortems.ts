import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { postmortemsApi } from "@/lib/actions/postmortems";
import type { CreatePostmortemRequest, UpdatePostmortemRequest } from "@/lib/actions/postmortems";
import { QUERY_KEYS } from "@/constants/api";

export function usePostmortems() {
  return useQuery({
    queryKey: QUERY_KEYS.POSTMORTEMS,
    queryFn: () => postmortemsApi.list(),
  });
}

export function usePostmortem(id: number | string) {
  return useQuery({
    queryKey: QUERY_KEYS.POSTMORTEM(id),
    queryFn: () => postmortemsApi.get(id),
    enabled: !!id,
  });
}

export function usePostmortemFieldDefinitions() {
  return useQuery({
    queryKey: QUERY_KEYS.POSTMORTEM_FIELD_DEFINITIONS,
    queryFn: () => postmortemsApi.fieldDefinitions(),
  });
}

export function useCreatePostmortem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePostmortemRequest) => postmortemsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEMS });
    },
  });
}

export function useUpdatePostmortem(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdatePostmortemRequest) => postmortemsApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEMS });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}

export function usePublishPostmortem(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (publish: boolean) =>
      publish ? postmortemsApi.publish(id) : postmortemsApi.unpublish(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEMS });
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}

export function useDeletePostmortem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number | string) => postmortemsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEMS });
    },
  });
}

export function useLinkIncident(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (incidentId: number) => postmortemsApi.linkIncident(id, incidentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}

export function useUnlinkIncident(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (incidentId: number) => postmortemsApi.unlinkIncident(id, incidentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}
