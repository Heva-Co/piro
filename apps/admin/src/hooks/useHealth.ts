import { useQuery } from "@tanstack/react-query";
import { healthApi } from "@/lib/api";
import { QUERY_KEYS } from "@/constants/api";

export function useHealth() {
  return useQuery({
    queryKey: QUERY_KEYS.HEALTH,
    queryFn: healthApi.check,
    staleTime: 5 * 60_000,
    retry: false,
  });
}
