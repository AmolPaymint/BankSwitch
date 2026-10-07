using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Xunit;

namespace BankSwitch.Tests;

public sealed class IsoFormatterTests
{
    [Fact]
    public void Formatter_round_trips_purchase_message()
    {
        var formatter = new Iso8583AsciiBitmapFormatter();
        var original = TestMessages.ValidPurchase();
        var bytes = formatter.Format(original);
        var parsed = formatter.Parse(bytes);
        Assert.Equal(original.Mti, parsed.Mti);
        Assert.Equal("5399838383838381", parsed.GetRequiredField(2));
        Assert.Equal("123456", parsed.GetRequiredField(11));
        Assert.Equal("123456789012", parsed.GetRequiredField(37));
        Assert.Equal("000000000000001", parsed.GetRequiredField(123));
    }

    [Fact]
    public void Response_clone_never_echoes_pin_track_or_mac()
    {
        var response = new IsoMessage("0200")
            .SetField(2, "5399838383838381")
            .SetField(35, "trackdata")
            .SetField(52, "1234567890123456")
            .SetField(64, "1234567890ABCDEF")
            .CloneResponse("0210", "00");
        Assert.False(response.TryGetField(35, out _));
        Assert.False(response.TryGetField(52, out _));
        Assert.False(response.TryGetField(64, out _));
        Assert.Equal("00", response.GetRequiredField(39));
    }
}
