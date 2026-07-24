using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.GoogleCloud;

public sealed class GoogleCloudConfig
{
    [Required, SecretField, MultilineField, SupportsFileUpload]
    [ConfigField("Service Account JSON",
        Placeholder = "Paste the contents of your Google Cloud service account key file (.json)",
        HelpText = "The key must have the necessary IAM permissions (e.g. run.executions.list for Cloud Run Jobs)."
    )]
    public string ServiceAccountJson { get; set; } = string.Empty;
}
