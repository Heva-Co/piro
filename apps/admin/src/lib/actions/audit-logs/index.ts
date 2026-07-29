import api from "@/lib/axios";
import { ENDPOINTS } from "@/constants/api";
import type { components } from "@/lib/api-types";

export type AuditTransaction = components["schemas"]["AuditTransactionDto"];
export type AuditEntry = components["schemas"]["AuditEntryDto"];
export type AuditLogPage = components["schemas"]["AuditLogPageDto"];
export type AuditAction = components["schemas"]["AuditAction"];

export interface AuditLogListParams {
  entityType?: string;
  userId?: string;
  action?: AuditAction;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export const auditLogsApi = {
  /**
   * A page holds whole transactions, so `totalCount` counts user actions rather than
   * individual entity changes.
   */
  list: (params?: AuditLogListParams) =>
    api.get<AuditLogPage>(ENDPOINTS.AUDIT_LOGS, { params }).then((r) => r.data),
};
