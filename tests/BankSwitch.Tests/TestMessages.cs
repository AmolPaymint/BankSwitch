using BankSwitch.Domain;

namespace BankSwitch.Tests;

internal static class TestMessages
{
    public static IsoMessage ValidPurchase() => new IsoMessage("0200")
        .SetField(2, "5399838383838381")
        .SetField(3, "000000")
        .SetField(4, "000000001000")
        .SetField(7, "0528061300")
        .SetField(11, "123456")
        .SetField(14, "3001")
        .SetField(22, "051")
        .SetField(37, "123456789012")
        .SetField(41, "TERM0001")
        .SetField(49, "566")
        .SetField(123, "000000000000001");
}
