using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// A catalog of tag keys: one row per distinct key, not per <c>key:value</c> combination. The value of a
/// key is a property of the assignment (this service is <c>tier:critical</c>, that one is
/// <c>tier:standard</c>), so it lives on the join tables rather than here. The <see cref="Source"/> is a
/// property of the key (fixed by the namespace), so it lives here.
/// </summary>
public class Tag
{
    public int Id { get; set; }

    /// <summary>Normalized, validated key (see <c>TagConstants</c>). Unique across the catalog.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Fixed by the namespace: <c>piro:*</c> keys are <see cref="TagSource.System"/>, all others <see cref="TagSource.User"/>.</summary>
    public TagSource Source { get; set; }

    public ICollection<ServiceTag> ServiceTags { get; set; } = [];
    public ICollection<CheckTag> CheckTags { get; set; } = [];
    public ICollection<WorkerTag> WorkerTags { get; set; } = [];
}
