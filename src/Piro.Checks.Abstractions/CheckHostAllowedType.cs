namespace Piro.Checks.Abstractions;

/// <summary>
/// Declares one service type a check is allowed to resolve through the <see cref="ICheckHost"/>. Registered
/// in DI (one per allowed type) so the allow-list is composable: Piro core allows the shared infrastructure
/// its built-in checks need, and each integration that ships a check adds the service that check consumes —
/// without Piro core having to know that type. Anything not declared this way is refused by the host.
/// Lives in the abstractions assembly so an integration can declare its own allowed type without
/// referencing the concrete check SDK.
/// </summary>
public sealed record CheckHostAllowedType(Type Type)
{
    public static CheckHostAllowedType Of<T>() where T : notnull => new(typeof(T));
}
