using System.Diagnostics;
using System.Net.Sockets;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Engine;

/// <summary>
/// Standard ISO 8583 certification test pack for BankSwitch.
/// Contains test vectors across 6 categories required for Visa/Mastercard/NIBSS
/// scheme certification and internal qualification testing.
///
/// Usage: run via the Admin API endpoint or the Engine CLI flag --run-cert-tests.
/// </summary>
public static class SwitchCertificationTestPack
{
    /// <summary>Returns all standard test vectors in the full certification pack.</summary>
    public static IReadOnlyList<Domain.CertificationTestCase> GetFullPack() =>
        new List<Domain.CertificationTestCase>
        {
            // ---------------------------------------------------------------
            // Category: PURCHASE (0200 Authorization)
            // ---------------------------------------------------------------
            new("TC-PUR-001", "Standard purchase — approved, sufficient balance",
                "PURCHASE", "00",
                new Dictionary<int, string>
                {
                    [0] = "0200", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000001000",  // 10.00 in minor units
                    [11] = "000001", [12] = "120000", [13] = "0715",
                    [22] = "051", [37] = "000000000001", [41] = "TERM0001",
                    [42] = "MERCH0001      ", [49] = "566", [123] = "00100010000000"
                }),

            new("TC-PUR-002", "Purchase — declined, insufficient funds",
                "PURCHASE", "51",
                new Dictionary<int, string>
                {
                    [0] = "0200", [2] = "5399838383838381", [3] = "000000",
                    [4] = "999999999999",  // astronomically large amount
                    [11] = "000002", [12] = "120001", [13] = "0715",
                    [22] = "051", [37] = "000000000002", [41] = "TERM0001",
                    [42] = "MERCH0001      ", [49] = "566", [123] = "00100010000000"
                }),

            new("TC-PUR-003", "Purchase — declined, expired card (expiryMonth in past)",
                "PURCHASE", "54",
                new Dictionary<int, string>
                {
                    [0] = "0200", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000001000",
                    [11] = "000003", [12] = "120002", [13] = "0715",
                    [14] = "2001",  // expired
                    [22] = "051", [37] = "000000000003", [41] = "TERM0001",
                    [42] = "MERCH0001      ", [49] = "566", [123] = "00100010000000"
                }),

            // ---------------------------------------------------------------
            // Category: DUPLICATE DETECTION
            // ---------------------------------------------------------------
            new("TC-DUP-001", "Duplicate STAN — must return 94",
                "DUPLICATE", "94",
                new Dictionary<int, string>
                {
                    [0] = "0200", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000001000",
                    [11] = "000001",  // Same STAN as TC-PUR-001 — must be rejected as duplicate
                    [12] = "120100", [13] = "0715",
                    [22] = "051", [37] = "000000000001", [41] = "TERM0001",
                    [42] = "MERCH0001      ", [49] = "566", [123] = "00100010000000"
                }),

            // ---------------------------------------------------------------
            // Category: PRE-AUTHORIZATION (0100 / 0220 / 0420)
            // ---------------------------------------------------------------
            new("TC-PREAUTH-001", "Pre-authorization 0100 — approved",
                "PRE_AUTH", "00",
                new Dictionary<int, string>
                {
                    [0] = "0100", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000050000",  // 500.00
                    [11] = "000010", [12] = "140000", [13] = "0715",
                    [22] = "051", [37] = "000000000010", [41] = "HOTEL001",
                    [42] = "HOTEL0001      ", [49] = "566", [123] = "00100010000000"
                }),

            new("TC-PREAUTH-002", "Pre-auth completion 0220 — approved",
                "PRE_AUTH", "00",
                new Dictionary<int, string>
                {
                    [0] = "0220", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000040000",  // 400.00 (less than pre-auth)
                    [11] = "000011", [12] = "180000", [13] = "0715",
                    [22] = "051", [37] = "000000000010",  // same RRN as TC-PREAUTH-001
                    [38] = "AUTH01", [41] = "HOTEL001",
                    [42] = "HOTEL0001      ", [49] = "566", [123] = "00100010000000"
                }),

            new("TC-PREAUTH-003", "Pre-auth void 0420 — approved",
                "PRE_AUTH", "00",
                new Dictionary<int, string>
                {
                    [0] = "0420", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000050000",
                    [11] = "000012", [12] = "190000", [13] = "0715",
                    [22] = "051", [37] = "000000000010",
                    [38] = "AUTH01", [41] = "HOTEL001",
                    [42] = "HOTEL0001      ", [49] = "566", [123] = "00100010000000"
                }),

            // ---------------------------------------------------------------
            // Category: REVERSAL (0400 / 0420)
            // ---------------------------------------------------------------
            new("TC-REV-001", "Timeout reversal 0420 — accepted by switch",
                "REVERSAL", "00",
                new Dictionary<int, string>
                {
                    [0] = "0420", [2] = "5399838383838381", [3] = "000000",
                    [4] = "000000001000",
                    [11] = "000020", [12] = "130000", [13] = "0715",
                    [22] = "051", [37] = "000000000020", [38] = "AUTHXX",
                    [41] = "TERM0001", [42] = "MERCH0001      ", [49] = "566",
                    [90] = "0200000020130000071500000000000000", // Original Data Element
                    [123] = "00100010000000"
                }),

            // ---------------------------------------------------------------
            // Category: VALIDATION (bad field, wrong MTI)
            // ---------------------------------------------------------------
            new("TC-VAL-001", "Missing mandatory field (F4 Amount) — must decline",
                "VALIDATION", "30",
                new Dictionary<int, string>
                {
                    [0] = "0200", [2] = "5399838383838381", [3] = "000000",
                    // F4 intentionally absent
                    [11] = "000030", [12] = "120000", [13] = "0715",
                    [22] = "051", [37] = "000000000030", [41] = "TERM0001",
                    [42] = "MERCH0001      ", [49] = "566", [123] = "00100010000000"
                }),

            new("TC-VAL-002", "Unsupported MTI 0800 (network management) — must decline",
                "VALIDATION", "12",
                new Dictionary<int, string>
                {
                    [0] = "0800", [11] = "000031", [70] = "301"
                }),

            // ---------------------------------------------------------------
            // Category: BIN ROUTING
            // ---------------------------------------------------------------
            new("TC-ROUTE-001", "Unknown BIN — no route configured, must decline 15",
                "ROUTING", "15",
                new Dictionary<int, string>
                {
                    [0] = "0200", [2] = "9999990000000000", [3] = "000000",
                    [4] = "000000001000",
                    [11] = "000040", [12] = "120000", [13] = "0715",
                    [22] = "051", [37] = "000000000040", [41] = "TERM0001",
                    [42] = "MERCH0001      ", [49] = "566", [123] = "00100010000000"
                }),
        };

