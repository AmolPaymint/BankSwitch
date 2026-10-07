using BankSwitch.Admin.Hubs;
using BankSwitch.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BankSwitch.Tests;

public sealed class CommandCenterRealtimeTests
{
    [Fact]
    public void CommandCenterHub_RequiresViewerPolicy()
    {
        var attributes = typeof(CommandCenterHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToArray();

        Assert.Contains(attributes, a => a.Policy == "Viewer");
    }

    [Fact]
    public void RealtimeBroadcaster_IsHostedBackgroundService()
    {
        Assert.True(typeof(BackgroundService).IsAssignableFrom(typeof(CommandCenterRealtimeBroadcaster)));
    }
}
