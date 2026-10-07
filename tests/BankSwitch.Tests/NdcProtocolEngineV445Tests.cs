using BankSwitch.Application;
using Xunit;

namespace BankSwitch.Tests;

public sealed class NdcProtocolEngineV445Tests
{
    private static (NdcProtocolCodec Codec, INdcProtocolEngine Engine) Create()
    {
        var mac = new HmacNdcMacProvider();
        var codec = new NdcProtocolCodec(mac);
        var engine = new NdcProtocolEngine(new InMemoryNdcProtocolRepository(), codec, new SystemClock());
        return (codec, engine);
    }

    [Theory]
    [InlineData(AtmProtocol.Ndc)]
    [InlineData(AtmProtocol.NdcPlus)]
    public void Codec_RoundTrips_Framed_Transaction_Request(AtmProtocol protocol)
    {
        var (codec, _) = Create();
        var profile = protocol == AtmProtocol.Ndc ? NdcWireProfile.NdcDefault : NdcWireProfile.NdcPlusDefault;
        var input = new NdcWireMessage(NdcMessageClass.TransactionRequest, "ATM00001", 7,
            new Dictionary<string, string> { ["TXN"]="CASH_WITHDRAWAL", ["STAN"]="123456", ["AMOUNT"]="1000.00", ["CCY"]="356" },
            null, "corr-1", DateTimeOffset.UtcNow);

        var frame = codec.Encode(input, profile);
        var decoded = codec.Decode(frame, profile, "corr-1");

        Assert.True(decoded.IsSuccess);
        Assert.True(decoded.LrcValid);
        Assert.Equal(NdcMessageClass.TransactionRequest, decoded.WireMessage!.MessageClass);
        Assert.Equal("ATM00001", decoded.WireMessage.Luno);
        Assert.Equal(7, decoded.WireMessage.SequenceNumber);
        Assert.Equal("123456", decoded.WireMessage.Fields["STAN"]);
    }

    [Fact]
    public void Codec_Rejects_Corrupt_Lrc()
    {
        var (codec, _) = Create();
        var frame = codec.Encode(new NdcWireMessage(NdcMessageClass.SolicitedStatus, "ATM1", 1,
            new Dictionary<string, string>{{"CMD","POLL"}}, null, "c", DateTimeOffset.UtcNow), NdcWireProfile.NdcDefault);
        frame[^1] ^= 0x7F;
        var decoded = codec.Decode(frame, NdcWireProfile.NdcDefault, "c");
        Assert.False(decoded.IsSuccess);
        Assert.Equal("NDC004", decoded.Code);
    }

    [Fact]
    public async Task Engine_Tracks_Session_Device_Status_And_Ej()
    {
        var (codec, engine) = Create();
        var profile = NdcWireProfile.NdcDefault;

        var status = codec.Encode(new NdcWireMessage(NdcMessageClass.UnsolicitedStatus, "ATM9", 1,
            new Dictionary<string, string>{{"DEVICE","DISPENSER"},{"STATE","Fault"},{"CODE","D01"},{"DETAIL","Cassette fault"}}, null, "c1", DateTimeOffset.UtcNow), profile);
        Assert.True((await engine.ProcessInboundAsync("ATM9", AtmProtocol.Ndc, status, "c1")).IsSuccess);

        var ej = codec.Encode(new NdcWireMessage(NdcMessageClass.ElectronicJournal, "ATM9", 2,
            new Dictionary<string, string>{{"EVENT","CASH_DISPENSED"},{"RRN","123"},{"STAN","456"},{"AMOUNT","500.00"},{"CCY","356"}}, null, "c2", DateTimeOffset.UtcNow), profile);
        Assert.True((await engine.ProcessInboundAsync("ATM9", AtmProtocol.Ndc, ej, "c2")).IsSuccess);

        var session = await engine.GetSessionAsync("ATM9");
        Assert.NotNull(session);
        Assert.Equal(NdcSessionState.InService, session!.State);
        Assert.Equal(3, session.NextSequenceNumber);
        Assert.Single(await engine.GetDeviceStatusAsync("ATM9", 10));
        Assert.Single(await engine.GetEjAsync("ATM9", 10));
    }

    [Fact]
    public async Task Engine_Builds_MultiBlock_Download_And_Advances_Sequence()
    {
        var (_, engine) = Create();
        var payload = Convert.ToBase64String(Enumerable.Repeat((byte)0x41, 3000).ToArray());
        var result = await engine.BuildDownloadAsync(new NdcDownloadRequest("ATM2", "STATE_SCREEN_FIT", "2026.09", payload, 512, "dl1"), AtmProtocol.NdcPlus);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.Count >= 5);
        var session = await engine.GetSessionAsync("ATM2");
        Assert.Equal(NdcSessionState.InService, session!.State);
        Assert.NotNull(session.LastDownloadAt);
    }

    [Fact]
    public async Task Simulator_CashWithdrawal_Pack_RoundTrips()
    {
        var (_, engine) = Create();
        var result = await engine.RunSimulatorScenarioAsync(new NdcSimulatorScenarioRequest("ATM-SIM", AtmProtocol.NdcPlus, "cash-withdrawal", 2500m, "356", "sim1"));
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Passed);
        Assert.True(result.Value.FramesBase64.Count >= 4);
    }
}
