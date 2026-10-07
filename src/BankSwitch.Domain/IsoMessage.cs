using System.Collections.Concurrent;

namespace BankSwitch.Domain;

public sealed class IsoMessage
{
    private readonly ConcurrentDictionary<int, string> _fields = new();

    public IsoMessage(string mti)
    {
        if (string.IsNullOrWhiteSpace(mti)) throw new ArgumentException("MTI is required.", nameof(mti));
        Mti = mti.Trim();
    }

    public string Mti { get; private set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public IReadOnlyDictionary<int, string> Fields => _fields;

    public bool TryGetField(int fieldNumber, out string value) => _fields.TryGetValue(fieldNumber, out value!);

    public string GetRequiredField(int fieldNumber)
    {
        if (!_fields.TryGetValue(fieldNumber, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"ISO field {fieldNumber} is required.");
        }
        return value;
    }

    public IsoMessage SetField(int fieldNumber, string? value)
    {
        if (fieldNumber < 1 || fieldNumber > 128) throw new ArgumentOutOfRangeException(nameof(fieldNumber));
        if (value is null)
        {
            _fields.TryRemove(fieldNumber, out _);
            return this;
        }
        _fields[fieldNumber] = value;
        return this;
    }

    public IsoMessage RemoveField(int fieldNumber)
    {
        _fields.TryRemove(fieldNumber, out _);
        return this;
    }

    public IsoMessage CloneResponse(string responseMti, string responseCode)
    {
        var response = new IsoMessage(responseMti)
        {
            CorrelationId = CorrelationId
        };
        foreach (var field in _fields)
        {
            if (!SensitiveIsoFieldPolicy.MustNeverEcho(field.Key))
            {
                response.SetField(field.Key, field.Value);
            }
        }
        response.SetField(39, responseCode);
        return response;
    }
}

public static class SensitiveIsoFieldPolicy
{
    public static bool MustNeverEcho(int fieldNumber) => fieldNumber is 35 or 45 or 52 or 64;

    public static bool IsSensitive(int fieldNumber) => fieldNumber is 2 or 14 or 35 or 45 or 52 or 55 or 64 or 102 or 103;
}
