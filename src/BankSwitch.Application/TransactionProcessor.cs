using System.Diagnostics;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class TransactionProcessor
{
    private readonly INodeRepository _nodes;
    private readonly IRouteRepository _routes;
    private readonly StrictIso8583Validator _validator;
    private readonly IHsmClient _hsm;
    private readonly ISinkClient _sinkClient;
    private readonly ITransactionRepository _transactions;
    private readonly ISecretProvider _secrets;
    private readonly ISensitiveDataProtector _dataProtector;
    private readonly IAuditLogger _audit;
    private readonly FeeCalculator _feeCalculator;
    private readonly ICorePrepaidCmsService _cms;
    private readonly CmsAuthorizationOptions _cmsOptions;
    private readonly ITransactionStateMachine _stateMachine;
    private readonly IStandInProcessor _standIn;       // B1: stand-in processing
    private readonly IPreAuthStore _preAuthStore;       // B1: pre-auth flow
    private readonly IDistributedIdempotencyStore _idempotency; // B1: distributed duplicate guard
    private readonly StandInOptions _standInOptions;
    private readonly ITransactionRecoverySnapshotRepository _recoverySnapshots;
    private readonly Iso8583AsciiBitmapFormatter _formatter;

    /// <summary>Exposed for queue latency measurement in <c>TransactionQueueProcessorHostedService</c>.</summary>
    public IClock Clock => _clock;
    private readonly IClock _clock;

    public TransactionProcessor(
        INodeRepository nodes,
        IRouteRepository routes,
        StrictIso8583Validator validator,
        IHsmClient hsm,
        ISinkClient sinkClient,
        ITransactionRepository transactions,
        ISecretProvider secrets,
        ISensitiveDataProtector dataProtector,
        IAuditLogger audit,
        FeeCalculator feeCalculator,
        ICorePrepaidCmsService cms,
        CmsAuthorizationOptions cmsOptions,
        ITransactionStateMachine stateMachine,
        IClock clock,
        IStandInProcessor standIn,
        IPreAuthStore preAuthStore,
        IDistributedIdempotencyStore idempotency,
        StandInOptions standInOptions,
        ITransactionRecoverySnapshotRepository recoverySnapshots,
        Iso8583AsciiBitmapFormatter formatter)
    {
        _nodes = nodes;
        _routes = routes;
        _validator = validator;
        _hsm = hsm;
        _sinkClient = sinkClient;
        _transactions = transactions;
        _secrets = secrets;
        _dataProtector = dataProtector;
        _audit = audit;
        _feeCalculator = feeCalculator;
        _cms = cms;
        _cmsOptions = cmsOptions;
        _stateMachine = stateMachine;
        _clock = clock;
        _standIn = standIn;
        _preAuthStore = preAuthStore;
        _idempotency = idempotency;
        _standInOptions = standInOptions;
        _recoverySnapshots = recoverySnapshots;
        _formatter = formatter;
    }

    public async Task<IsoMessage> ProcessAsync(IsoMessage request, string sourceNodeId, CancellationToken cancellationToken = default)
    {
        request.CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId;
        var stopwatch = Stopwatch.StartNew();
        var macStatus = "NOT_CHECKED";
        SourceNode? sourceNode = null;
        SinkNode? sinkNode = null;
        Scheme? scheme = null;
        Fee? fee = null;
        RouteDefinition? route = null;
        var stan = request.TryGetField(11, out var s) ? s : string.Empty;

        // STATE: Received
        await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Received, "Transaction arrived from TCP gateway", 0, cancellationToken).ConfigureAwait(false);

        // B1: DISTRIBUTED IDEMPOTENCY — claim the key before any processing
        // Key: (sourceNodeId, STAN, businessDate). This is a fast first-line check ahead
        // of the SQL ExistsDuplicateAsync check in StrictIso8583Validator.
        var idempotencyKey = $"{sourceNodeId}:{stan}:{DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime):yyyyMMdd}";
        var idempotencyClaimed = await _idempotency.TryClaimAsync(idempotencyKey, request.CorrelationId, TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
        if (!idempotencyClaimed)
        {
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, "Distributed idempotency: duplicate key claim rejected", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
            return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "94"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            sourceNode = await _nodes.GetSourceNodeAsync(sourceNodeId, cancellationToken).ConfigureAwait(false);
            if (sourceNode is null || !sourceNode.IsActive)
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, "Source node not found or inactive", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "91"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            var validation = await _validator.ValidateAsync(request, sourceNode, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                _audit.LogSystem(request.CorrelationId, $"ISO validation failed: {validation.Reason}");
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, $"Validation failed: {validation.Reason}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, validation.ResponseCode), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            // STATE: Validated
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Validated, "ISO 8583 field validation passed", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);

            var mac = await _hsm.ValidateMacAsync(request, sourceNode, cancellationToken).ConfigureAwait(false);
            macStatus = mac.Status;
            if (!mac.IsValid)
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, "MAC verification failed", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "88"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            // STATE: MacVerified
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.MacVerified, "MAC cryptographic integrity confirmed", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);

            var pan = request.GetRequiredField(2);
            var binPrefix = pan[..6];
            if (sourceNode.AllowedBinRanges.Count > 0 && !sourceNode.AllowedBinRanges.Any(prefix => binPrefix.StartsWith(prefix, StringComparison.Ordinal)))
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, $"BIN {binPrefix} not allowed on source node", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "15"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            var routeCriteria = BuildRouteCriteria(request, sourceNode, pan, binPrefix);
            route = await _routes.GetBestRouteAsync(routeCriteria, cancellationToken).ConfigureAwait(false);
            if (route is null || !route.IsActive)
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, $"No active route for BIN {binPrefix} country={routeCriteria.CountryCode} mcc={routeCriteria.MerchantCategoryCode} currency={routeCriteria.CurrencyCode} device={routeCriteria.DeviceCode} interchange={routeCriteria.InterchangeCode} institution={routeCriteria.InstitutionCode} product={routeCriteria.ProductCode} network={routeCriteria.NetworkCode} account={MaskAccountForLog(routeCriteria.AccountNumber)}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "15"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            // Resolve primary sink — if circuit-broken, try fallback route (A1: NETWORK FAILOVER)
            sinkNode = await ResolveSinkWithFallbackAsync(route, cancellationToken).ConfigureAwait(false);
            if (sinkNode is null)
            {
                // B1: STAND-IN PROCESSING — when all sinks are unavailable
                pan = request.TryGetField(2, out var panForStandIn) ? panForStandIn : string.Empty;
                var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
                var panHash = string.IsNullOrWhiteSpace(pan) ? string.Empty : CardholderDataProtector.HashForLookup(pan, lookupKey);
                binPrefix = pan.Length >= 6 ? pan[..6] : string.Empty;
                var standInProfile = await _standIn.GetProfileForBinAsync(binPrefix, cancellationToken).ConfigureAwait(false);
                var standInDecision = await _standIn.EvaluateAsync(request, sourceNode!, panHash, standInProfile, cancellationToken).ConfigureAwait(false);

                if (standInDecision.IsApproved)
                {
                    await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Responded, $"Stand-in approved: {standInDecision.Basis} authCode={standInDecision.AuthorizationCode}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                    var standInResponse = ResponseBuilder.Approve(request, standInDecision.AuthorizationCode);
                    return await LogAndReturnAsync(request, standInResponse, sourceNode, null, route, scheme, fee, stopwatch, "STAND_IN", TransactionLifecycleState.Responded, cancellationToken).ConfigureAwait(false);
                }

                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, "Primary and fallback sink nodes unavailable (circuit open or inactive)", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "91"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            scheme = await _routes.GetSchemeForSourceAndRouteAsync(sourceNode.Id, route.Id, cancellationToken).ConfigureAwait(false);
            if (scheme is null || !scheme.IsActive)
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, "No active scheme for source+route combination", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "92"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            var transactionCode = request.GetRequiredField(3)[..2];
            var channelCode = string.IsNullOrWhiteSpace(routeCriteria.ChannelCode) ? "00" : routeCriteria.ChannelCode;
            var permission = scheme.Permissions.SingleOrDefault(x => x.TransactionTypeCode == transactionCode && x.ChannelCode == channelCode);
            if (permission is null)
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, $"No scheme permission for txnType={transactionCode} channel={channelCode}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "58"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            fee = await _routes.GetFeeAsync(permission.FeeId, cancellationToken).ConfigureAwait(false);
            if (fee is null || !fee.IsActive)
            {
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, "Scheme fee not found or inactive", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "58"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
            }

            // STATE: Routed
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Routed, $"Routed → sink={sinkNode.NodeId} scheme={scheme.Name} routePriority={route.Priority} criteria=country:{routeCriteria.CountryCode}|mcc:{routeCriteria.MerchantCategoryCode}|currency:{routeCriteria.CurrencyCode}|device:{routeCriteria.DeviceCode}|interchange:{routeCriteria.InterchangeCode}|institution:{routeCriteria.InstitutionCode}|product:{routeCriteria.ProductCode}|network:{routeCriteria.NetworkCode}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);

            IsoFieldHelper.TryGetDecimalAmount(request, 4, out var amount);
            var charge = _feeCalculator.Calculate(fee, amount);
            request.SetField(28, charge.ToString("0.00"));

            if (_cmsOptions.Enabled && _cmsOptions.PrepaidTransactionTypes.Contains(transactionCode))
            {
                request.TryGetField(49, out var cmsCurrency);
                request.TryGetField(11, out var cmsStan);
                request.TryGetField(37, out var cmsRrn);
                request.TryGetField(41, out var cmsTerminalId);
                request.TryGetField(18, out var cmsMcc);
                request.TryGetField(42, out var cmsMerchantId);
                request.TryGetField(43, out var cmsMerchantLocation);
                request.TryGetField(62, out var cmsThreeDsTransactionId);
                request.TryGetField(63, out var cmsCavvToken);
                request.TryGetField(124, out var cmsStoredCredentialIndicator);
                request.TryGetField(125, out var cmsCardOnFileFlag);

                var cmsDecision = await _cms.AuthorizeAsync(new CmsAuthorizationRequest(
                    FullPan: pan,
                    Amount: amount,
                    CurrencyCode: cmsCurrency,
                    ProcessingCode: request.GetRequiredField(3),
                    ChannelCode: channelCode,
                    Stan: cmsStan,
                    Rrn: cmsRrn,
                    Mti: request.Mti,
                    TerminalId: cmsTerminalId,
                    CorrelationId: request.CorrelationId)
                {
                    MerchantCategoryCode = cmsMcc,
                    MerchantId = cmsMerchantId,
                    MerchantName = cmsMerchantLocation,
                    MerchantCountryCode = cmsMerchantLocation.Length >= 2 ? cmsMerchantLocation[^2..] : string.Empty,
                    DeviceId = cmsTerminalId,
                    IsEcommerce = string.Equals(channelCode, "02", StringComparison.OrdinalIgnoreCase) || string.Equals(channelCode, "ECOM", StringComparison.OrdinalIgnoreCase) || string.Equals(channelCode, "WEB", StringComparison.OrdinalIgnoreCase),
                    DirectoryServerTransactionId = cmsThreeDsTransactionId,
                    CavvToken = cmsCavvToken,
                    StoredCredentialIndicator = cmsStoredCredentialIndicator ?? string.Empty,
                    IsCardOnFileToken = string.Equals(cmsCardOnFileFlag, "1", StringComparison.Ordinal)
                }, cancellationToken).ConfigureAwait(false);

                var cmsResponse = ResponseBuilder.Decline(request, cmsDecision.ResponseCode);
                if (!string.IsNullOrWhiteSpace(cmsDecision.AuthorizationCode)) cmsResponse.SetField(38, cmsDecision.AuthorizationCode);
                if (!cmsDecision.IsApproved || !_cmsOptions.ForwardApprovedTransactionsToSink)
                {
                    var cmsState = cmsDecision.IsApproved ? TransactionLifecycleState.CmsApproved : TransactionLifecycleState.Failed;
                    await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, cmsState, $"CMS decision: code={cmsDecision.ResponseCode} approved={cmsDecision.IsApproved}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                    return await LogAndReturnAsync(request, cmsResponse, sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, cmsState, cancellationToken).ConfigureAwait(false);
                }

                // STATE: CmsApproved
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.CmsApproved, $"CMS approved authCode={cmsDecision.AuthorizationCode}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            var outboundMac = await _hsm.GenerateMacAsync(request, sinkNode, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(outboundMac)) request.SetField(64, outboundMac);

            // V44.7: write a durable recovery snapshot BEFORE the request leaves the switch.
            // This closes the unknown-outcome window where a process crash can occur after the
            // issuer received the request but before a TransactionLog was committed locally.
            var recoveryReversal = BuildRecoveryReversal(request);
            var recoveryBytes = _formatter.Format(recoveryReversal);
            var protectedRecoveryPayload = _dataProtector.Protect(Convert.ToBase64String(recoveryBytes), "TXN-RECOVERY-REVERSAL");
            var recoverySnapshot = new TransactionRecoverySnapshot
            {
                CorrelationId = request.CorrelationId,
                SourceNodeId = sourceNodeId,
                SinkNodeId = sinkNode.Id,
                Stan = stan,
                Rrn = request.TryGetField(37, out var recoveryRrn) ? recoveryRrn : string.Empty,
                OriginalMti = request.Mti,
                OriginalDataElement = recoveryReversal.TryGetField(90, out var recoveryOde) ? recoveryOde : string.Empty,
                ProtectedReversalPayload = protectedRecoveryPayload,
                Status = TransactionRecoverySnapshotStatus.Pending,
                ForwardedAt = _clock.UtcNow,
                NextAttemptAt = _clock.UtcNow
            };
            await _recoverySnapshots.UpsertPendingAsync(recoverySnapshot, cancellationToken).ConfigureAwait(false);

            // STATE: ForwardedToSink
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.ForwardedToSink, $"Forwarding to sink={sinkNode.NodeId} host={sinkNode.Host}:{sinkNode.Port}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);

            IsoMessage response;
            try
            {
                response = await _sinkClient.SendAsync(request, sinkNode, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Sink timed out — the durable recovery snapshot remains unresolved and is
                // picked up by TransactionRecoveryWorker for a compensating reversal.
                await _recoverySnapshots.MarkTimedOutAsync(request.CorrelationId, $"Sink {sinkNode.NodeId} timed out", cancellationToken).ConfigureAwait(false);
                await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.TimedOut, $"Sink {sinkNode.NodeId} timed out", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "91"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.TimedOut, cancellationToken).ConfigureAwait(false);
            }

            // STATE: Responded / TimedOut
            response.TryGetField(39, out var responseCode);

            // TcpIsoSinkClient represents a transport timeout as ISO response code 68.
            // A timeout is an unknown-outcome condition: the issuer may have processed the
            // financial request even though the switch did not receive the reply.  Do NOT
            // resolve the durable recovery snapshot in this case; leave it eligible for the
            // compensating 0420 recovery worker.
            if (string.Equals(responseCode, "68", StringComparison.Ordinal))
            {
                await _recoverySnapshots.MarkTimedOutAsync(
                    request.CorrelationId,
                    $"Sink {sinkNode.NodeId} returned transport-timeout response 68",
                    cancellationToken).ConfigureAwait(false);
                await _stateMachine.TransitionAsync(
                    request.CorrelationId,
                    stan,
                    sourceNodeId,
                    TransactionLifecycleState.TimedOut,
                    $"Sink {sinkNode.NodeId} timed out (response 68)",
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken).ConfigureAwait(false);
                return await LogAndReturnAsync(
                    request, response, sourceNode, sinkNode, route, scheme, fee, stopwatch,
                    macStatus, TransactionLifecycleState.TimedOut, cancellationToken).ConfigureAwait(false);
            }

            await _recoverySnapshots.MarkResolvedAsync(request.CorrelationId, responseCode ?? string.Empty, cancellationToken).ConfigureAwait(false);
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Responded, $"Sink responded code={responseCode}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);

            // B1: PRE-AUTH TRACKING — for 0100 (pre-auth) approved responses, store the pre-auth record
            if (request.Mti is "0100" && responseCode is "00" or "08" or "10")
            {
                var preAuthPan = request.TryGetField(2, out var pp) ? pp : string.Empty;
                var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
                request.TryGetField(37, out var preAuthRrn);
                response.TryGetField(38, out var preAuthCode);
                IsoFieldHelper.TryGetDecimalAmount(request, 4, out var preAuthAmount);
                request.TryGetField(49, out var preAuthCurrency);

                var preAuthRecord = new PreAuthRecord
                {
                    SourceNodeId = sourceNodeId,
                    Stan = stan,
                    Rrn = preAuthRrn ?? string.Empty,
                    AuthorizationCode = preAuthCode ?? string.Empty,
                    MaskedPan = CardholderDataProtector.MaskPan(preAuthPan),
                    PanHash = string.IsNullOrWhiteSpace(preAuthPan) ? string.Empty : CardholderDataProtector.HashForLookup(preAuthPan, lookupKey),
                    AuthorizedAmount = preAuthAmount,
                    CurrencyCode = preAuthCurrency ?? string.Empty,
                    SinkNodeId = sinkNode.NodeId,
                    OriginalCorrelationId = request.CorrelationId,
                    Status = PreAuthStatus.Approved,
                    CreatedAt = _clock.UtcNow,
                    ExpiresAt = _clock.UtcNow.AddHours(24) // hotels hold 24 hours by default
                };
                await _preAuthStore.AddAsync(preAuthRecord, cancellationToken).ConfigureAwait(false);
                _audit.LogSystem(request.CorrelationId, $"Pre-auth recorded: RRN={preAuthRrn} amount={preAuthAmount} authCode={preAuthCode}");
            }

            // B1: PRE-AUTH COMPLETION — for 0220 completion, mark the original pre-auth as completed
            if (request.Mti is "0220" && (responseCode is "00" or "10"))
            {
                request.TryGetField(37, out var completionRrn);
                var preAuth = await _preAuthStore.FindByRrnAndSourceAsync(completionRrn ?? string.Empty, sourceNodeId, cancellationToken).ConfigureAwait(false);
                if (preAuth is not null)
                {
                    IsoFieldHelper.TryGetDecimalAmount(request, 4, out var captureAmount);
                    var completed = preAuth with { Status = PreAuthStatus.Completed, CompletedAt = _clock.UtcNow, CompletedAmount = captureAmount, CompletionCorrelationId = request.CorrelationId };
                    await _preAuthStore.UpdateAsync(completed, cancellationToken).ConfigureAwait(false);
                    _audit.LogSystem(request.CorrelationId, $"Pre-auth completed: RRN={completionRrn} captureAmount={captureAmount}");
                }
            }

            // B1: PRE-AUTH VOID — for 0420, mark the original pre-auth as voided
            if (request.Mti is "0420" or "0421")
            {
                request.TryGetField(37, out var voidRrn);
                var preAuth = await _preAuthStore.FindByRrnAndSourceAsync(voidRrn ?? string.Empty, sourceNodeId, cancellationToken).ConfigureAwait(false);
                if (preAuth is not null && preAuth.Status == PreAuthStatus.Approved)
                {
                    var voided = preAuth with { Status = PreAuthStatus.Voided };
                    await _preAuthStore.UpdateAsync(voided, cancellationToken).ConfigureAwait(false);
                    _audit.LogSystem(request.CorrelationId, $"Pre-auth voided: RRN={voidRrn}");
                }
            }

            return await LogAndReturnAsync(request, response, sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Responded, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _audit.LogSystem(request.CorrelationId, "Structured transaction processing error", ex);
            await _stateMachine.TransitionAsync(request.CorrelationId, stan, sourceNodeId, TransactionLifecycleState.Failed, $"Unhandled exception: {ex.GetType().Name}", stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
            return await LogAndReturnAsync(request, ResponseBuilder.Decline(request, "06"), sourceNode, sinkNode, route, scheme, fee, stopwatch, macStatus, TransactionLifecycleState.Failed, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A1 FAILOVER: Attempts primary sink. If its circuit breaker is open,
    /// tries the route's fallback sink node. Returns null only when both are unavailable.
    /// </summary>
    private static RouteMatchCriteria BuildRouteCriteria(IsoMessage request, SourceNode sourceNode, string pan, string binPrefix)
    {
        var channelCode = TryGetChannelCode(request);
        var countryCode = FirstNonEmpty(GetField(request, 19), ExtractCountryFromMerchantLocation(GetField(request, 43)));
        var institutionCode = FirstNonEmpty(GetField(request, 32), GetField(request, 33), GetField(request, 100), sourceNode.InstitutionCode);
        var networkCode = FirstNonEmpty(GetField(request, 24), GetField(request, 123));
        var interchangeCode = FirstNonEmpty(GetField(request, 24), GetField(request, 32), networkCode);
        var productCode = FirstNonEmpty(GetField(request, 120), GetField(request, 123));
        var accountNumber = FirstNonEmpty(GetField(request, 102), GetField(request, 103));

        return new RouteMatchCriteria
        {
            Pan = pan,
            BinPrefix = binPrefix,
            CountryCode = NormalizeCriteria(countryCode),
            MerchantCategoryCode = NormalizeCriteria(GetField(request, 18)),
            CurrencyCode = NormalizeCriteria(GetField(request, 49)),
            DeviceCode = NormalizeCriteria(GetField(request, 41)),
            InterchangeCode = NormalizeCriteria(interchangeCode),
            InstitutionCode = NormalizeCriteria(institutionCode),
            ProductCode = NormalizeCriteria(productCode),
            NetworkCode = NormalizeCriteria(networkCode),
            AccountNumber = NormalizeCriteria(accountNumber),
            ChannelCode = NormalizeCriteria(channelCode),
            ProcessingCode = NormalizeCriteria(GetField(request, 3))
        };
    }

    private static string TryGetChannelCode(IsoMessage request)
    {
        var field123 = GetField(request, 123);
        if (field123.Length >= 15) return field123.Substring(13, 2);
        return FirstNonEmpty(GetField(request, 22), GetField(request, 25));
    }

    private static string GetField(IsoMessage request, int field)
        => request.TryGetField(field, out var value) ? value ?? string.Empty : string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string NormalizeCriteria(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string ExtractCountryFromMerchantLocation(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length >= 2 ? trimmed[^2..] : string.Empty;
    }

    private static string MaskAccountForLog(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return string.Empty;
        return accountNumber.Length <= 4 ? "****" : new string('*', Math.Max(0, accountNumber.Length - 4)) + accountNumber[^4..];
    }

    private async Task<SinkNode?> ResolveSinkWithFallbackAsync(RouteDefinition route, CancellationToken cancellationToken)
    {
        var primary = await _nodes.GetSinkNodeAsync(route.SinkNodeId, cancellationToken).ConfigureAwait(false);
        if (primary is not null && primary.IsActive)
            return primary;

        if (route.FallbackSinkNodeId.HasValue)
        {
            _audit.LogSystem(Guid.NewGuid().ToString("N"), $"Primary sink {route.SinkNodeId} unavailable. Trying fallback sink {route.FallbackSinkNodeId.Value}.");
            var fallback = await _nodes.GetSinkNodeAsync(route.FallbackSinkNodeId.Value, cancellationToken).ConfigureAwait(false);
            if (fallback is not null && fallback.IsActive)
                return fallback;
        }

        return null;
    }

    private IsoMessage BuildRecoveryReversal(IsoMessage request)
    {
        var reversal = new IsoMessage("0420") { CorrelationId = $"REC-{request.CorrelationId}" };
        CopyIfPresent(request, reversal, 2);
        CopyIfPresent(request, reversal, 3);
        CopyIfPresent(request, reversal, 4);
        reversal.SetField(7, _clock.UtcNow.ToString("MMddHHmmss"));
        CopyIfPresent(request, reversal, 11);
        CopyIfPresent(request, reversal, 37);
        CopyIfPresent(request, reversal, 41);
        CopyIfPresent(request, reversal, 42);
        CopyIfPresent(request, reversal, 49);
        reversal.SetField(90, BuildOriginalDataElement(request));
        reversal.SetField(123, "000000000000001");
        return reversal;
    }

    private static string BuildOriginalDataElement(IsoMessage request)
    {
        var stan = request.TryGetField(11, out var f11) ? f11.PadLeft(6, '0')[..6] : "000000";
        var transmission = request.TryGetField(7, out var f7) ? f7.PadLeft(10, '0')[..10] : "0000000000";
        var acquirer = request.TryGetField(32, out var f32) ? f32.PadLeft(11, '0')[..11] : "00000000000";
        var forwarder = request.TryGetField(33, out var f33) ? f33.PadLeft(11, '0')[..11] : "00000000000";
        var mti = request.Mti.PadLeft(4, '0')[..4];
        return mti + stan + transmission + acquirer + forwarder;
    }

    private static void CopyIfPresent(IsoMessage source, IsoMessage target, int field)
    {
        if (source.TryGetField(field, out var value) && !string.IsNullOrWhiteSpace(value)) target.SetField(field, value);
    }

    private async Task<IsoMessage> LogAndReturnAsync(
        IsoMessage request,
        IsoMessage response,
        SourceNode? sourceNode,
        SinkNode? sinkNode,
        RouteDefinition? route,
        Scheme? scheme,
        Fee? fee,
        Stopwatch stopwatch,
        string macStatus,
        TransactionLifecycleState lifecycleState,
        CancellationToken cancellationToken)
    {
        stopwatch.Stop();
        var pan = request.TryGetField(2, out var p) ? p : string.Empty;
        var lookupSecret = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        IsoFieldHelper.TryGetDecimalAmount(request, 4, out var amount);

        var log = new TransactionLog
        {
            CorrelationId = request.CorrelationId,
            Mti = request.Mti,
            SourceNodeId = sourceNode?.NodeId ?? string.Empty,
            SinkNodeId = sinkNode?.NodeId ?? string.Empty,
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanToken = string.IsNullOrWhiteSpace(pan) ? string.Empty : _dataProtector.Protect(pan, "PAN"),
            PanHash = string.IsNullOrWhiteSpace(pan) ? string.Empty : CardholderDataProtector.HashForLookup(pan, lookupSecret),
            Stan = request.TryGetField(11, out var stan) ? stan : string.Empty,
            Rrn = request.TryGetField(37, out var rrn) ? rrn : string.Empty,
            Amount = amount,
            CurrencyCode = request.TryGetField(49, out var currency) ? currency : string.Empty,
            ResponseCode = response.TryGetField(39, out var rc) ? rc : string.Empty,
            LatencyMilliseconds = stopwatch.ElapsedMilliseconds,
            RouteUsed = route?.BinPrefix ?? string.Empty,
            SchemeUsed = scheme?.Name ?? string.Empty,
            FeeApplied = fee?.Name ?? string.Empty,
            MacValidationStatus = macStatus,
            LifecycleState = lifecycleState,
            // Clearing: capture settlement profile from the sink node for clearing batch grouping
            SettlementProfile = sinkNode?.SettlementProfile ?? string.Empty
        };
        await _transactions.SaveTransactionAsync(log, cancellationToken).ConfigureAwait(false);
        _audit.LogTransaction(log);
        return response;
    }
}
