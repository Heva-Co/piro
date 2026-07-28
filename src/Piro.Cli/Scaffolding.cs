namespace Piro.Cli;

/// <summary>Templates written by <c>piro init</c>.</summary>
internal static class Scaffolding
{
    /// <summary>
    /// Meant to be committed, which is exactly why it holds no credential — the loader rejects an
    /// <c>api_key</c> field outright rather than ignoring it (RFC 0019 §4.6).
    /// </summary>
    public const string ConfigFile = """
        # Piro CLI configuration. Safe to commit — it never holds credentials.
        # The API key comes from the PIRO_API_KEY environment variable.

        current: production

        instances:
          production:
            url: https://status.example.com
            config: ./piro.yaml

          # staging:
          #   url: https://status.staging.example.com
          #   config: ./piro.yaml

        """;

    public const string ExampleDocument = """
        # yaml-language-server: $schema=https://raw.githubusercontent.com/Heva-Co/piro/main/schema/piro.schema.json
        version: 1

        # Everything this file does not declare, it does not touch: fields set in the admin panel
        # survive an apply, and resources absent from this file are left alone unless you pass --prune.

        services:
          - slug: example-api
            name: Example API
            description: Public API
            checks:
              - slug: health
                name: Health endpoint
                type: HTTP
                cron: "*/5 * * * *"
                type_data:
                  url: https://api.example.com/health
                  expectedStatusCodes: ["2xx"]
                  timeout: 5000
                alert_configs:
                  - dimension: Status
                    alert_value: DOWN
                    failure_threshold: 2
                    severity: Critical

        """;
}
