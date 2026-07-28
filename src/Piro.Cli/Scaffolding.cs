namespace Piro.Cli;

/// <summary>Templates written by <c>piro init</c>.</summary>
internal static class Scaffolding
{
    /// <summary>
    /// Meant to be committed, which is exactly why it holds no credential — the loader rejects an
    /// <c>api_key</c> field outright rather than ignoring it (RFC 0019 §4.6).
    /// </summary>
    public const string ConfigFile = """
        # Piro CLI configuration. Safe to commit: it never holds credentials.
        # Authenticate with `piro login`, or set PIRO_API_KEY for non-interactive use.

        current: production

        instances:
          production:
            url: https://status.example.com
            config: ./piro.yaml
            # Only needed when the admin panel is not served from the same origin as the API
            # (the `piro login` consent screen is a panel route):
            # admin_url: https://admin.example.com

          # staging:
          #   url: https://status.staging.example.com
          #   config: ./piro.yaml

        """;

    public const string ExampleDocument = """
        # yaml-language-server: $schema=./piro.schema.json   # from `piro schema -o piro.schema.json`
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
