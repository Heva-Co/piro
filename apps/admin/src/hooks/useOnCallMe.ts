import { useQuery } from "@tanstack/react-query";
import { onCallApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export function useMyOnCallSlots(from: string, to: string) {
  return useQuery({
    queryKey: QUERY_KEYS.ONCALL_MY_SLOTS(from, to),
    queryFn: () => onCallApi.getMySlots(from, to),
  });
}

export function useMyOnCallCurrentStatus() {
  return useQuery({
    queryKey: QUERY_KEYS.ONCALL_MY_CURRENT,
    queryFn: () => onCallApi.getMyCurrentStatus(),
    refetchInterval: 60_000,
  });
}
