using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class HsmClient : IHsmClient
{
    private readonly IConfiguration _configuration;
    private readonly ISecretProvider _secrets;
    private readonly Iso8583AsciiBitmapFormatter _formatter;
    private readonly ILogger<HsmClient> _logger;
    private readonly HttpClient _httpClient = new();

    public HsmClient(IConfiguration configuration, ISecretProvider secrets, Iso8583AsciiBitmapFormatter formatter, ILogger<HsmClient> logger)
    {
        _configuration = configuration;
        _secrets = secrets;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task<MacValidationResult> ValidateMacAsync(IsoMessage message, SourceNode sourceNode, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        {
            return MacValidationResult.NotApplicable;
        }

        if (!message.TryGetField(64, out var mac) || string.IsNullOrWhiteSpace(mac))
        {
            return MacValidationResult.Failed("ISO field 64 MAC is required.");
        }

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            var expected = await ComputeSoftwareMacAsync(message, sourceNode.KeyProfile, cancellationToken).ConfigureAwait(false);
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(mac))
                ? MacValidationResult.Passed
                : MacValidationResult.Failed("MAC mismatch.");
        }

        var response = await PostHsmAsync<ValidateMacResponse>("validate-mac", new HsmMacRequest(sourceNode.KeyProfile, message.Mti, message.Fields, mac), cancellationToken).ConfigureAwait(false);
        return response.IsValid ? MacValidationResult.Passed : MacValidationResult.Failed(response.Reason ?? "HSM rejected MAC.");
    }

    public async Task<string> GenerateMacAsync(IsoMessage message, SinkNode sinkNode, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            return await ComputeSoftwareMacAsync(message, sinkNode.KeyProfile, cancellationToken).ConfigureAwait(false);
        }

        var response = await PostHsmAsync<GenerateMacResponse>("generate-mac", new HsmMacRequest(sinkNode.KeyProfile, message.Mti, message.Fields, null), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.Mac)) throw new InvalidOperationException("HSM returned an empty MAC.");
        return response.Mac;
    }


    public async Task<string> GenerateMacForSourceAsync(IsoMessage message, SourceNode sourceNode, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            return await ComputeSoftwareMacAsync(message, sourceNode.KeyProfile, cancellationToken).ConfigureAwait(false);
        }

        var response = await PostHsmAsync<GenerateMacResponse>("generate-mac", new HsmMacRequest(sourceNode.KeyProfile, message.Mti, message.Fields, null), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.Mac)) throw new InvalidOperationException("HSM returned an empty source MAC.");
        return response.Mac;
    }

    private async Task<string> ComputeSoftwareMacAsync(IsoMessage message, string keyProfile, CancellationToken cancellationToken)
    {
        var secretName = string.IsNullOrWhiteSpace(keyProfile) ? "SoftwareMacKey" : $"SoftwareMacKey:{keyProfile}";
        string secret;
        try
        {
            secret = await _secrets.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            secret = await _secrets.GetSecretAsync("SoftwareMacKey", cancellationToken).ConfigureAwait(false);
        }

        var payload = _formatter.Format(message, omitMacField: true);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(payload))[..16];
    }

    private async Task<T> PostHsmAsync<T>(string operation, object payload, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["Hsm:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new InvalidOperationException("Hsm:BaseUrl is required when Hsm:Mode is Http.");
        var uri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), operation);
        using var response = await _httpClient.PostAsJsonAsync(uri, payload, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("HSM operation {Operation} failed with status {Status}: {Body}", operation, response.StatusCode, CardholderDataProtector.RedactLogText(body));
            throw new InvalidOperationException($"HSM operation {operation} failed with HTTP {(int)response.StatusCode}.");
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"HSM operation {operation} returned an empty response.");
    }

    public async Task<string> DeriveTerminalKeyCheckValueAsync(string keyProfile, string keySerialNumber, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        {
            return "000000";
        }

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            return await ComputeSoftwareKeyCheckValueAsync(keyProfile, keySerialNumber, cancellationToken).ConfigureAwait(false);
        }

        var response = await PostHsmAsync<DeriveTerminalKeyResponse>("derive-terminal-key", new HsmTerminalKeyRequest(keyProfile, keySerialNumber), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.KeyCheckValue)) throw new InvalidOperationException("HSM returned an empty key check value.");
        return response.KeyCheckValue;
    }

    private async Task<string> ComputeSoftwareKeyCheckValueAsync(string keyProfile, string keySerialNumber, CancellationToken cancellationToken)
    {
        var secretName = string.IsNullOrWhiteSpace(keyProfile) ? "SoftwareTerminalBaseKey" : $"SoftwareTerminalBaseKey:{keyProfile}";
        string secret;
        try
        {
            secret = await _secrets.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            secret = await _secrets.GetSecretAsync("SoftwareTerminalBaseKey", cancellationToken).ConfigureAwait(false);
        }

        // Simulated DUKPT-style derivation for development/test only: the derived session key
        // is HMAC(baseDerivationKey, KSN); the KCV is the first 6 hex characters of HMAC(derivedKey, "KCV").
        using var bdkHmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var derivedKey = bdkHmac.ComputeHash(Encoding.ASCII.GetBytes(keySerialNumber));
        using var kcvHmac = new HMACSHA256(derivedKey);
        var kcv = kcvHmac.ComputeHash(Encoding.ASCII.GetBytes("KCV"));
        return Convert.ToHexString(kcv)[..6];
    }

    private sealed record HsmMacRequest(
        [property: JsonPropertyName("keyProfile")] string KeyProfile,
        [property: JsonPropertyName("mti")] string Mti,
        [property: JsonPropertyName("fields")] IReadOnlyDictionary<int, string> Fields,
        [property: JsonPropertyName("mac")] string? Mac);

    private sealed record ValidateMacResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("reason")] string? Reason);

    private sealed record GenerateMacResponse([property: JsonPropertyName("mac")] string Mac);

    private sealed record HsmTerminalKeyRequest(
        [property: JsonPropertyName("keyProfile")] string KeyProfile,
        [property: JsonPropertyName("keySerialNumber")] string KeySerialNumber);

    private sealed record DeriveTerminalKeyResponse([property: JsonPropertyName("keyCheckValue")] string KeyCheckValue);

    // ---------------------------------------------------------------
    // PIN block translation and verification
    // ---------------------------------------------------------------

    public async Task<string> TranslatePinBlockAsync(string encryptedPinBlock, string sourceKeyProfile, string destinationKeyProfile, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return encryptedPinBlock; // pass-through in development; no real translation

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            // Simulate zone-key translation: HMAC(destinationKey, encryptedPinBlock). For test only.
            var destKey = await _secrets.GetSecretAsync($"SoftwarePinKey:{destinationKeyProfile}", cancellationToken).ConfigureAwait(false);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(destKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.ASCII.GetBytes(encryptedPinBlock)));
        }

        var response = await PostHsmAsync<TranslatePinBlockResponse>("translate-pin-block",
            new HsmTranslatePinRequest(encryptedPinBlock, sourceKeyProfile, destinationKeyProfile), cancellationToken).ConfigureAwait(false);
        return response.TranslatedPinBlock;
    }

    public async Task<bool> VerifyPinAsync(string encryptedPinBlock, string panSequenceNumber, string keyProfile, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return true; // bypass for development

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(encryptedPinBlock); // accept any non-empty block in test

        var response = await PostHsmAsync<VerifyPinResponse>("verify-pin",
            new HsmVerifyPinRequest(encryptedPinBlock, panSequenceNumber, keyProfile), cancellationToken).ConfigureAwait(false);
        return response.IsValid;
    }

    // ---------------------------------------------------------------
    // CVV generation and verification
    // ---------------------------------------------------------------

    public async Task<string> GenerateCvvAsync(string pan, int expiryMonth, int expiryYear, string serviceCode, string keyProfile, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return "000"; // sentinel development CVV — never use in production

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
            return await ComputeSoftwareCvvAsync(pan, expiryMonth, expiryYear, serviceCode, keyProfile, cancellationToken).ConfigureAwait(false);

        var response = await PostHsmAsync<CvvResponse>("generate-cvv",
            new HsmCvvRequest(pan, expiryMonth, expiryYear, serviceCode, null, keyProfile), cancellationToken).ConfigureAwait(false);
        return response.Cvv;
    }

    public async Task<bool> VerifyCvvAsync(string pan, int expiryMonth, int expiryYear, string serviceCode, string cvv, string keyProfile, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return true; // bypass for development

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
        {
            var expected = await ComputeSoftwareCvvAsync(pan, expiryMonth, expiryYear, serviceCode, keyProfile, cancellationToken).ConfigureAwait(false);
            return string.Equals(expected, cvv, StringComparison.Ordinal);
        }

        var response = await PostHsmAsync<CvvVerifyResponse>("verify-cvv",
            new HsmCvvRequest(pan, expiryMonth, expiryYear, serviceCode, cvv, keyProfile), cancellationToken).ConfigureAwait(false);
        return response.IsValid;
    }

    private async Task<string> ComputeSoftwareCvvAsync(string pan, int expiryMonth, int expiryYear, string serviceCode, string keyProfile, CancellationToken cancellationToken)
    {
        // Simulated CVK-based CVV for testing — mirrors the real algorithm structure:
        // CVV = leftmost 3 digits of DES(CVK, PAN || Expiry || ServiceCode).
        // In production this runs inside the HSM with the real Card Verification Key.
        var secretName = string.IsNullOrWhiteSpace(keyProfile) ? "SoftwareCvk" : $"SoftwareCvk:{keyProfile}";
        string cvk;
        try { cvk = await _secrets.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false); }
        catch { cvk = "test-cvk-development-only"; }

        var data = $"{pan}{expiryYear:D4}{expiryMonth:D2}{serviceCode}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(cvk));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
        var numeric = string.Concat(hash.Select(b => (b % 10).ToString()));
        return numeric[..3];
    }

    // ---------------------------------------------------------------
    // EMV ARQC/ARPC processing
    // ---------------------------------------------------------------

    public async Task<EmvCryptogramResult?> VerifyArqcAndGenerateArpcAsync(string pan, string panSequenceNumber, string arqc, string transactionData, string arpcResponseCode, string keyProfile, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        if (string.Equals(mode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return new EmvCryptogramResult(true, "0000000000000000"); // accept all in development

        if (string.Equals(mode, "HmacSoftwareForTestOnly", StringComparison.OrdinalIgnoreCase))
            return new EmvCryptogramResult(!string.IsNullOrWhiteSpace(arqc), "FFFFFFFFFFFFFFFF");

        var response = await PostHsmAsync<ArqcVerifyResponse>("verify-arqc",
            new HsmArqcRequest(pan, panSequenceNumber, arqc, transactionData, arpcResponseCode, keyProfile), cancellationToken).ConfigureAwait(false);
        return response.IsValid ? new EmvCryptogramResult(true, response.Arpc, response.Script ?? string.Empty) : null;
    }

    // ---------------------------------------------------------------
    // Request / response records for new HSM operations
    // ---------------------------------------------------------------

    private sealed record HsmTranslatePinRequest(
        [property: JsonPropertyName("pinBlock")] string PinBlock,
        [property: JsonPropertyName("sourceKeyProfile")] string SourceKeyProfile,
        [property: JsonPropertyName("destinationKeyProfile")] string DestinationKeyProfile);

    private sealed record TranslatePinBlockResponse(
        [property: JsonPropertyName("translatedPinBlock")] string TranslatedPinBlock);

    private sealed record HsmVerifyPinRequest(
        [property: JsonPropertyName("pinBlock")] string PinBlock,
        [property: JsonPropertyName("panSequenceNumber")] string PanSequenceNumber,
        [property: JsonPropertyName("keyProfile")] string KeyProfile);

    private sealed record VerifyPinResponse([property: JsonPropertyName("isValid")] bool IsValid);

    private sealed record HsmCvvRequest(
        [property: JsonPropertyName("pan")] string Pan,
        [property: JsonPropertyName("expiryMonth")] int ExpiryMonth,
        [property: JsonPropertyName("expiryYear")] int ExpiryYear,
        [property: JsonPropertyName("serviceCode")] string ServiceCode,
        [property: JsonPropertyName("cvv")] string? Cvv,
        [property: JsonPropertyName("keyProfile")] string KeyProfile);

    private sealed record CvvResponse([property: JsonPropertyName("cvv")] string Cvv);

    private sealed record CvvVerifyResponse([property: JsonPropertyName("isValid")] bool IsValid);

    private sealed record HsmArqcRequest(
        [property: JsonPropertyName("pan")] string Pan,
        [property: JsonPropertyName("panSequenceNumber")] string PanSequenceNumber,
        [property: JsonPropertyName("arqc")] string Arqc,
        [property: JsonPropertyName("transactionData")] string TransactionData,
        [property: JsonPropertyName("arpcResponseCode")] string ArpcResponseCode,
        [property: JsonPropertyName("keyProfile")] string KeyProfile);

    private sealed record ArqcVerifyResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("arpc")] string Arpc,
        [property: JsonPropertyName("script")] string? Script);
}
