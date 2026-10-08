using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Engine;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "BankSwitch.Engine");
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
// 1. Keeps TransactionRecoveryService happy (The one you just added)
builder.Services.AddSingleton<BankSwitch.Application.Iso8583AsciiBitmapFormatter>();

// 2. Fixes HsmClient (The missing one causing the new crash)
builder.Services.AddSingleton<BankSwitch.Infrastructure.Iso8583AsciiBitmapFormatter>();
builder.Services.AddSingleton<ISensitiveDataProtector>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var mode = configuration["SensitiveData:Mode"] ?? "AesGcm";
    return string.Equals(mode, "Development", StringComparison.OrdinalIgnoreCase)
        ? new DevelopmentSensitiveDataProtector()
        : new AesGcmSensitiveDataProtector(configuration, sp.GetRequiredService<ISecretProvider>());
});

var repositoryProvider = builder.Configuration["Repository:Provider"] ?? "InMemory";
if (string.Equals(repositoryProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    //builder.Services.AddSingleton<SecureSqlConnectionFactory>();
    builder.Services.AddSingleton<SecurePostgresConnectionFactory>();
    builder.Services.AddSingleton<SqlSwitchRepository>();
    builder.Services.AddSingleton<SqlPrepaidCmsRepository>();
    builder.Services.AddSingleton<SqlOperationalControlRepository>();
    builder.Services.AddSingleton<SqlFinancialOperationsRepository>();
    builder.Services.AddSingleton<SqlEnterpriseProductionRepository>();
    builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<INodeRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<IRouteRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<IReversalRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<ITransactionStateRepository, SqlTransactionStateRepository>();
    builder.Services.AddSingleton<IStandInRepository, SqlStandInRepository>();
    builder.Services.AddSingleton<IPreAuthStore, SqlPreAuthStore>();
    builder.Services.AddSingleton<ITransactionRecoverySnapshotRepository, SqlTransactionRecoverySnapshotRepository>();
    builder.Services.AddSingleton<ICmsRepository>(sp => sp.GetRequiredService<SqlPrepaidCmsRepository>());
    builder.Services.AddSingleton<IOperationalControlRepository>(sp => sp.GetRequiredService<SqlOperationalControlRepository>());
    builder.Services.AddSingleton<IFinancialOperationsRepository>(sp => sp.GetRequiredService<SqlFinancialOperationsRepository>());
    builder.Services.AddSingleton<IEnterpriseProductionRepository>(sp => sp.GetRequiredService<SqlEnterpriseProductionRepository>());
}
else
{
    builder.Services.AddSingleton<InMemorySwitchStore>();
    builder.Services.AddSingleton<InMemoryPrepaidCmsRepository>();
    builder.Services.AddSingleton<InMemoryOperationalControlRepository>();
    builder.Services.AddSingleton<InMemoryFinancialOperationsRepository>();
    builder.Services.AddSingleton<InMemoryEnterpriseProductionRepository>();
    builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<INodeRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<IRouteRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<IReversalRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<ITransactionStateRepository, InMemoryTransactionStateRepository>();
    builder.Services.AddSingleton<IStandInRepository, InMemoryStandInRepository>();
    builder.Services.AddSingleton<IPreAuthStore, InMemoryPreAuthStore>();
    builder.Services.AddSingleton<ITransactionRecoverySnapshotRepository, InMemoryTransactionRecoverySnapshotRepository>();
    builder.Services.AddSingleton<ICmsRepository>(sp => sp.GetRequiredService<InMemoryPrepaidCmsRepository>());
    builder.Services.AddSingleton<IOperationalControlRepository>(sp => sp.GetRequiredService<InMemoryOperationalControlRepository>());
    builder.Services.AddSingleton<IFinancialOperationsRepository>(sp => sp.GetRequiredService<InMemoryFinancialOperationsRepository>());
    builder.Services.AddSingleton<IEnterpriseProductionRepository>(sp => sp.GetRequiredService<InMemoryEnterpriseProductionRepository>());
}

builder.Services.AddSingleton<IAuditLogger, StructuredAuditLogger>();
builder.Services.AddSingleton<ICardNumberGenerator, SecureCardNumberGenerator>();
builder.Services.AddSingleton(sp => new CmsAuthorizationOptions
{
    Enabled = builder.Configuration.GetValue("CmsAuthorization:Enabled", true),
    ForwardApprovedTransactionsToSink = builder.Configuration.GetValue("CmsAuthorization:ForwardApprovedTransactionsToSink", false),
    PrepaidTransactionTypes = (builder.Configuration.GetSection("CmsAuthorization:PrepaidTransactionTypes").Get<string[]>() ?? new[] { "00", "20" }).ToHashSet(StringComparer.OrdinalIgnoreCase)
});
builder.Services.AddSingleton<ICorePrepaidCmsService, CorePrepaidCmsService>();
builder.Services.AddSingleton<IOperationalControlService, OperationalControlService>();
builder.Services.AddSingleton<IFinancialOperationsService, FinancialOperationsService>();
builder.Services.AddSingleton(sp => new EnterpriseProductionOptions
{
    Enabled = builder.Configuration.GetValue("EnterpriseProduction:Enabled", true),
    RequireThreeDsForCardNotPresent = builder.Configuration.GetValue("EnterpriseProduction:RequireThreeDsForCardNotPresent", true),
    ThreeDsRequiredChannels = (builder.Configuration.GetSection("EnterpriseProduction:ThreeDsRequiredChannels").Get<string[]>() ?? new[] { "02", "ECOM", "WEB" }).ToHashSet(StringComparer.OrdinalIgnoreCase),
    ThreeDsExpiryMinutes = builder.Configuration.GetValue("EnterpriseProduction:ThreeDsExpiryMinutes", 10),
    ThreeDsChallengeAmountThreshold = builder.Configuration.GetValue("EnterpriseProduction:ThreeDsChallengeAmountThreshold", 100000m),
    FraudAlertScoreThreshold = builder.Configuration.GetValue("EnterpriseProduction:FraudAlertScoreThreshold", 60),
    FraudDeclineScoreThreshold = builder.Configuration.GetValue("EnterpriseProduction:FraudDeclineScoreThreshold", 85),
    VelocityEventCountThreshold = builder.Configuration.GetValue("EnterpriseProduction:VelocityEventCountThreshold", 5),
    VelocityWindow = TimeSpan.FromMinutes(builder.Configuration.GetValue("EnterpriseProduction:VelocityWindowMinutes", 10)),
    DataWarehouseOutputRoot = builder.Configuration.GetValue("EnterpriseProduction:DataWarehouseOutputRoot", "./warehouse-exports"),
    ClusterHeartbeatTtl = TimeSpan.FromSeconds(builder.Configuration.GetValue("EnterpriseProduction:ClusterHeartbeatTtlSeconds", 90)),
    HeartbeatInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue("EnterpriseProduction:HeartbeatIntervalSeconds", 30)),
    WarehouseWorkerInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue("EnterpriseProduction:WarehouseWorkerIntervalSeconds", 300)),
    SiemWorkerInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue("EnterpriseProduction:SiemWorkerIntervalSeconds", 15))
});
builder.Services.AddSingleton<IEnterpriseProductionService, EnterpriseProductionService>();
builder.Services.AddSingleton<INotificationDispatcher, InProcessNotificationDispatcher>();
builder.Services.AddSingleton<IHsmClient, HsmClient>();
// V37: Real HSM & Key Management Production Core services for engine-side key operations and audits
builder.Services.AddSingleton<IHsmKeyManagementRepository, InMemoryHsmKeyManagementRepository>();
builder.Services.AddSingleton<IHsmCommandAdapter, ThalesPayShieldAdapter>();
builder.Services.AddSingleton<IHsmCommandAdapter, AtallaHsmAdapter>();
builder.Services.AddSingleton<IHsmCommandAdapter, FuturexHsmAdapter>();
builder.Services.AddSingleton<IHsmCommandAdapter, SimulatorHsmAdapter>();
builder.Services.AddSingleton<IHsmKeyManagementService, HsmKeyManagementService>();
builder.Services.AddSingleton(new CircuitBreakerOptions
{
    FailuresBeforeOpen = builder.Configuration.GetValue("CircuitBreaker:FailuresBeforeOpen", 3),
    OpenDuration = TimeSpan.FromSeconds(builder.Configuration.GetValue("CircuitBreaker:OpenSeconds", 30))
});
builder.Services.AddSingleton<SinkCircuitBreaker>();
builder.Services.AddSingleton<ISinkClient, TcpIsoSinkClient>();
builder.Services.AddSingleton<IReplayCache, InMemoryReplayCache>();
builder.Services.AddSingleton<INodeRateLimiter, SlidingWindowNodeRateLimiter>();
// A3: Real SLA metrics backed by ring buffer + OpenTelemetry instrumentation
var telOpts = builder.Configuration.GetSection("Telemetry").Get<TelemetryOptions>() ?? new TelemetryOptions();
builder.Services.AddSingleton(telOpts);
builder.Services.AddSingleton<BankSwitchInstrumentation>();
builder.Services.AddSingleton<IMetricCollector>(sp => new RingBufferMetricCollector(telOpts.MetricRingBufferCapacity));
builder.Services.AddSingleton<ISlaMetrics, InstrumentedSlaMetrics>();

