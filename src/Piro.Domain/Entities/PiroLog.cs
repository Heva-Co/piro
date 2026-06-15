namespace Piro.Domain.Entities;

/// <summary>Persisted application log entry written by Serilog.</summary>
public class PiroLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    /// <summary>Serilog structured properties as JSON.</summary>
    public string? Properties { get; set; }
    public string? SourceContext { get; set; }
}
