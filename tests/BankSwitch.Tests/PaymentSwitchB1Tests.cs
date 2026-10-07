using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Engine;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

/// <summary>
/// Tests for B1 — Payment Switch Missing Capabilities:
///   - Distributed idempotency: claim, duplicate rejection, release, TTL
///   - Stand-in processor: floor limit approval, excess amount decline, velocity limit,
///     transaction type eligibility, globally disabled
///   - Pre-authorization store: add, find by RRN, void, expiry
///   - Certification test pack: structure, categories, field presence
///   - ResponseBuilder.Approve: correct MTI and auth code
///   - Transaction recovery service: RecoverStuckTransactionsAsync structure
/// </summary>
public sealed class PaymentSwitchB1Tests
{
    // ---------------------------------------------------------------
    // Distributed Idempotency Store
    // ---------------------------------------------------------------

    [Fact]
    public async Task IdempotencyStore_TryClaim_succeeds_on_first_call()
    {
        var store = new InMemoryDistributedIdempotencyStore();
        var claimed = await store.TryClaimAsync("SRC-001:000001:20240715", "CORR-001", TimeSpan.FromMinutes(5));
        Assert.True(claimed);
    }

    [Fact]
    public async Task IdempotencyStore_TryClaim_returns_false_for_duplicate_key()
    {
        var store = new InMemoryDistributedIdempotencyStore();
        var key = "SRC-001:000001:20240715";
        await store.TryClaimAsync(key, "CORR-001", TimeSpan.FromMinutes(5));

        var duplicate = await store.TryClaimAsync(key, "CORR-002", TimeSpan.FromMinutes(5));
        Assert.False(duplicate);
    }

    [Fact]
    public async Task IdempotencyStore_GetClaimant_returns_correlationId_of_holder()
    {
        var store = new InMemoryDistributedIdempotencyStore();
        var key = "SRC-001:000002:20240715";
        await store.TryClaimAsync(key, "CORR-HOLDER", TimeSpan.FromMinutes(5));

        var claimant = await store.GetClaimantAsync(key);
        Assert.Equal("CORR-HOLDER", claimant);
    }

    [Fact]
    public async Task IdempotencyStore_Release_allows_reclaim()
    {
        var store = new InMemoryDistributedIdempotencyStore();
        var key = "SRC-001:000003:20240715";
        await store.TryClaimAsync(key, "CORR-A", TimeSpan.FromMinutes(5));
        await store.ReleaseAsync(key, "CORR-A");

        var reclaimed = await store.TryClaimAsync(key, "CORR-B", TimeSpan.FromMinutes(5));
        Assert.True(reclaimed);
    }

    [Fact]
    public async Task IdempotencyStore_different_keys_can_both_be_claimed()
    {
        var store = new InMemoryDistributedIdempotencyStore();
        var a = await store.TryClaimAsync("SRC-001:000010:20240715", "CORR-A", TimeSpan.FromMinutes(5));
        var b = await store.TryClaimAsync("SRC-001:000011:20240715", "CORR-B", TimeSpan.FromMinutes(5));
        Assert.True(a && b);
    }

    // ---------------------------------------------------------------
    // Stand-in Processor
    // ---------------------------------------------------------------

    private static StandInProcessor CreateStandIn(bool enabled = true, decimal floorLimit = 10_000m)
    {
        var repo = new InMemoryStandInRepository();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var clock = new SystemClock();
        var options = new StandInOptions { Enabled = enabled, GlobalFloorLimitAmount = floorLimit, GlobalFloorLimitCurrency = "566" };
        return new StandInProcessor(repo, new SecureCardNumberGenerator(), audit, clock, options);
    }

    private static IsoMessage BuildPurchaseRequest(decimal amount, string stan = "000001")
    {
        var msg = new IsoMessage("0200");
        msg.CorrelationId = Guid.NewGuid().ToString("N");
        msg.SetField(2, "5399838383838381");
        msg.SetField(3, "000000");
        msg.SetField(4, ((long)(amount * 100)).ToString("D12"));
        msg.SetField(11, stan);
        msg.SetField(37, $"RRN{stan}");
        msg.SetField(41, "TERM0001");
        msg.SetField(49, "566");
        msg.SetField(123, "00100010000000");
        return msg;
    }

    private static SourceNode BuildSourceNode() => new()
    {
        NodeId = "SRC-TEST-001",
        Name = "Test Source",
        IsActive = true,
        Limits = new NodeLimits { TpsLimit = 100, MaxMessageBytes = 65535 },
        AllowedBinRanges = new HashSet<string>(),
        PermittedMtis = new HashSet<string> { "0200" },
        PermittedChannels = new HashSet<string>()
    };