// A3: Alerting service
var alertOpts = builder.Configuration.GetSection("Alerting").Get<AlertingOptions>() ?? new AlertingOptions();
builder.Services.AddSingleton(alertOpts);
builder.Services.AddSingleton<IAlertingRepository, InMemoryAlertingRepository>();
builder.Services.AddSingleton<ISiemForwarder>(sp =>
{
    var cfg = builder.Configuration;
    var mode = cfg["Siem:Mode"] ?? "LogOnly";
    var logFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
    return mode switch
    {
        "SplunkHec" => (ISiemForwarder)new SplunkHecSiemForwarder(cfg["Siem:SplunkHecUrl"]!, cfg["Siem:SplunkHecToken"]!, logFactory.CreateLogger<SplunkHecSiemForwarder>()),
        "Sentinel" => new SentinelSiemForwarder(cfg["Siem:SentinelWorkspaceId"]!, cfg["Siem:SentinelSharedKey"]!, cfg["Siem:SentinelLogType"] ?? "BankSwitchSecurity", logFactory.CreateLogger<SentinelSiemForwarder>()),
        "Webhook" => new WebhookSiemForwarder(cfg["Siem:WebhookUrl"]!, cfg["Siem:WebhookAuthHeader"], logFactory.CreateLogger<WebhookSiemForwarder>()),
        _ => new LogOnlySiemForwarder(logFactory.CreateLogger<LogOnlySiemForwarder>())
    };
});
builder.Services.AddSingleton<IAlertingService, AlertingService>();
builder.Services.AddSingleton<ITransactionStateMachine, TransactionStateMachine>();
builder.Services.AddSingleton<IClearingRepository, InMemoryClearingRepository>();
builder.Services.AddSingleton<IInterchangeFeeRuleRepository, InMemoryInterchangeFeeRuleRepository>();
builder.Services.AddSingleton<IInterchangeFeeRuleEngine, InterchangeFeeRuleEngine>();
builder.Services.AddSingleton<ISettlementCertificationService, SettlementCertificationService>();
builder.Services.AddSingleton<INetworkSettlementRunRepository, InMemoryNetworkSettlementRunRepository>();
builder.Services.AddSingleton<INetworkSettlementGateway>(_ => new SimulatedNetworkSettlementGateway(SettlementNetwork.Visa));
builder.Services.AddSingleton<INetworkSettlementGateway>(_ => new SimulatedNetworkSettlementGateway(SettlementNetwork.Mastercard));
builder.Services.AddSingleton<INetworkSettlementGateway>(_ => new SimulatedNetworkSettlementGateway(SettlementNetwork.Rupay));
builder.Services.AddSingleton<INetworkSettlementGateway>(_ => new SimulatedNetworkSettlementGateway(SettlementNetwork.NpciNfs));
builder.Services.AddSingleton<INetworkSettlementGateway>(_ => new SimulatedNetworkSettlementGateway(SettlementNetwork.Internal));
builder.Services.AddSingleton<IEftRepository, InMemoryEftRepository>();

