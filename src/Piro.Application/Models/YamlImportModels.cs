namespace Piro.Application.Models;

/// <summary>Root YAML configuration document.</summary>
public class PiroYamlConfig
{
    public List<ServiceYamlEntry>? Services { get; set; }
}

public class ServiceYamlEntry
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<CheckYamlEntry>? Checks { get; set; }
}

public class AlertYamlEntry
{
    public string AlertFor { get; set; } = "Status";
    public string AlertValue { get; set; } = "DOWN";
    public int FailureThreshold { get; set; } = 1;
    public int SuccessThreshold { get; set; } = 1;
    public string Severity { get; set; } = "Warning";
    public string? Description { get; set; }
}

public class CheckYamlEntry
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "HTTP";
    public string Cron { get; set; } = "* * * * *";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsMultiRegion { get; set; } = false;
    public int? FailureThreshold { get; set; }
    public int? RecoveryThreshold { get; set; }
    public int? HistoryDaysDesktop { get; set; }
    public int? HistoryDaysMobile { get; set; }
    /// <summary>Flexible type-specific check data (url, host, port, etc.).</summary>
    public Dictionary<object, object>? TypeData { get; set; }
    public List<AlertYamlEntry>? Alerts { get; set; }
}
