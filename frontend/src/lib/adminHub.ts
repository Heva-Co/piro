import { HubConnectionBuilder, HubConnectionState, type HubConnection } from "@microsoft/signalr";
import { PIRO_API } from "./api.js";

let connection: HubConnection | null = null;

export async function getAdminHub(tokenFactory: () => Promise<string>): Promise<HubConnection> {
  if (connection && connection.state === HubConnectionState.Connected) {
    return connection;
  }

  connection = new HubConnectionBuilder()
    .withUrl(`${PIRO_API}/hub/admin`, { accessTokenFactory: tokenFactory })
    .withAutomaticReconnect()
    .build();

  await connection.start();
  return connection;
}

export function stopAdminHub() {
  connection?.stop();
  connection = null;
}
