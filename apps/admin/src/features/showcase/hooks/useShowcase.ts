import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { profileApi } from "@/lib/actions/profile";
import { QUERY_KEYS } from "@/constants/api";

/** Whether the post-setup feature showcase should be shown, and a mutation to dismiss it for good. */
export function useShowcase() {
  const qc = useQueryClient();

  const { data: profile } = useQuery({
    queryKey: QUERY_KEYS.MY_PROFILE,
    queryFn: profileApi.get,
  });

  const dismiss = useMutation({
    mutationFn: profileApi.markShowcaseSeen,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: QUERY_KEYS.MY_PROFILE });
    },
  });

  return {
    shouldShow: !!profile && !profile.hasSeenShowcase,
    dismiss: () => dismiss.mutate(),
  };
}
