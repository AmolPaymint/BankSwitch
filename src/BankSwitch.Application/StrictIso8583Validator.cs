using System.Text.RegularExpressions;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class StrictIso8583Validator
{
    private static readonly Regex Numeric = new("^[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex AlphaNumeric = new("^[A-Za-z0-9]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedMtis = new(StringComparer.Ordinal)
    {
        "0100", "0200", "0220", "0400", "0420", "0421"
    };

    private static readonly Dictionary<string, int[]> RequiredFieldsByMti = new(StringComparer.Ordinal)
    {
        ["0100"] = new[] { 2, 3, 4, 7, 11, 14, 22, 37, 41, 49, 123 },
        ["0200"] = new[] { 2, 3, 4, 7, 11, 14, 22, 37, 41, 49, 123 },
        ["0220"] = new[] { 2, 3, 4, 7, 11, 14, 22, 37, 41, 49, 123 },
        ["0400"] = new[] { 2, 3, 4, 7, 11, 37, 41, 49, 90, 123 },
        ["0420"] = new[] { 2, 3, 4, 7, 11, 37, 41, 49, 90, 123 },
        ["0421"] = new[] { 2, 3, 4, 7, 11, 37, 41, 49, 90, 123 }
    };

    private readonly ITransactionRepository _transactions;
    private readonly IClock _clock;

    public StrictIso8583Validator(ITransactionRepository transactions, IClock clock)
    {
        _transactions = transactions;
        _clock = clock;
    }

    public async Task<IsoValidationResult> ValidateAsync(IsoMessage message, SourceNode sourceNode, CancellationToken cancellationToken = default)
    {
        if (!SupportedMtis.Contains(message.Mti))
        {
            return IsoValidationResult.Fail("12", $"Unsupported MTI {message.Mti}.");
        }

        if (sourceNode.PermittedMtis.Count > 0 && !sourceNode.PermittedMtis.Contains(message.Mti))
        {
            return IsoValidationResult.Fail("57", $"MTI {message.Mti} is not permitted for source node {sourceNode.NodeId}.");
        }

        foreach (var field in RequiredFieldsByMti[message.Mti])
        {
            if (!IsoFieldHelper.TryGetString(message, field, out _))
            {
                return IsoValidationResult.Fail("30", $"Required field {field} is missing.");
            }
        }

        var panResult = ValidatePan(message);
        if (!panResult.IsValid) return panResult;

        var processingCodeResult = ValidateProcessingCode(message);
        if (!processingCodeResult.IsValid) return processingCodeResult;

        var amountResult = ValidateAmount(message);
        if (!amountResult.IsValid) return amountResult;

        var stanResult = ValidateStan(message);
        if (!stanResult.IsValid) return stanResult;

        var rrnResult = ValidateRrn(message);
        if (!rrnResult.IsValid) return rrnResult;

        var expiryResult = ValidateExpiry(message);
        if (!expiryResult.IsValid) return expiryResult;

        var currencyResult = ValidateCurrency(message);
        if (!currencyResult.IsValid) return currencyResult;

        var channelResult = ValidateChannel(message, sourceNode);
        if (!channelResult.IsValid) return channelResult;

        var duplicateResult = await ValidateDuplicateAsync(message, sourceNode, cancellationToken).ConfigureAwait(false);
        if (!duplicateResult.IsValid) return duplicateResult;

        return IsoValidationResult.Ok();
    }

    private static IsoValidationResult ValidatePan(IsoMessage message)
    {
        if (!IsoFieldHelper.TryGetString(message, 2, out var pan)) return IsoValidationResult.Fail("30", "PAN is missing.");
        if (pan.Length is < 12 or > 19 || !Numeric.IsMatch(pan)) return IsoValidationResult.Fail("14", "PAN length or type is invalid.");
        return LuhnValidator.IsValid(pan) ? IsoValidationResult.Ok() : IsoValidationResult.Fail("14", "PAN failed Luhn validation.");
    }

    private static IsoValidationResult ValidateProcessingCode(IsoMessage message)
    {
        if (!IsoFieldHelper.TryGetString(message, 3, out var value)) return IsoValidationResult.Fail("30", "Processing code is missing.");
        if (value.Length != 6 || !Numeric.IsMatch(value)) return IsoValidationResult.Fail("30", "Processing code must be six numeric digits.");
        var transactionCode = value[..2];
        return transactionCode switch
        {
            "00" or "01" or "20" or "31" or "40" or "50" => IsoValidationResult.Ok(),
            _ => IsoValidationResult.Fail("58", $"Processing transaction code {transactionCode} is not allowed.")
        };
    }

    private static IsoValidationResult ValidateAmount(IsoMessage message)
    {
        if (!IsoFieldHelper.TryGetString(message, 4, out var amountText)) return IsoValidationResult.Fail("13", "Amount is missing.");
        if (amountText.Length != 12 || !Numeric.IsMatch(amountText)) return IsoValidationResult.Fail("13", "Amount must be a twelve-digit numeric minor-unit value.");
        return decimal.Parse(amountText) >= 0 ? IsoValidationResult.Ok() : IsoValidationResult.Fail("13", "Amount is invalid.");
    }

    private static IsoValidationResult ValidateStan(IsoMessage message)
    {
        if (!IsoFieldHelper.TryGetString(message, 11, out var stan)) return IsoValidationResult.Fail("30", "STAN is missing.");
        return stan.Length == 6 && Numeric.IsMatch(stan) ? IsoValidationResult.Ok() : IsoValidationResult.Fail("30", "STAN must be six numeric digits.");
    }

    private static IsoValidationResult ValidateRrn(IsoMessage message)
    {
        if (!IsoFieldHelper.TryGetString(message, 37, out var rrn)) return IsoValidationResult.Fail("30", "RRN is missing.");
        return rrn.Length == 12 && AlphaNumeric.IsMatch(rrn) ? IsoValidationResult.Ok() : IsoValidationResult.Fail("30", "RRN must be twelve alphanumeric characters.");
    }

    private static IsoValidationResult ValidateExpiry(IsoMessage message)
    {
        if (message.Mti is "0400" or "0420" or "0421") return IsoValidationResult.Ok();
        if (!IsoFieldHelper.TryGetString(message, 14, out var expiry)) return IsoValidationResult.Fail("30", "Expiry is missing.");
        if (expiry.Length != 4 || !Numeric.IsMatch(expiry)) return IsoValidationResult.Fail("30", "Expiry must be YYMM.");
        var month = int.Parse(expiry[2..4]);
        if (month is < 1 or > 12) return IsoValidationResult.Fail("30", "Expiry month is invalid.");
        return IsoValidationResult.Ok();
    }

    private static IsoValidationResult ValidateCurrency(IsoMessage message)
    {
        if (!IsoFieldHelper.TryGetString(message, 49, out var currency)) return IsoValidationResult.Fail("30", "Currency code is missing.");
        return currency.Length == 3 && Numeric.IsMatch(currency) ? IsoValidationResult.Ok() : IsoValidationResult.Fail("30", "Currency code must be ISO 4217 numeric format.");
    }

    private static IsoValidationResult ValidateChannel(IsoMessage message, SourceNode sourceNode)
    {
        if (!IsoFieldHelper.TryGetString(message, 123, out var field123)) return IsoValidationResult.Fail("30", "Channel metadata is missing.");
        if (field123.Length < 15) return IsoValidationResult.Fail("30", "Field 123 is too short for channel extraction.");
        var channel = field123.Substring(13, 2);
        if (!channel.All(char.IsAsciiLetterOrDigit))
            return IsoValidationResult.Fail("30", "Field 123 channel code contains invalid characters.");
        if (sourceNode.PermittedChannels.Count > 0 && !sourceNode.PermittedChannels.Contains(channel))
        {
            return IsoValidationResult.Fail("58", $"Channel {channel} is not permitted for source node {sourceNode.NodeId}.");
        }
        return IsoValidationResult.Ok();
    }

    private async Task<IsoValidationResult> ValidateDuplicateAsync(IsoMessage message, SourceNode sourceNode, CancellationToken cancellationToken)
    {
        // ---------------------------------------------------------------
        // DUPLICATE DETECTION — TWO-LAYER STRATEGY (A4: CONSOLIDATED)
        //
        // Layer 1 — In-process replay cache (IsoTcpGatewayHostedService):
        //   IReplayCache.TryAccept(key, ttl) is a sliding-window in-memory bloom
        //   filter hit before messages even reach this validator. It handles the
        //   common case of network retransmits arriving within seconds.
        //   Fast: O(1), no DB round-trip, but process-local (not cluster-safe).
        //
        // Layer 2 — SQL persistent check (this method):
        //   ITransactionRepository.ExistsDuplicateAsync queries dbo.TransactionLog
        //   on (SourceNodeId, STAN, RRN, Amount, BusinessDate). This is the
        //   authoritative, cluster-safe check that catches duplicates across nodes,
        //   process restarts, and long-window retries.
        //
        // The legacy BankSwitch.Logic.TransactionLogManager.ExistsDuplicate()
        // (NHibernate) has been superseded by Layer 2 above. See MIGRATION_COMPLETE.md.
        //
        // Reversal messages (0400/0420/0421) are exempt — a reversal for the same
        // STAN/RRN as the original authorization is intentional, not a duplicate.
        // ---------------------------------------------------------------
        if (message.Mti is "0400" or "0420" or "0421") return IsoValidationResult.Ok();
        if (!IsoFieldHelper.TryGetString(message, 11, out var stan)) return IsoValidationResult.Fail("30", "STAN is missing.");
        if (!IsoFieldHelper.TryGetString(message, 37, out var rrn)) return IsoValidationResult.Fail("30", "RRN is missing.");
        if (!IsoFieldHelper.TryGetDecimalAmount(message, 4, out var amount)) return IsoValidationResult.Fail("13", "Amount is invalid.");
        var exists = await _transactions.ExistsDuplicateAsync(sourceNode.NodeId, stan, rrn, amount, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken).ConfigureAwait(false);
        return exists ? IsoValidationResult.Fail("94", "Duplicate STAN/RRN/amount/source transaction detected.") : IsoValidationResult.Ok();
    }
}

public sealed record IsoValidationResult(bool IsValid, string ResponseCode, string Reason)
{
    public static IsoValidationResult Ok() => new(true, "00", "OK");
    public static IsoValidationResult Fail(string responseCode, string reason) => new(false, responseCode, reason);
}

public static class LuhnValidator
{
    public static bool IsValid(string number)
    {
        var sum = 0;
        var alternate = false;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(number[i])) return false;
            var n = number[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return number.Length > 0 && sum % 10 == 0;
    }

    /// <summary>Computes the Luhn check digit that should be appended to <paramref name="numberWithoutCheckDigit"/>.</summary>
    public static char ComputeCheckDigit(string numberWithoutCheckDigit)
    {
        var sum = 0;
        var alternate = true;
        for (var i = numberWithoutCheckDigit.Length - 1; i >= 0; i--)
        {
            var n = numberWithoutCheckDigit[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        var checkDigit = (10 - sum % 10) % 10;
        return (char)('0' + checkDigit);
    }
}