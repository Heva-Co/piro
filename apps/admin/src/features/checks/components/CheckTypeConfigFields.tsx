import { HttpConfig, DnsConfig, TcpConfig, PingConfig, SslConfig, GcpCloudRunJobConfig } from "@/features/checks/components";
import type { GcpIntegration } from "@/features/checks/components/types";
import type { CheckConfigFormValues } from "@/features/checks/validations";

interface Props {
  type: CheckConfigFormValues["type"];
  integrations: GcpIntegration[];
}

const CHECK_TYPE_CONFIG_RENDERERS: Record<CheckConfigFormValues["type"], (integrations: GcpIntegration[]) => React.ReactNode> = {
  HTTP: () => <HttpConfig />,
  DNS: () => <DnsConfig />,
  TCP: () => <TcpConfig />,
  Ping: () => <PingConfig />,
  SSL: () => <SslConfig />,
  // Heartbeat: () => <HeartbeatConfig />,
  GCP_CloudRunJob: (integrations) => <GcpCloudRunJobConfig integrations={integrations} />,
  // GRPC: () => <HttpConfig />,
};

/** Renders the config fields for the given CheckType — a single lookup instead of a per-type `if`/`===` chain repeated on every page that needs it. */
export function CheckTypeConfigFields(props: Props) {
  const { type, integrations } = props;
  
  const render = CHECK_TYPE_CONFIG_RENDERERS[type];
  return render ? render(integrations) : <HttpConfig />;
}
