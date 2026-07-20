import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type ForgotPasswordRequest = components["schemas"]["ForgotPasswordRequest"];
export type ResetPasswordRequest = components["schemas"]["ResetPasswordRequest"];

export const authApi = {
  forgotPassword: (data: ForgotPasswordRequest) =>
    api.post(ENDPOINTS.AUTH.FORGOT_PASSWORD, data),

  resetPassword: (data: ResetPasswordRequest) =>
    api.post(ENDPOINTS.AUTH.RESET_PASSWORD, data),
};