// B2: EFT Rail file generators
builder.Services.AddSingleton<INeftBatchRepository, InMemoryNeftBatchRepository>();
builder.Services.AddSingleton<ISwiftMessageRepository, InMemorySwiftMessageRepository>();
builder.Services.AddSingleton<IAchFileRepository, InMemoryAchFileRepository>();
builder.Services.AddSingleton<IDirectDebitMandateRepository, InMemoryDirectDebitMandateRepository>();
builder.Services.AddSingleton<INeftBatchGenerator, NeftBatchGenerator>();
builder.Services.AddSingleton<ISwiftMessageGenerator, SwiftMessageGenerator>();
builder.Services.AddSingleton<IAchBatchGenerator, AchBatchGenerator>();
builder.Services.AddSingleton<IDirectDebitMandateService, DirectDebitMandateService>();

// B2: Chargeback workflow
builder.Services.AddSingleton<IChargebackRepository, InMemoryChargebackRepository>();
builder.Services.AddSingleton<IChargebackService, ChargebackService>();

// B2: Dispute management
builder.Services.AddSingleton<IDisputeRepository, InMemoryDisputeRepository>();
builder.Services.AddSingleton<IDisputeService, DisputeService>();

// B2: Reconciliation engine
builder.Services.AddSingleton(new ReconciliationOptions());
builder.Services.AddSingleton<IReconciliationRepository, InMemoryReconciliationRepository>();
builder.Services.AddSingleton<IReconciliationEngine, ReconciliationEngine>();
// V29: Advanced network reconciliation, ATM EJ/CCTV evidence, C3R and ODR/UDIR
builder.Services.AddSingleton<IAdvancedReconciliationRepository, InMemoryAdvancedReconciliationRepository>();
builder.Services.AddSingleton<INetworkReconciliationFormatParser, NpciNfsSettlementParser>();
builder.Services.AddSingleton<INetworkReconciliationFormatParser, RupaySettlementParser>();
builder.Services.AddSingleton<INetworkReconciliationFormatParser, VisaSettlementParser>();
builder.Services.AddSingleton<INetworkReconciliationFormatParser, MastercardIpmParser>();
builder.Services.AddSingleton<INetworkReconciliationFormatParser, GenericIso8583SettlementParser>();
builder.Services.AddSingleton<IOdrUdirGateway>(_ => new SimulatedOdrUdirGateway(OdrUdirNetwork.RbiOdr));
builder.Services.AddSingleton<IOdrUdirGateway>(_ => new SimulatedOdrUdirGateway(OdrUdirNetwork.NpciUdir));
builder.Services.AddSingleton<IAdvancedReconciliationService, AdvancedReconciliationService>();
// V30: Network dispute file exchange and external ODR/UDIR integration
builder.Services.AddSingleton<INetworkDisputeExchangeRepository, InMemoryNetworkDisputeExchangeRepository>();
builder.Services.AddSingleton<INetworkDisputeFormatAdapter, VisaDisputeFormatAdapter>();
builder.Services.AddSingleton<INetworkDisputeFormatAdapter, MastercardDisputeFormatAdapter>();
builder.Services.AddSingleton<INetworkDisputeFormatAdapter, NpciRupayDisputeFormatAdapter>();
builder.Services.AddSingleton<INetworkDisputeFormatAdapter, NpciNfsDisputeFormatAdapter>();
builder.Services.AddSingleton<INetworkDisputeFormatAdapter, RbiOdrDisputeFormatAdapter>();
builder.Services.AddSingleton<INetworkDisputeFormatAdapter, NpciUdirDisputeFormatAdapter>();
builder.Services.AddSingleton<INetworkDisputeTransportGateway>(_ => new SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork.Visa, "VROL-SFTP"));
builder.Services.AddSingleton<INetworkDisputeTransportGateway>(_ => new SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork.Mastercard, "MCOM-FILEEXPRESS"));
builder.Services.AddSingleton<INetworkDisputeTransportGateway>(_ => new SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork.NpciRupay, "NPCI-SFTP"));
builder.Services.AddSingleton<INetworkDisputeTransportGateway>(_ => new SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork.NpciNfs, "NFS-SFTP"));
builder.Services.AddSingleton<INetworkDisputeTransportGateway>(_ => new SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork.RbiOdr, "RBI-ODR-API"));
builder.Services.AddSingleton<INetworkDisputeTransportGateway>(_ => new SimulatedNetworkDisputeTransportGateway(DisputeExchangeNetwork.NpciUdir, "NPCI-UDIR-API"));
builder.Services.AddSingleton<INetworkDisputeExchangeService, NetworkDisputeExchangeService>();
builder.Services.AddSingleton<ISettlementPositionRepository, InMemorySettlementPositionRepository>();

