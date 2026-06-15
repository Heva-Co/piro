import { json } from "@sveltejs/kit";
import { adminApi } from "$lib/api";
import type { RequestHandler } from "./$types";

export const POST: RequestHandler = async ({ request, locals }) => {
  const token = locals.accessToken;
  if (!token) return json({ error: "Unauthorized" }, { status: 401 });

  const { action, data } = await request.json();

  try {
    switch (action) {
      // ── Services ────────────────────────────────────────────────────────
      case "getServices": {
        const services = await adminApi.getServices(token);
        return json(services);
      }
      case "createService": {
        const service = await adminApi.createService(token, data);
        return json(service);
      }
      case "updateService": {
        const { slug, ...rest } = data;
        const service = await adminApi.updateService(token, slug, rest);
        return json(service);
      }
      case "deleteService": {
        await adminApi.deleteService(token, data.slug);
        return json({ success: true });
      }

      // ── Checks ──────────────────────────────────────────────────────────
      case "getAllChecks": {
        const checks = await adminApi.getAllChecks(token);
        return json(checks);
      }
      case "getChecks": {
        const checks = await adminApi.getChecks(token, data.serviceSlug);
        return json(checks);
      }
      case "getCheck": {
        const check = await adminApi.getCheck(token, data.serviceSlug, data.checkSlug);
        return json(check);
      }
      case "createCheck": {
        const { serviceSlug, ...rest } = data;
        const check = await adminApi.createCheck(token, serviceSlug, rest);
        return json(check);
      }
      case "updateCheck": {
        const { serviceSlug, checkSlug, ...rest } = data;
        const check = await adminApi.updateCheck(token, serviceSlug, checkSlug, rest);
        return json(check);
      }
      case "deleteCheck": {
        await adminApi.deleteCheck(token, data.serviceSlug, data.checkSlug);
        return json({ success: true });
      }
      case "runCheck": {
        await adminApi.runCheck(token, data.serviceSlug, data.checkSlug);
        return json({ success: true });
      }
      case "getCheckLogs": {
        const logs = await adminApi.getCheckLogs(token, data.serviceSlug, data.checkSlug, data.limit ?? 20);
        return json(logs);
      }

      // ── Incidents ────────────────────────────────────────────────────────
      case "getIncidents": {
        const incidents = await adminApi.getIncidents(token, data?.includeResolved ?? false);
        return json({ incidents, total: incidents.length });
      }
      case "getIncident": {
        const incident = await adminApi.getIncident(token, data.id);
        return json(incident);
      }
      case "createIncident": {
        const incident = await adminApi.createIncident(token, data);
        return json(incident);
      }
      case "updateIncident": {
        const { id, ...rest } = data;
        const incident = await adminApi.updateIncident(token, id, rest);
        return json(incident);
      }
      case "addComment": {
        const { id, ...rest } = data;
        await adminApi.addComment(token, id, rest);
        return json({ success: true });
      }
      case "updateComment": {
        const { id, commentId, ...rest } = data;
        await adminApi.updateComment(token, id, commentId, rest);
        return json({ success: true });
      }
      case "deleteComment": {
        await adminApi.deleteComment(token, data.id, data.commentId);
        return json({ success: true });
      }
      case "addIncidentService": {
        const { id, ...rest } = data;
        const incident = await adminApi.addIncidentService(token, id, rest);
        return json(incident);
      }
      case "removeIncidentService": {
        const incident = await adminApi.removeIncidentService(token, data.id, data.serviceSlug);
        return json(incident);
      }
      case "deleteIncident": {
        await adminApi.deleteIncident(token, data.id);
        return json({ success: true });
      }

      // ── Maintenances ─────────────────────────────────────────────────────
      case "getMaintenances": {
        const maintenances = await adminApi.getMaintenances(token);
        return json({ maintenances, total: maintenances.length });
      }
      case "createMaintenance": {
        const maintenance = await adminApi.createMaintenance(token, data);
        return json(maintenance);
      }
      case "getMaintenance": {
        const maintenances = await adminApi.getMaintenances(token);
        const found = maintenances.find((m) => m.id === data.id);
        if (!found) return json({ error: "Not found" }, { status: 404 });
        return json(found);
      }
      case "updateMaintenance": {
        const { id, ...rest } = data;
        const maintenance = await adminApi.updateMaintenance(token, id, rest);
        return json(maintenance);
      }
      case "cancelMaintenance": {
        await adminApi.cancelMaintenance(token, data.id);
        return json({ success: true });
      }
      case "deleteMaintenance": {
        await adminApi.deleteMaintenance(token, data.id);
        return json({ success: true });
      }

      // ── Triggers ─────────────────────────────────────────────────────────
      case "getTriggers": {
        const triggers = await adminApi.getTriggers(token);
        return json(triggers);
      }
      case "getTrigger": {
        const trigger = await adminApi.getTrigger(token, data.id);
        return json(trigger);
      }
      case "createTrigger": {
        const trigger = await adminApi.createTrigger(token, data);
        return json(trigger);
      }
      case "updateTrigger": {
        const { id, ...rest } = data;
        const trigger = await adminApi.updateTrigger(token, id, rest);
        return json(trigger);
      }
      case "deleteTrigger": {
        await adminApi.deleteTrigger(token, data.id);
        return json({ success: true });
      }
      case "testTrigger": {
        const result = await adminApi.testTrigger(token, { type: data.type, metaJson: data.metaJson, name: data.name });
        return json(result);
      }

      // ── Alert Configs ─────────────────────────────────────────────────────
      case "getAlertConfigs": {
        const configs = await adminApi.getAlertConfigs(token, data.serviceSlug, data.checkSlug);
        return json(configs);
      }
      case "createAlertConfig": {
        const { serviceSlug, checkSlug, ...rest } = data;
        const config = await adminApi.createAlertConfig(token, serviceSlug, checkSlug, rest);
        return json(config);
      }
      case "updateAlertConfig": {
        const { serviceSlug, checkSlug, id, ...rest } = data;
        const config = await adminApi.updateAlertConfig(token, serviceSlug, checkSlug, id, rest);
        return json(config);
      }
      case "deleteAlertConfig": {
        await adminApi.deleteAlertConfig(token, data.serviceSlug, data.checkSlug, data.id);
        return json({ success: true });
      }

      // ── User ─────────────────────────────────────────────────────────────
      case "updateUser": {
        // name update - TODO: add endpoint when needed
        return json({ success: true });
      }
      case "updatePassword": {
        // password change - TODO: add endpoint when needed
        return json({ success: true });
      }

      // ── API Keys ──────────────────────────────────────────────────────────
      case "getApiKeys": {
        const keys = await adminApi.getApiKeys(token);
        return json(keys);
      }
      case "createApiKey": {
        const key = await adminApi.createApiKey(token, data.name);
        return json(key);
      }
      case "revokeApiKey": {
        await adminApi.revokeApiKey(token, data.id);
        return json({ success: true });
      }

      // ── Users ────────────────────────────────────────────────────────────
      case "getUsers": {
        const users = await adminApi.getUsers(token);
        return json(users);
      }
      case "getRoles": {
        const roles = await adminApi.getRoles(token);
        return json(roles);
      }
      case "inviteUser": {
        await adminApi.inviteUser(token, data.email, data.roleId);
        return json({ success: true });
      }
      case "changeUserRole": {
        await adminApi.changeUserRole(token, data.userId, data.roleId);
        return json({ success: true });
      }
      case "deleteUser": {
        await adminApi.deleteUser(token, data.userId);
        return json({ success: true });
      }

      // ── OIDC / SSO config ─────────────────────────────────────────────────
      case "getOidcConfigs": {
        const configs = await adminApi.getOidcConfigs(token);
        return json(configs);
      }
      case "upsertOidcConfig": {
        await adminApi.upsertOidcConfig(token, data);
        return json({ success: true });
      }
      case "testOidcProvider": {
        const result = await adminApi.testOidcProvider(token, data.providerId);
        return json(result);
      }
      case "getSsoMode": {
        const result = await adminApi.getSsoMode(token);
        return json(result);
      }
      case "setSsoMode": {
        await adminApi.setSsoMode(token, data.ssoOnly);
        return json({ success: true });
      }

      // ── Site config ───────────────────────────────────────────────────────────
      case "getSiteConfig": {
        const result = await adminApi.getSiteConfig(token);
        return json(result);
      }
      case "updateSiteConfig": {
        await adminApi.updateSiteConfig(token, data);
        return json({ success: true });
      }
      case "deleteSiteAsset": {
        await adminApi.deleteSiteAsset(token, data.type);
        return json({ success: true });
      }

      // ── Workers ───────────────────────────────────────────────────────────
      case "getWorkers": {
        const workers = await adminApi.getWorkers(token);
        return json(workers);
      }
      case "createWorker": {
        const worker = await adminApi.createWorker(token, data);
        return json(worker);
      }
      case "deleteWorker": {
        await adminApi.deleteWorker(token, data.id);
        return json({ success: true });
      }

      // ── Config import ────────────────────────────────────────────────────
      case "importConfigPlan": {
        const result = await adminApi.importConfig(token, data.yaml, false);
        return json(result);
      }
      case "importConfigApply": {
        const result = await adminApi.importConfig(token, data.yaml, true);
        return json(result);
      }
      case "getLogs": {
        const result = await adminApi.getLogs(token, data);
        return json(result);
      }
      case "getToken": {
        return json({ token });
      }

      default:
        return json({ error: `Unknown action: ${action}` }, { status: 400 });
    }
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : "Request failed";
    return json({ error: msg }, { status: 500 });
  }
};
