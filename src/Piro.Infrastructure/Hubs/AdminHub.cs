using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Piro.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time admin UI updates.
/// Requires a valid JWT — same auth as the REST API.
/// </summary>
[Authorize]
public class AdminHub : Hub<IAdminClient>;
