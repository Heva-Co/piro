import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type SetupStatus = components["schemas"]["SetupStatusResponse"];
export type SetupEmailConfig = components["schemas"]["SetupEmailConfigPayload"];
export type SendSetupEmailCodeRequest = components["schemas"]["SendSetupEmailCodeRequest"];
export type ConfirmSetupEmailCodeRequest = components["schemas"]["ConfirmSetupEmailCodeRequest"];
export type CompleteSetupRequest = components["schemas"]["CompleteSetupRequest"];

export const setupApi = {
  status: () => api.get<SetupStatus>(ENDPOINTS.SETUP.STATUS).then((r) => r.data),

  sendEmailTestCode: (data: SendSetupEmailCodeRequest) => api.post(ENDPOINTS.SETUP.EMAIL_TEST, data),

  confirmEmailTestCode: (data: ConfirmSetupEmailCodeRequest) =>
    api.post(ENDPOINTS.SETUP.EMAIL_CONFIRM, data),

  complete: (data: CompleteSetupRequest) => api.post(ENDPOINTS.SETUP.COMPLETE, data),
};