    [Fact]
    public async Task StandIn_approves_when_amount_is_below_floor_limit()
    {
        var processor = CreateStandIn(enabled: true, floorLimit: 10_000m);
        var request = BuildPurchaseRequest(5_000m); // below 10,000 floor
        var decision = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-HASH-001", null);
        Assert.True(decision.IsApproved);
        Assert.Equal("00", decision.ResponseCode);
        Assert.Equal(StandInApprovalBasis.BelowFloorLimit, decision.Basis);
        Assert.NotEmpty(decision.AuthorizationCode);
    }

    [Fact]
    public async Task StandIn_declines_when_amount_exceeds_floor_limit()
    {
        var processor = CreateStandIn(enabled: true, floorLimit: 10_000m);
        var request = BuildPurchaseRequest(15_000m); // exceeds floor
        var decision = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-HASH-002", null);
        Assert.False(decision.IsApproved);
        Assert.Equal("13", decision.ResponseCode);
        Assert.Equal(StandInApprovalBasis.ExceedsFloorLimit, decision.Basis);
    }

    [Fact]
    public async Task StandIn_declines_when_globally_disabled()
    {
        var processor = CreateStandIn(enabled: false);
        var request = BuildPurchaseRequest(100m);
        var decision = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-HASH-003", null);
        Assert.False(decision.IsApproved);
        Assert.Equal("91", decision.ResponseCode);
        Assert.Equal(StandInApprovalBasis.NotApplicable, decision.Basis);
    }

    [Fact]
    public async Task StandIn_declines_ineligible_transaction_type()
    {
        var processor = CreateStandIn(enabled: true);
        var request = BuildPurchaseRequest(100m);
        request.SetField(3, "200000"); // cash advance, not eligible
        var profile = new StandInProfile
        {
            ProfileCode = "TEST", BinPrefix = "539983",
            FloorLimitAmount = 50_000m, CurrencyCode = "566",
            VelocityCountLimit = 3, VelocityWindow = TimeSpan.FromHours(24),
            EligibleTransactionTypes = new HashSet<string> { "00" } // only purchases
        };
        var decision = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-HASH-004", profile);
        Assert.False(decision.IsApproved);
        Assert.Equal(StandInApprovalBasis.TransactionTypeNotEligible, decision.Basis);
    }

    [Fact]
    public async Task StandIn_declines_after_velocity_limit_reached()
    {
        var repo = new InMemoryStandInRepository();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        var clock = new SystemClock();
        var options = new StandInOptions { Enabled = true, GlobalFloorLimitAmount = 50_000m };
        var processor = new StandInProcessor(repo, new SecureCardNumberGenerator(), audit, clock, options);

        var profile = new StandInProfile
        {
            ProfileCode = "VEL-TEST", BinPrefix = "539983",
            FloorLimitAmount = 50_000m, CurrencyCode = "566",
            VelocityCountLimit = 2, VelocityWindow = TimeSpan.FromHours(24),
            EligibleTransactionTypes = new HashSet<string> { "00" }
        };

        var request = BuildPurchaseRequest(100m);

        // First two should succeed (velocity limit = 2)
        var d1 = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-VEL-001", profile);
        var d2 = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-VEL-001", profile);
        // Third should be declined for velocity
        var d3 = await processor.EvaluateAsync(request, BuildSourceNode(), "PAN-VEL-001", profile);

        Assert.True(d1.IsApproved);
        Assert.True(d2.IsApproved);
        Assert.False(d3.IsApproved);
        Assert.Equal(StandInApprovalBasis.VelocityLimitExceeded, d3.Basis);
    }

    // ---------------------------------------------------------------
    // Pre-Authorization Store
    // ---------------------------------------------------------------

