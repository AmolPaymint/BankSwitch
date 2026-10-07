using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BankSwitch.Admin.Hubs;

/// <summary>
/// Authenticated SignalR hub used by the Enterprise Command Center.
/// The hub only exposes server-to-client operational events; privileged data
/// is segmented into role groups during connection establishment.
/// </summary>
[Authorize(Policy = "Viewer")]
public sealed class CommandCenterHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            Context.Abort();
            return;
        }

        if (Context.User.IsInRole("Operations") ||
            Context.User.IsInRole("CardOperations") ||
            Context.User.IsInRole("AgencyManager") ||
            Context.User.IsInRole("CorporateManager") ||
            Context.User.IsInRole("FinanceOfficer") ||
            Context.User.IsInRole("ReconciliationOfficer") ||
            Context.User.IsInRole("SuperAdmin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "operations").ConfigureAwait(false);
        }

        if (Context.User.IsInRole("RiskAnalyst") ||
            Context.User.IsInRole("RiskManager") ||
            Context.User.IsInRole("SuperAdmin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "risk").ConfigureAwait(false);
        }

        if (Context.User.IsInRole("SecurityAdmin") || Context.User.IsInRole("SuperAdmin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "security").ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public Task Ping() => Clients.Caller.SendAsync("heartbeat", new
    {
        serverTime = DateTimeOffset.UtcNow,
        connectionId = Context.ConnectionId
    });
}
