import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { postmortemsApi } from "@/lib/actions/postmortems";
import type {
  CreatePostmortemRequest,
  UpdatePostmortemRequest,
  CreateTimelineEntryRequest,
  UpdateTimelineEntryRequest,
  CreateFieldDefinitionRequest,
  UpdateFieldDefinitionRequest,
} from "@/lib/actions/postmortems";
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

export function usePostmortemFieldDefinitions(includeInactive = false) {
  return useQuery({
    queryKey: [...QUERY_KEYS.POSTMORTEM_FIELD_DEFINITIONS, { includeInactive }],
    queryFn: () => postmortemsApi.fieldDefinitions(includeInactive),
  });
}

export function useCreateFieldDefinition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateFieldDefinitionRequest) => postmortemsApi.createFieldDefinition(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM_FIELD_DEFINITIONS });
    },
  });
}

export function useUpdateFieldDefinition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { defId: number; data: UpdateFieldDefinitionRequest }) =>
      postmortemsApi.updateFieldDefinition(args.defId, args.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM_FIELD_DEFINITIONS });
    },
  });
}

export function useReorderFieldDefinitions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderedIds: number[]) => postmortemsApi.reorderFieldDefinitions(orderedIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM_FIELD_DEFINITIONS });
    },
  });
}

export function useDeleteFieldDefinition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (defId: number) => postmortemsApi.deleteFieldDefinition(defId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM_FIELD_DEFINITIONS });
    },
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

export function useAddTimelineEntry(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateTimelineEntryRequest) => postmortemsApi.addTimelineEntry(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}

export function useUpdateTimelineEntry(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { entryId: number; data: UpdateTimelineEntryRequest }) =>
      postmortemsApi.updateTimelineEntry(id, args.entryId, args.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}

export function useDeleteTimelineEntry(id: number | string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (entryId: number) => postmortemsApi.deleteTimelineEntry(id, entryId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.POSTMORTEM(id) });
    },
  });
}