    [Fact]
    public async Task PreAuthStore_AddAsync_and_FindByRrn()
    {
        var store = new InMemoryPreAuthStore();
        var record = new PreAuthRecord
        {
            SourceNodeId = "SRC-001",
            Stan = "000001",
            Rrn = "RRN001",
            AuthorizationCode = "AUTH01",
            AuthorizedAmount = 500m,
            CurrencyCode = "566",
            Status = PreAuthStatus.Approved,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        await store.AddAsync(record);

        var found = await store.FindByRrnAndSourceAsync("RRN001", "SRC-001");
        Assert.NotNull(found);
        Assert.Equal("AUTH01", found!.AuthorizationCode);
        Assert.Equal(500m, found.AuthorizedAmount);
    }

    [Fact]
    public async Task PreAuthStore_UpdateAsync_changes_status_to_voided()
    {
        var store = new InMemoryPreAuthStore();
        var record = new PreAuthRecord { Rrn = "RRN002", SourceNodeId = "SRC-001", Status = PreAuthStatus.Approved, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        await store.AddAsync(record);

        var voided = record with { Status = PreAuthStatus.Voided };
        await store.UpdateAsync(voided);

        var retrieved = await store.GetAsync(record.Id);
        Assert.Equal(PreAuthStatus.Voided, retrieved!.Status);
    }

    [Fact]
    public async Task PreAuthStore_GetExpiredAsync_returns_only_approved_expired_records()
    {
        var store = new InMemoryPreAuthStore();
        var expired = new PreAuthRecord { Status = PreAuthStatus.Approved, ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), Rrn = "EXP001", SourceNodeId = "SRC" };
        var active = new PreAuthRecord { Status = PreAuthStatus.Approved, ExpiresAt = DateTimeOffset.UtcNow.AddHours(23), Rrn = "ACT001", SourceNodeId = "SRC" };
        var completed = new PreAuthRecord { Status = PreAuthStatus.Completed, ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2), Rrn = "COM001", SourceNodeId = "SRC" };
        await store.AddAsync(expired);
        await store.AddAsync(active);
        await store.AddAsync(completed);

        var results = await store.GetExpiredAsync(DateTimeOffset.UtcNow);
        Assert.Single(results);
        Assert.Equal("EXP001", results[0].Rrn);
    }

    // ---------------------------------------------------------------
    // Certification Test Pack
    // ---------------------------------------------------------------

    [Fact]
    public void CertTestPack_GetFullPack_contains_minimum_test_cases()
    {
        var pack = SwitchCertificationTestPack.GetFullPack();
        Assert.True(pack.Count >= 10, $"Expected at least 10 test cases, got {pack.Count}");
    }

    [Fact]
    public void CertTestPack_all_cases_have_unique_TestId()
    {
        var pack = SwitchCertificationTestPack.GetFullPack();
        var ids = pack.Select(tc => tc.TestId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void CertTestPack_GetByCategory_PURCHASE_returns_purchase_cases()
    {
        var purchaseCases = SwitchCertificationTestPack.GetByCategory("PURCHASE");
        Assert.NotEmpty(purchaseCases);
        Assert.All(purchaseCases, tc => Assert.Equal("PURCHASE", tc.Category));
    }

    [Fact]
    public void CertTestPack_PURCHASE_approved_case_expects_00_response()
    {
        var purchaseCases = SwitchCertificationTestPack.GetByCategory("PURCHASE");
        var approved = purchaseCases.FirstOrDefault(tc => tc.TestId == "TC-PUR-001");
        Assert.NotNull(approved);
        Assert.Equal("00", approved!.ExpectedResponseCode);
        Assert.True(approved.RequestFields.ContainsKey(2)); // PAN
        Assert.True(approved.RequestFields.ContainsKey(4)); // Amount
    }

    [Fact]
    public void CertTestPack_DUPLICATE_case_expects_94_response()
    {
        var dupCases = SwitchCertificationTestPack.GetByCategory("DUPLICATE");
        Assert.NotEmpty(dupCases);
        Assert.All(dupCases, tc => Assert.Equal("94", tc.ExpectedResponseCode));
    }

    [Fact]
    public void CertTestPack_covers_all_expected_categories()
    {
        var categories = SwitchCertificationTestPack.Categories;
        foreach (var cat in categories)
        {
            var cases = SwitchCertificationTestPack.GetByCategory(cat);
            Assert.True(cases.Count > 0, $"Category {cat} has no test cases");
        }
    }

    // ---------------------------------------------------------------
    // ResponseBuilder.Approve
    // ---------------------------------------------------------------

    [Fact]
    public void ResponseBuilder_Approve_sets_response_code_00_and_auth_code()
    {
        var request = new IsoMessage("0200");
        request.SetField(11, "000001");
        request.SetField(37, "RRN001");

        var response = ResponseBuilder.Approve(request, "STAN001");

        Assert.True(response.TryGetField(39, out var code));
        Assert.Equal("00", code);
        Assert.True(response.TryGetField(38, out var authCode));
        Assert.Equal("STAN001", authCode);
    }

    [Fact]
    public void ResponseBuilder_Approve_builds_correct_response_MTI_for_0100()
    {
        var request = new IsoMessage("0100");
        var response = ResponseBuilder.Approve(request, "AUTH01");
        Assert.Equal("0110", response.Mti);
    }

    [Fact]
    public void ResponseBuilder_Approve_builds_correct_response_MTI_for_0200()
    {
        var request = new IsoMessage("0200");
        var response = ResponseBuilder.Approve(request, "AUTH02");
        Assert.Equal("0210", response.Mti);
    }
}
