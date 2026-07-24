using Microsoft.Extensions.DependencyInjection;
using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// Concrete <see cref="ICheckHost"/>: the narrow, allow-listed window a check uses to reach the rest of
/// Piro. Only types declared via a registered <see cref="CheckHostAllowedType"/> resolve; anything else
/// throws, so a check can never reach a repository, the DbContext, or Piro internals through the host. A
/// check gets its typed config directly and asks the host only for shared infrastructure (an
/// <see cref="IHttpClientFactory"/>, or a service its own integration registered such as a token provider).
/// </summary>
public sealed class CheckHost(IServiceProvider services, IEnumerable<CheckHostAllowedType> allowed) : ICheckHost
{
    private readonly HashSet<Type> _allowed = [.. allowed.Select(a => a.Type)];

    public T GetRequiredService<T>() where T : notnull
    {
        if (!_allowed.Contains(typeof(T)))
            throw new InvalidOperationException(
                $"A check requested {typeof(T).Name}, which is not on the check host allow-list. " +
                "A check may only resolve infrastructure explicitly declared with CheckHostAllowedType " +
                "(Piro core declares the shared ones; an integration declares what its own check needs).");

        return services.GetRequiredService<T>();
    }
}
