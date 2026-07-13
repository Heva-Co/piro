import { createContext, useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { profileApi } from "@/lib/actions/profile";
import { QUERY_KEYS } from "@/constants/api";
import { useAuth } from "@/hooks/useAuth";

interface TimezoneContextValue {
  /** The timezone actually used to format dates throughout the app. */
  activeTimeZone: string;
  /** The timezone stored on the user's profile. */
  profileTimeZone: string | undefined;
  /** The timezone reported by the browser/OS. */
  browserTimeZone: string;
  /** True when the browser timezone differs from the profile timezone. */
  mismatch: boolean;
  /** True when the user has opted to view dates in the browser timezone instead of their profile's. */
  useBrowserTimeZone: boolean;
  setUseBrowserTimeZone: (value: boolean) => void;
}

const TimezoneContext = createContext<TimezoneContextValue | null>(null);

function detectBrowserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  } catch {
    return "UTC";
  }
}

export function TimezoneProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [useBrowserTimeZone, setUseBrowserTimeZone] = useState(false);

  const { data: profile } = useQuery({
    queryKey: QUERY_KEYS.MY_PROFILE,
    queryFn: profileApi.get,
    enabled: isAuthenticated,
    staleTime: 60_000,
  });

  const browserTimeZone = useMemo(detectBrowserTimeZone, []);
  const profileTimeZone = profile?.timeZone;
  const mismatch = !!profileTimeZone && profileTimeZone !== browserTimeZone;

  const activeTimeZone =
    useBrowserTimeZone || !profileTimeZone ? browserTimeZone : profileTimeZone;

  const handleSetUseBrowserTimeZone = useCallback((value: boolean) => {
    setUseBrowserTimeZone(value);
  }, []);

  return (
    <TimezoneContext.Provider
      value={{
        activeTimeZone,
        profileTimeZone,
        browserTimeZone,
        mismatch,
        useBrowserTimeZone,
        setUseBrowserTimeZone: handleSetUseBrowserTimeZone,
      }}
    >
      {children}
    </TimezoneContext.Provider>
  );
}

export { TimezoneContext };