// B4: Financial Processing capabilities (Engine side)
builder.Services.AddSingleton<IGlAccountRepository, InMemoryGlAccountRepository>();
builder.Services.AddSingleton<IGlAccountService, GlAccountService>();
builder.Services.AddSingleton<IGlPeriodRepository, InMemoryGlPeriodRepository>();
builder.Services.AddSingleton<ILedgerIntegrityService, LedgerIntegrityService>();
builder.Services.AddSingleton<IJournalEngine, JournalEngine>();
builder.Services.AddSingleton<IEndOfDayService, EndOfDayService>();
builder.Services.AddSingleton<IGlExportService, GlExportService>();

// B1: Stand-in processing
var standInOptions = new StandInOptions
{
    Enabled = builder.Configuration.GetValue("StandIn:Enabled", false),
    StuckTransactionThresholdSeconds = builder.Configuration.GetValue("StandIn:StuckTransactionThresholdSeconds", 120),
    RecoveryWorkerIntervalSeconds = builder.Configuration.GetValue("StandIn:RecoveryWorkerIntervalSeconds", 60),
    GlobalFloorLimitAmount = builder.Configuration.GetValue("StandIn:GlobalFloorLimitAmount", 10_000m),
    GlobalFloorLimitCurrency = builder.Configuration["StandIn:GlobalFloorLimitCurrency"] ?? "566"
};
builder.Services.AddSingleton(standInOptions);
builder.Services.AddSingleton<IStandInProcessor, StandInProcessor>();

