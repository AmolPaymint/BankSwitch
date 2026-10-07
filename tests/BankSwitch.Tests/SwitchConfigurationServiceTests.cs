using BankSwitch.Application;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class SwitchConfigurationServiceTests
{
    private static (ISwitchConfigurationService Service, InMemorySwitchStore Store) CreateService()
    {
        var store = new InMemorySwitchStore();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        return (new SwitchConfigurationService(store, audit), store);
    }

    [Fact]
    public async Task Seeded_development_data_is_visible_through_service()
    {
        var (service, _) = CreateService();

        var sources = await service.GetSourceNodesAsync();
        Assert.Contains(sources, x => x.NodeId == "SRC-DEV-001");

        var routes = await service.GetRoutesAsync();
        Assert.Contains(routes, x => x.BinPrefix == "539983");

        var schemes = await service.GetSchemesAsync();
        Assert.Single(schemes);
        Assert.Equal(2, schemes[0].Permissions.Count);
    }

    [Fact]
    public async Task Creating_source_node_with_duplicate_node_id_fails()
    {
        var (service, _) = CreateService();
        var input = new SourceNodeInput { NodeId = "SRC-DEV-001", Name = "Duplicate" };

        var result = await service.SaveSourceNodeAsync(null, input, "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("94", result.ResponseCode);
    }

    [Fact]
    public async Task Creating_source_node_with_new_node_id_succeeds_and_is_listed()
    {
        var (service, _) = CreateService();
        var input = new SourceNodeInput
        {
            NodeId = "SRC-DEV-002",
            Name = "Second source",
            IsActive = true,
            TpsLimit = 25,
            MaxMessageBytes = 4096,
            IdleTimeoutSeconds = 30,
            PermittedMtis = "0200; 0420",
            PermittedChannels = "01",
            AllowedBinRanges = "400000"
        };

        var result = await service.SaveSourceNodeAsync(null, input, "tester");

        Assert.True(result.IsSuccess);
        Assert.Equal("SRC-DEV-002", result.Value!.NodeId);
        Assert.Contains("0200", result.Value.PermittedMtis);
        Assert.Contains((await service.GetSourceNodesAsync()), x => x.NodeId == "SRC-DEV-002");
    }

    [Fact]
    public async Task Route_with_invalid_bin_prefix_format_fails()
    {
        var (service, _) = CreateService();
        var sink = (await service.GetSinkNodesAsync()).Single();

        var result = await service.SaveRouteAsync(null, new RouteInput("12AB", sink.Id, true), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("30", result.ResponseCode);
    }

    [Fact]
    public async Task Active_route_with_already_routed_bin_prefix_fails()
    {
        var (service, _) = CreateService();
        var sink = (await service.GetSinkNodesAsync()).Single();

        // 539983 is already an active route from seed data.
        var result = await service.SaveRouteAsync(null, new RouteInput("539983", sink.Id, true), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("94", result.ResponseCode);
    }

    [Fact]
    public async Task New_route_to_unknown_sink_fails()
    {
        var (service, _) = CreateService();

        var result = await service.SaveRouteAsync(null, new RouteInput("411111", Guid.NewGuid(), true), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    [Fact]
    public async Task New_route_with_unique_bin_prefix_succeeds()
    {
        var (service, _) = CreateService();
        var sink = (await service.GetSinkNodesAsync()).Single();

        var result = await service.SaveRouteAsync(null, new RouteInput("411111", sink.Id, true), "tester");

        Assert.True(result.IsSuccess);
        Assert.Contains((await service.GetRoutesAsync()), x => x.BinPrefix == "411111");
    }

    [Fact]
    public async Task Fee_with_minimum_greater_than_maximum_fails()
    {
        var (service, _) = CreateService();

        var result = await service.SaveFeeAsync(null, new FeeInput("Bad fee", 0m, 5m, 20m, 10m, true), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("30", result.ResponseCode);
    }

    [Fact]
    public async Task Fee_can_be_created_and_updated()
    {
        var (service, _) = CreateService();

        var created = await service.SaveFeeAsync(null, new FeeInput("Interchange fee", 0m, 1.5m, 1m, 50m, true), "tester");
        Assert.True(created.IsSuccess);

        var updated = await service.SaveFeeAsync(created.Value!.Id, new FeeInput("Interchange fee v2", 0m, 2m, 1m, 50m, true), "tester");
        Assert.True(updated.IsSuccess);
        Assert.Equal("Interchange fee v2", updated.Value!.Name);
        Assert.Equal(2m, updated.Value.PercentageOfTransaction);
    }

    [Fact]
    public async Task Scheme_with_unknown_fee_in_permission_fails()
    {
        var (service, _) = CreateService();
        var source = (await service.GetSourceNodesAsync()).Single();
        var route = (await service.GetRoutesAsync()).Single();

        var permissions = new[] { new SchemePermissionInput("00", "01", Guid.NewGuid()) };
        var result = await service.SaveSchemeAsync(null, new SchemeInput("New scheme", source.Id, route.Id, true, permissions), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("58", result.ResponseCode);
    }

    [Fact]
    public async Task Scheme_can_be_created_with_valid_permissions_and_retrieved()
    {
        var (service, _) = CreateService();
        var source = (await service.GetSourceNodesAsync()).Single();
        var route = (await service.GetRoutesAsync()).Single();
        var fee = (await service.GetFeesAsync()).Single();

        var permissions = new[] { new SchemePermissionInput("01", "02", fee.Id) };
        var result = await service.SaveSchemeAsync(null, new SchemeInput("Second scheme", source.Id, route.Id, true, permissions), "tester");

        Assert.True(result.IsSuccess);
        var reloaded = await service.GetSchemeAsync(result.Value!.Id);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Permissions);
        Assert.Equal("01", reloaded.Permissions.First().TransactionTypeCode);
    }

    [Fact]
    public async Task Scheme_with_duplicate_permission_rows_fails()
    {
        var (service, _) = CreateService();
        var source = (await service.GetSourceNodesAsync()).Single();
        var route = (await service.GetRoutesAsync()).Single();
        var fee = (await service.GetFeesAsync()).Single();

        var permissions = new[]
        {
            new SchemePermissionInput("01", "02", fee.Id),
            new SchemePermissionInput("01", "02", fee.Id)
        };
        var result = await service.SaveSchemeAsync(null, new SchemeInput("Dup scheme", source.Id, route.Id, true, permissions), "tester");

        Assert.False(result.IsSuccess);
        Assert.Equal("94", result.ResponseCode);
    }
}