    /// <summary>Returns only the test vectors for a specific category.</summary>
    public static IReadOnlyList<Domain.CertificationTestCase> GetByCategory(string category)
        => GetFullPack().Where(tc => string.Equals(tc.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyList<string> Categories =>
        new[] { "PURCHASE", "DUPLICATE", "PRE_AUTH", "REVERSAL", "VALIDATION", "ROUTING" };
}

/// <summary>
/// Connects to the live switch TCP endpoint and executes certification test cases,
/// comparing the actual ISO 8583 response code against the expected code.
/// </summary>
public sealed class TcpCertificationTestRunner : ICertificationTestRunner
{
    private readonly Iso8583AsciiBitmapFormatter _formatter;
    private readonly IAuditLogger _audit;

    public TcpCertificationTestRunner(Iso8583AsciiBitmapFormatter formatter, IAuditLogger audit)
    {
        _formatter = formatter;
        _audit = audit;
    }

    public async Task<CertificationRunResult> RunAsync(
        string switchHost,
        int switchPort,
        string sourceNodeId,
        IReadOnlyList<Domain.CertificationTestCase> testPack,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<Domain.CertificationTestResult>();

        _audit.LogSystem("CERT", $"Certification run started: {testPack.Count} test(s) against {switchHost}:{switchPort}");

        foreach (var testCase in testPack)
        {
            var result = await RunSingleTestAsync(testCase, switchHost, switchPort, sourceNodeId, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            _audit.LogSystem("CERT", $"[{result.Outcome}] {testCase.TestId}: expected={testCase.ExpectedResponseCode} actual={result.ActualResponseCode} latency={result.LatencyMs}ms{(result.FailureReason.Length > 0 ? " reason=" + result.FailureReason : "")}");
        }

        var run = new CertificationRunResult(
            startedAt, DateTimeOffset.UtcNow,
            results.Count,
            results.Count(r => r.Outcome == CertificationTestOutcome.Pass),
            results.Count(r => r.Outcome == CertificationTestOutcome.Fail),
            results.Count(r => r.Outcome == CertificationTestOutcome.Skipped),
            results);

        _audit.LogSystem("CERT", $"Certification complete: passed={run.PassedCases}/{run.TotalCases} failed={run.FailedCases}");
        return run;
    }

    public Task<CertificationRunResult> RunCategoryAsync(string switchHost, int switchPort, string sourceNodeId, string category, CancellationToken cancellationToken = default)
        => RunAsync(switchHost, switchPort, sourceNodeId, SwitchCertificationTestPack.GetByCategory(category), cancellationToken);

    private async Task<Domain.CertificationTestResult> RunSingleTestAsync(
        Domain.CertificationTestCase tc,
        string host, int port,
        string sourceNodeId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            await using var stream = tcp.GetStream();

            var msg = new IsoMessage(tc.RequestFields.TryGetValue(0, out var mtiValue) ? mtiValue : "0200");
            msg.CorrelationId = $"CERT-{tc.TestId}-{Guid.NewGuid().ToString("N")[..8]}";
            foreach (var (field, value) in tc.RequestFields.Where(f => f.Key != 0))
                msg.SetField(field, value);
            // Set source node ID in field 41 if not set
            if (!msg.TryGetField(41, out _)) msg.SetField(41, sourceNodeId[..Math.Min(16, sourceNodeId.Length)].PadRight(16));

            var bytes = _formatter.Format(msg);
            var header = new byte[] { (byte)(bytes.Length >> 8), (byte)(bytes.Length & 0xFF) };
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

            // Read response
            var respHeader = new byte[2];
            await stream.ReadExactlyAsync(respHeader, cancellationToken).ConfigureAwait(false);
            var respLen = (respHeader[0] << 8) | respHeader[1];
            var respBytes = new byte[respLen];
            await stream.ReadExactlyAsync(respBytes, cancellationToken).ConfigureAwait(false);

            var response = _formatter.Parse(respBytes);
            response.TryGetField(39, out var actualCode);
            sw.Stop();

            var pass = string.Equals(actualCode, tc.ExpectedResponseCode, StringComparison.Ordinal);
            return new Domain.CertificationTestResult(
                tc.TestId, tc.Description,
                pass ? CertificationTestOutcome.Pass : CertificationTestOutcome.Fail,
                actualCode ?? "(no field 39)",
                tc.ExpectedResponseCode,
                sw.ElapsedMilliseconds,
                pass ? string.Empty : $"Expected {tc.ExpectedResponseCode} but got {actualCode}");
        }
        catch (OperationCanceledException)
        {
            return new Domain.CertificationTestResult(tc.TestId, tc.Description, CertificationTestOutcome.Skipped,
                string.Empty, tc.ExpectedResponseCode, sw.ElapsedMilliseconds, "Test run cancelled");
        }
        catch (Exception ex)
        {
            return new Domain.CertificationTestResult(tc.TestId, tc.Description, CertificationTestOutcome.Fail,
                string.Empty, tc.ExpectedResponseCode, sw.ElapsedMilliseconds, $"Exception: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
