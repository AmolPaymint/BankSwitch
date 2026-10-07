using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public static class CardholderDataProtector
{
    private static readonly Regex PanLikePattern = new(@"\b\d{12,19}\b", RegexOptions.Compiled);

    public static string MaskPan(string? pan)
    {
        if (string.IsNullOrWhiteSpace(pan) || pan.Length < 10) return "INVALID_PAN";
        return pan.Substring(0, 6) + new string('*', pan.Length - 10) + pan.Substring(pan.Length - 4);
    }

    public static string MaskAccount(string? account)
    {
        if (string.IsNullOrWhiteSpace(account) || account.Length < 6) return "MASKED";
        return new string('*', Math.Max(0, account.Length - 4)) + account.Substring(account.Length - 4);
    }

    public static string RedactLogText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var redacted = PanLikePattern.Replace(text, match => MaskPan(match.Value));
        foreach (var secretWord in new[] { "password", "pinblock", "cvv", "cvc", "track2", "track1", "session", "token", "key" })
        {
            redacted = Regex.Replace(redacted, $@"(?i){secretWord}\s*=\s*[^\s,;]+", $"{secretWord}=<redacted>");
        }
        return redacted;
    }

    public static string HashForLookup(string value, string keyedHashSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keyedHashSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public static class IsoMessageSanitizer
{
    public static IReadOnlyDictionary<int, string> ToSafeFields(IsoMessage message)
    {
        var safe = new SortedDictionary<int, string>();
        foreach (var field in message.Fields)
        {
            safe[field.Key] = field.Key switch
            {
                2 => CardholderDataProtector.MaskPan(field.Value),
                14 => "<expiry-redacted>",
                35 or 45 => "<track-data-redacted>",
                52 => "<pin-block-redacted>",
                55 => "<emv-redacted>",
                102 or 103 => CardholderDataProtector.MaskAccount(field.Value),
                _ => CardholderDataProtector.RedactLogText(field.Value)
            };
        }
        return safe;
    }
}