// B1: Distributed idempotency store (in-process; swap for Redis in production)
builder.Services.AddSingleton<IDistributedIdempotencyStore, InMemoryDistributedIdempotencyStore>();

// B1: Transaction recovery service
builder.Services.AddSingleton<ITransactionRecoveryService, TransactionRecoveryService>();

// B1: ISO certification test runner
builder.Services.AddSingleton<ICertificationTestRunner, TcpCertificationTestRunner>();
builder.Services.AddSingleton(new ClearingEngineOptions
{
    CutOffTime = TimeSpan.Parse(builder.Configuration["Clearing:CutOffTimeUtc"] ?? "22:00:00"),
    WorkerIntervalMinutes = builder.Configuration.GetValue("Clearing:WorkerIntervalMinutes", 15),
    DefaultCurrencyCode = builder.Configuration["Clearing:DefaultCurrencyCode"] ?? "566",
    ClearingOutputDirectory = builder.Configuration["Clearing:OutputDirectory"] ?? "./clearing-files"
});
builder.Services.AddSingleton<IClearingEngineService, ClearingEngineService>();
builder.Services.AddSingleton<IEftRailService, EftRailService>();

// ---------------------------------------------------------------
// B5: Performance capabilities
// ---------------------------------------------------------------
// Connection pool options
var dbPerfOptions = new DatabasePerformanceOptions
{
    MinPoolSize    = builder.Configuration.GetValue("Database:MinPoolSize", 10),
    MaxPoolSize    = builder.Configuration.GetValue("Database:MaxPoolSize", 200),
    ConnectionTimeoutSeconds = builder.Configuration.GetValue("Database:ConnectionTimeoutSeconds", 5),
    CommandTimeoutSeconds    = builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 30),
    SlowConnectionThresholdMs = builder.Configuration.GetValue("Database:SlowConnectionThresholdMs", 100)
};
builder.Services.AddSingleton(dbPerfOptions);

// Redis connection factory + distributed cache
builder.Services.AddSingleton<RedisConnectionFactory>();
builder.Services.AddSingleton<IDistributedCacheService>(sp =>
{
    var redis = sp.GetRequiredService<RedisConnectionFactory>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisDistributedCacheService>>();
    return redis.IsAvailable
        ? new RedisDistributedCacheService(redis, logger)
        : new InMemoryDistributedCacheService();
});
// Redis replay cache (cluster-safe, falls back to in-memory)
builder.Services.AddSingleton<IRedisReplayCache, RedisReplayCache>();

