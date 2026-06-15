namespace Piro.Infrastructure.Hubs;

/// <summary>Methods the server can invoke on connected admin UI clients.</summary>
public interface IAdminClient
{
    /// <summary>Notifies the admin UI that the worker list has changed (connect / disconnect / heartbeat).</summary>
    Task WorkersChanged();
}
