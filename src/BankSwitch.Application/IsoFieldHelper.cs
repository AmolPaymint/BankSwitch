using BankSwitch.Domain;

namespace BankSwitch.Application;

public static class IsoFieldHelper
{
    public static bool TryGetString(IsoMessage? message, int fieldNumber, out string value)
    {
        value = string.Empty;
        if (message is null) return false;
        if (!message.TryGetField(fieldNumber, out var raw)) return false;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        value = raw.Trim();
        return true;
    }

    public static bool TryGetDecimalAmount(IsoMessage message, int fieldNumber, out decimal amount)
    {
        amount = 0m;
        if (!TryGetString(message, fieldNumber, out var raw)) return false;
        if (!raw.All(char.IsDigit)) return false;
        if (!decimal.TryParse(raw, out var minorUnits)) return false;
        amount = minorUnits / 100m;
        return true;
    }
}
