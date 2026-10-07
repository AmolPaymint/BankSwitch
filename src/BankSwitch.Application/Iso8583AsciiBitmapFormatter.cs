using System.Globalization;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class Iso8583AsciiBitmapFormatter
{
    private static readonly IReadOnlyDictionary<int, FieldSpec> Specs = new Dictionary<int, FieldSpec>
    {
        [2] = FieldSpec.LlVar(19),
        [3] = FieldSpec.Fixed(6),
        [4] = FieldSpec.Fixed(12),
        [7] = FieldSpec.Fixed(10),
        [11] = FieldSpec.Fixed(6),
        [12] = FieldSpec.Fixed(6),
        [13] = FieldSpec.Fixed(4),
        [14] = FieldSpec.Fixed(4),
        [18] = FieldSpec.Fixed(4),
        [22] = FieldSpec.Fixed(3),
        [25] = FieldSpec.Fixed(2),
        [28] = FieldSpec.Fixed(9),
        [32] = FieldSpec.LlVar(11),
        [35] = FieldSpec.LlVar(37),
        [37] = FieldSpec.Fixed(12),
        [38] = FieldSpec.Fixed(6),
        [39] = FieldSpec.Fixed(2),
        [41] = FieldSpec.Fixed(8),
        [42] = FieldSpec.Fixed(15),
        [43] = FieldSpec.Fixed(40),
        [49] = FieldSpec.Fixed(3),
        [52] = FieldSpec.Fixed(16),
        [55] = FieldSpec.LllVar(999),
        [62] = FieldSpec.LllVar(999),
        [63] = FieldSpec.LllVar(999),
        [64] = FieldSpec.Fixed(16),
        [90] = FieldSpec.Fixed(42),
        [102] = FieldSpec.LlVar(28),
        [103] = FieldSpec.LlVar(28),
        [123] = FieldSpec.LllVar(999)
    };

    public byte[] Format(IsoMessage message, bool omitMacField = false)
    {
        if (message.Mti.Length != 4 || !message.Mti.All(char.IsDigit))
        {
            throw new FormatException("MTI must be exactly four numeric characters.");
        }

        var fields = message.Fields
            .Where(x => x.Key is >= 2 and <= 128)
            .Where(x => !(omitMacField && x.Key == 64))
            .OrderBy(x => x.Key)
            .ToArray();

        var hasSecondary = fields.Any(x => x.Key > 64);
        var bits = new bool[129];
        bits[1] = hasSecondary;
        foreach (var field in fields) bits[field.Key] = true;

        var builder = new StringBuilder();
        builder.Append(message.Mti);
        builder.Append(ToBitmapHex(bits, 1, 64));
        if (hasSecondary) builder.Append(ToBitmapHex(bits, 65, 128));

        foreach (var field in fields)
        {
            if (!Specs.TryGetValue(field.Key, out var spec))
            {
                throw new FormatException($"No ISO 8583 field specification is configured for field {field.Key}.");
            }
            builder.Append(EncodeField(field.Key, field.Value, spec));
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    public IsoMessage Parse(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.ASCII.GetString(bytes);
        if (text.Length < 20) throw new FormatException("ISO 8583 message is too short for MTI and primary bitmap.");
        var offset = 0;
        var mti = text.Substring(offset, 4);
        offset += 4;
        var primary = ParseBitmap(text.Substring(offset, 16));
        offset += 16;
        var bits = new bool[129];
        for (var i = 1; i <= 64; i++) bits[i] = primary[i];
        if (bits[1])
        {
            if (text.Length < offset + 16) throw new FormatException("Secondary bitmap indicated but not present.");
            var secondary = ParseBitmap(text.Substring(offset, 16));
            offset += 16;
            for (var i = 65; i <= 128; i++) bits[i] = secondary[i - 64];
        }

        var message = new IsoMessage(mti);
        for (var field = 2; field <= 128; field++)
        {
            if (!bits[field]) continue;
            if (!Specs.TryGetValue(field, out var spec))
            {
                throw new FormatException($"No ISO 8583 field specification is configured for field {field}.");
            }
            var value = DecodeField(text, ref offset, field, spec);
            message.SetField(field, value);
        }

        if (offset != text.Length)
        {
            throw new FormatException($"ISO 8583 message has {text.Length - offset} trailing character(s).");
        }
        return message;
    }

    private static string EncodeField(int fieldNumber, string? value, FieldSpec spec)
    {
        value ??= string.Empty;
        if (spec.LengthHeaderDigits == 0)
        {
            if (value.Length != spec.MaxLength)
            {
                if (fieldNumber == 28 && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    var minor = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
                    return minor.ToString("000000000", CultureInfo.InvariantCulture);
                }
                throw new FormatException($"Field {fieldNumber} must be exactly {spec.MaxLength} characters but was {value.Length}.");
            }
            return value;
        }

        if (value.Length > spec.MaxLength) throw new FormatException($"Field {fieldNumber} exceeds maximum length {spec.MaxLength}.");
        return value.Length.ToString(new string('0', spec.LengthHeaderDigits), CultureInfo.InvariantCulture) + value;
    }

    private static string DecodeField(string text, ref int offset, int fieldNumber, FieldSpec spec)
    {
        var length = spec.MaxLength;
        if (spec.LengthHeaderDigits > 0)
        {
            EnsureAvailable(text, offset, spec.LengthHeaderDigits, fieldNumber);
            var lenText = text.Substring(offset, spec.LengthHeaderDigits);
            if (!int.TryParse(lenText, NumberStyles.None, CultureInfo.InvariantCulture, out length))
            {
                throw new FormatException($"Field {fieldNumber} has invalid variable length header.");
            }
            offset += spec.LengthHeaderDigits;
            if (length > spec.MaxLength) throw new FormatException($"Field {fieldNumber} length {length} exceeds max {spec.MaxLength}.");
        }

        EnsureAvailable(text, offset, length, fieldNumber);
        var value = text.Substring(offset, length);
        offset += length;
        return value;
    }

    private static void EnsureAvailable(string text, int offset, int length, int fieldNumber)
    {
        if (offset + length > text.Length) throw new FormatException($"Field {fieldNumber} is truncated.");
    }

    private static string ToBitmapHex(bool[] bits, int start, int end)
    {
        var bytes = new byte[8];
        for (var field = start; field <= end; field++)
        {
            if (!bits[field]) continue;
            var index = field - start;
            bytes[index / 8] |= (byte)(0x80 >> (index % 8));
        }
        return Convert.ToHexString(bytes);
    }

    private static bool[] ParseBitmap(string hex)
    {
        if (hex.Length != 16) throw new FormatException("Bitmap must be sixteen hexadecimal characters.");
        var bytes = Convert.FromHexString(hex);
        var bits = new bool[65];
        for (var i = 0; i < 64; i++)
        {
            bits[i + 1] = (bytes[i / 8] & (0x80 >> (i % 8))) != 0;
        }
        return bits;
    }

    private sealed record FieldSpec(int MaxLength, int LengthHeaderDigits)
    {
        public static FieldSpec Fixed(int length) => new(length, 0);
        public static FieldSpec LlVar(int maxLength) => new(maxLength, 2);
        public static FieldSpec LllVar(int maxLength) => new(maxLength, 3);
    }
}