// Redis idempotency store (replaces InMemoryDistributedIdempotencyStore)
builder.Services.AddSingleton<IDistributedIdempotencyStore, RedisDistributedIdempotencyStore>();

// TPS load test harness
builder.Services.AddSingleton<ITpsLoadTestHarness, TpsLoadTestHarness>();

// Active-active cluster coordinator
builder.Services.AddSingleton<IClusterCoordinator, ActiveActiveCoordinator>();

// Outbox publisher (in-memory for dev; swap for RabbitMq/ServiceBus in production)
builder.Services.AddSingleton<IOutboxPublisher, InMemoryOutboxPublisher>();

// ---------------------------------------------------------------
// B3: Security capabilities (Engine side)
// ---------------------------------------------------------------
builder.Services.AddSingleton<ITotpEnrollmentRepository, InMemoryTotpEnrollmentRepository>();
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddSingleton<IPciControlResultRepository, InMemoryPciControlResultRepository>();
builder.Services.AddSingleton<IPciComplianceService, PciDssComplianceService>();

// B7 — AML Integration
builder.Services.AddSingleton<IAmlReportRepository, InMemoryAmlReportRepository>();
builder.Services.AddSingleton<IAmlIntegrationService, AmlIntegrationService>();
builder.Services.AddHostedService<AmlRescreeningWorker>();

// B7 — Fraud Detection Engine
builder.Services.AddSingleton<ICardBaselineRepository, InMemoryCardBaselineRepository>();
builder.Services.AddSingleton<IFraudRulesEngine, FraudRulesEngine>();

// B7 — OWASP Verification
builder.Services.AddSingleton<IOwaspResultRepository, InMemoryOwaspResultRepository>();
builder.Services.AddSingleton<IOwaspVerificationService, OwaspVerificationService>();

// B7 — ISO 27001 Readiness
builder.Services.AddSingleton<IIso27001Repository, InMemoryIso27001Repository>();
builder.Services.AddSingleton<IIso27001Service, Iso27001Service>();

// B7 — KYC Workflow
builder.Services.AddSingleton<IKycWorkflowService, KycWorkflowService>();
builder.Services.AddHostedService<KycRescreeningWorker>();

// B7 — Audit Evidence (Engine registers but Admin serves the endpoints)
builder.Services.AddSingleton<IAuditEvidenceService>(sp => new AuditEvidenceService(
    sp.GetRequiredService<IPciComplianceService>(),
    sp.GetRequiredService<IOwaspVerificationService>(),
    sp.GetRequiredService<IIso27001Service>(),
    sp.GetRequiredService<IAmlReportRepository>(),
    sp.GetRequiredService<IEnterpriseProductionRepository>(),
    //sp.GetService<IJournalEngine>(),
    sp.GetService<ILedgerIntegrityService>(),
    sp.GetRequiredService<IClock>()));

// Ensure the database connection factory is registered
builder.Services.AddSingleton<SecurePostgresConnectionFactory>();

// Register the KYC repository mapping
builder.Services.AddSingleton<IKycRepository, SqlKycRepository>();

// Register the lifecycle service you added in the previous step
builder.Services.AddSingleton<IKycProviderClient, StubKycProviderClient>();
builder.Services.AddSingleton<ICardLifecycleService, CardLifecycleService>();
builder.Services.AddSingleton<IHsmLifecycleRepository, InMemoryHsmLifecycleRepository>();
builder.Services.AddSingleton<IHsmLifecycleService, HsmLifecycleService>();
builder.Services.AddSingleton<IDukptKeyStateRepository, InMemoryDukptKeyStateRepository>();
builder.Services.AddSingleton<IDukptKeyService, DukptKeyService>();
builder.Services.AddSingleton<IKeyRotationScheduler, KeyRotationScheduler>();
builder.Services.AddSingleton<IPanEncryptionValidator, PanEncryptionValidator>();
builder.Services.AddSingleton<PendingResponseRegistry>();
builder.Services.AddSingleton<ITransactionQueue>(sp => new BoundedTransactionQueue(
    builder.Configuration.GetValue("TransactionQueue:Capacity", 4096)));
builder.Services.AddSingleton<FeeCalculator>();

