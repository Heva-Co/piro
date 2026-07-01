export type CheckConfig = Record<string, unknown>;

export interface CheckConfigProps {
  config: CheckConfig;
  onChange: (config: CheckConfig) => void;
}

export interface GcpCheckConfigProps extends CheckConfigProps {
  integrations: { id: number; name: string; type: string }[];
}
