using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>A status update posted on an <see cref="Incident"/> during its lifecycle.</summary>
public class IncidentComment
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public string Comment { get; set; } = string.Empty;

    /// <summary>Unix timestamp (seconds) when this update was posted.</summary>
    public long CommentedAt { get; set; }

    public IncidentStatus Status { get; set; }

    /// <summary>Controls visibility on the public status page. Can only be Public if the parent incident is also Public.</summary>
    public CommentVisibility Visibility { get; set; } = CommentVisibility.Private;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Incident Incident { get; set; } = null!;
}