// ---------------------------------------------------------------
// A4: Domain event bus and handlers (Engine side)
// ---------------------------------------------------------------
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddSingleton<IEventHandler<AuthorizationApprovedEvent>, AuthorizationApprovedGlHandler>();
builder.Services.AddSingleton<IEventHandler<WalletTopUpCompletedEvent>, WalletTopUpGlHandler>();
builder.Services.AddSingleton<IEventHandler<CardIssuedEvent>, AuditDomainEventHandler<CardIssuedEvent>>();
builder.Services.AddSingleton<IEventHandler<CardActivatedEvent>, AuditDomainEventHandler<CardActivatedEvent>>();
builder.Services.AddSingleton<IEventHandler<EftTransferInitiatedEvent>, AuditDomainEventHandler<EftTransferInitiatedEvent>>();
builder.Services.AddSingleton<IEventHandler<EftTransferSettledEvent>, AuditDomainEventHandler<EftTransferSettledEvent>>();
builder.Services.AddSingleton<StrictIso8583Validator>();
builder.Services.AddSingleton(new ReversalOptions
{
    MaxAttempts = builder.Configuration.GetValue("Reversal:MaxAttempts", 5),
    WorkerInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue("Reversal:WorkerIntervalSeconds", 30))
});
builder.Services.AddSingleton<ReversalService>();
builder.Services.AddSingleton<TransactionProcessor>();
builder.Services.AddHostedService<AutoReversalBackgroundWorker>();
builder.Services.AddHostedService<EnterpriseHeartbeatHostedService>();
builder.Services.AddHostedService<AlertingBackgroundWorker>();      // A3: real alerting evaluator
builder.Services.AddHostedService<SiemDispatchWorker>();            // A3: real SIEM delivery
builder.Services.AddHostedService<EnterpriseWarehouseExportHostedService>();
builder.Services.AddHostedService<TransactionRecoveryWorker>();     // B1: crash recovery
builder.Services.AddHostedService<PreAuthExpiryWorker>();           // B1: pre-auth expiry
builder.Services.AddHostedService<KeyRotationWorker>();             // B3: key rotation alerts
builder.Services.AddHostedService<ClusterHeartbeatWorker>();        // B5: active-active heartbeat
builder.Services.AddHostedService<OutboxDrainWorker>();             // B5: async outbox delivery
builder.Services.AddHostedService<IsoTcpGatewayHostedService>();
builder.Services.AddHostedService<SwitchWorker>();
builder.Services.AddHostedService<TransactionQueueProcessorHostedService>(sp =>
    new TransactionQueueProcessorHostedService(
        sp.GetRequiredService<ITransactionQueue>(),
        sp.GetRequiredService<TransactionProcessor>(),
        sp.GetRequiredService<PendingResponseRegistry>(),
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TransactionQueueProcessorHostedService>>(),
        builder.Configuration.GetValue("TransactionQueue:ConsumerParallelism", 8)));
builder.Services.AddHostedService<ClearingEngineBackgroundWorker>();
builder.Services.AddHostedService<ReconciliationBackgroundWorker>();  // B2: daily four-way recon
builder.Services.AddHostedService<ChargebackDeadlineMonitor>();       // B2: chargeback deadline alerts

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

var builtApp = builder.Build();

// CD-04 GUARD: Block startup in Production if bypass modes are configured
/*var engineHsmMode = builtApp.Configuration["Hsm:Mode"] ?? "Http";
var engineSensitiveMode = builtApp.Configuration["SensitiveData:Mode"] ?? "AesGcm";
if (builtApp.Environment.IsProduction())*/

var engineHsmMode = builder.Configuration["Hsm:Mode"] ?? "Http";
var engineSensitiveMode = builder.Configuration["SensitiveData:Mode"] ?? "AesGcm";
if (builder.Environment.IsProduction())
{
    if (string.Equals(engineHsmMode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "SECURITY STARTUP GUARD: Hsm:Mode=BypassForDevelopmentOnly must never run in Production. " +
            "All ISO 8583 MAC validations would be bypassed — any terminal could submit unauthenticated messages.");
    if (string.Equals(engineSensitiveMode, "Development", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "SECURITY STARTUP GUARD: SensitiveData:Mode=Development must never run in Production.");
}

await builtApp.RunAsync().ConfigureAwait(false);
