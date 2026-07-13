import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type UserProfile = components["schemas"]["UserProfileDto"];
export type UpdateProfileRequest = components["schemas"]["UpdateProfileRequest"];
export type ChangePasswordRequest = components["schemas"]["ChangePasswordRequest"];

export const profileApi = {
  get: () => api.get<UserProfile>(ENDPOINTS.AUTH_ME).then((r) => r.data),

  update: (data: Partial<UpdateProfileRequest>) =>
    api.put<UserProfile>(ENDPOINTS.AUTH_ME, data).then((r) => r.data),

  changePassword: (data: ChangePasswordRequest) => api.put(ENDPOINTS.AUTH_ME_PASSWORD, data),

  markShowcaseSeen: () => api.put(ENDPOINTS.AUTH_ME_SHOWCASE_SEEN),
};
