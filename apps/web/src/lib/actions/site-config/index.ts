import { get } from "@/src/lib/http";
import type { components } from "@/src/lib/api-types";

export type SiteConfig = components["schemas"]["SiteConfigResponse"];

export const siteConfigApi = {
  get: () => get<SiteConfig>("/site/config"),
};
