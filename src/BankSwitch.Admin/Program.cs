using BankSwitch.Admin.Endpoints;
using BankSwitch.Admin.Hubs;
using BankSwitch.Admin.Services;
using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 64 * 1024;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddHostedService<CommandCenterRealtimeBroadcaster>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AuthorizeFolder("/ConfigChanges", "ConfigMakerOrChecker");
    options.Conventions.AuthorizeFolder("/Configuration", "ConfigMakerOrChecker");
    options.Conventions.AuthorizeFolder("/Security", "SecurityAdmin");
    options.Conventions.AuthorizeFolder("/Monitoring", "Viewer");
    options.Conventions.AuthorizeFolder("/Reports", "Viewer");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Admin:SessionTimeoutMinutes", 15));
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Viewer", policy => policy.RequireRole("Viewer", "Operations", "CardOperations", "AgencyManager", "CorporateManager", "RiskAnalyst", "RiskManager", "FinanceOfficer", "ReconciliationOfficer", "ConfigMaker", "ConfigChecker", "SecurityAdmin", "Auditor", "SuperAdmin"));
    options.AddPolicy("Operations", policy => policy.RequireRole("Operations", "CardOperations", "AgencyManager", "CorporateManager", "FinanceOfficer", "ReconciliationOfficer", "SuperAdmin"));
    options.AddPolicy("RiskOperations", policy => policy.RequireRole("RiskAnalyst", "RiskManager", "SuperAdmin"));
    options.AddPolicy("ConfigMakerOrChecker", policy => policy.RequireRole("ConfigMaker", "ConfigChecker", "RiskManager", "FinanceOfficer", "ReconciliationOfficer", "SuperAdmin"));
    options.AddPolicy("ConfigMaker", policy => policy.RequireRole("ConfigMaker", "SuperAdmin"));
    options.AddPolicy("ConfigChecker", policy => policy.RequireRole("ConfigChecker", "SuperAdmin"));
    options.AddPolicy("SecurityAdmin", policy => policy.RequireRole("SecurityAdmin", "SuperAdmin"));
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("Admin:SessionTimeoutMinutes", 15));
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
//builder.Services.AddSingleton<Iso8583AsciiBitmapFormatter>();
builder.Services.AddSingleton<BankSwitch.Infrastructure.Iso8583AsciiBitmapFormatter>();
builder.Services.AddSingleton<IAuditLogger, StructuredAuditLogger>();
builder.Services.AddSingleton<ISensitiveDataProtector>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var mode = configuration["SensitiveData:Mode"] ?? "AesGcm";  // CD-03 FIX: was "Development" — now defaults to AesGcm
    if (string.Equals(mode, "Development", StringComparison.OrdinalIgnoreCase))
    {
        var env = builder.Environment.EnvironmentName;
        if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "SECURITY: SensitiveData:Mode=Development must never be used in a Production environment. " +
                "Configure SensitiveData:Mode=AesGcm and supply CardDataEncryptionKey via a secrets vault.");
    }
    return string.Equals(mode, "Development", StringComparison.OrdinalIgnoreCase)
        ? new DevelopmentSensitiveDataProtector()
        : new AesGcmSensitiveDataProtector(configuration, sp.GetRequiredService<ISecretProvider>());
});
builder.Services.AddSingleton<ICardNumberGenerator, SecureCardNumberGenerator>();
builder.Services.AddSingleton<FeeCalculator>();
builder.Services.AddSingleton<ICorePrepaidCmsService, CorePrepaidCmsService>();
builder.Services.AddSingleton<IOperationalControlService, OperationalControlService>();
builder.Services.AddSingleton<IFinancialOperationsService, FinancialOperationsService>();
builder.Services.AddSingleton<ISettlementPositionRepository, InMemorySettlementPositionRepository>();

// ---------------------------------------------------------------
// B4: Financial Processing capabilities
// ---------------------------------------------------------------
// Chart of Accounts + GL Account Balances
builder.Services.AddSingleton<IGlAccountRepository, InMemoryGlAccountRepository>();
builder.Services.AddSingleton<IGlAccountService, GlAccountService>();
// GL Period management
builder.Services.AddSingleton<IGlPeriodRepository, InMemoryGlPeriodRepository>();
// Immutable ledger hash chain
builder.Services.AddSingleton<ILedgerIntegrityService, LedgerIntegrityService>();
// Enhanced Journal Engine (hash chain + balance updates + period validation)
builder.Services.AddSingleton<IJournalEngine, JournalEngine>();
// End-of-Day processing
builder.Services.AddSingleton<IEndOfDayService, EndOfDayService>();
// GL Export (trial balance, journals to CSV/ISO 20022)
builder.Services.AddSingleton<IGlExportService, GlExportService>();
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
builder.Services.AddSingleton<IEftRailService, EftRailService>();

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
// V31/V44.5: ATM Driving - production NDC/NDC+ codec/session engine plus DDC/XFS/APTRA boundaries.
builder.Services.AddSingleton<IAtmDrivingRepository, InMemoryAtmDrivingRepository>();
builder.Services.AddSingleton<INdcMacProvider>(_ =>
{
    var keyHex = builder.Configuration["Ndc:MacKeyHex"];
    if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(keyHex))
        throw new InvalidOperationException("Production NDC/NDC+ requires Ndc:MacKeyHex from a protected secret source or a bank-certified HSM MAC provider.");
    return new HmacNdcMacProvider(keyHex);
});
builder.Services.AddSingleton<NdcProtocolCodec>();
builder.Services.AddSingleton<INdcProtocolEngine, NdcProtocolEngine>();
builder.Services.AddSingleton<INdcLodParser, NdcLodParser>();
builder.Services.AddSingleton<INdcLodManagementService, NdcLodManagementService>();
builder.Services.AddSingleton<IAtmProtocolDriver, NdcProtocolDriver>();
builder.Services.AddSingleton<IAtmProtocolDriver, NdcPlusProtocolDriver>();
builder.Services.AddSingleton<IAtmProtocolDriver, DdcProtocolDriver>();
builder.Services.AddSingleton<IAtmProtocolDriver, XfsProtocolDriver>();
builder.Services.AddSingleton<IAtmProtocolDriver, AptraProtocolDriver>();
builder.Services.AddSingleton<IAtmDrivingService, AtmDrivingService>();
// V32: POS / mPOS / e-Commerce Terminal Driving - Verifone, Ingenico, Gemalto, mPOS, key download, contactless, tip, Cash@POS and merchant settlement
builder.Services.AddSingleton<IPosTerminalDrivingRepository, InMemoryPosTerminalDrivingRepository>();
builder.Services.AddSingleton<IPosProtocolDriver, VerifoneProtocolDriver>();
builder.Services.AddSingleton<IPosProtocolDriver, IngenicoProtocolDriver>();
builder.Services.AddSingleton<IPosProtocolDriver, GemaltoProtocolDriver>();
builder.Services.AddSingleton<IPosProtocolDriver, Iso8583PosProtocolDriver>();
builder.Services.AddSingleton<IPosProtocolDriver, JsonApiPosProtocolDriver>();
builder.Services.AddSingleton<IPosProtocolDriver, SoftPosSdkProtocolDriver>();
builder.Services.AddSingleton<IPosTerminalDrivingService, PosTerminalDrivingService>();
// V33: POS Acquiring Production Core - merchant onboarding, persistent POS repository, MDR/interchange, offline clearing, key ceremony and EMV evidence
builder.Services.AddSingleton<IPosAcquiringProductionService, PosAcquiringProductionService>();
// V34: Card Network Acquiring Certification Simulator - Visa, Mastercard, RuPay/NPCI simulators and evidence reporting
builder.Services.AddSingleton<IAcquiringSchemeSimulator, VisaAcquiringSimulator>();
builder.Services.AddSingleton<IAcquiringSchemeSimulator, MastercardAcquiringSimulator>();
builder.Services.AddSingleton<IAcquiringSchemeSimulator, RupayAcquiringSimulator>();
builder.Services.AddSingleton<IAcquiringSchemeSimulator, NpciNfsAcquiringSimulator>();
builder.Services.AddSingleton<IAcquiringCertificationService, AcquiringCertificationService>();
builder.Services.AddSingleton<IAcquiringCertificationLabService, AcquiringCertificationLabService>();

// V36: Issuer Certification Simulator & Host Validation Lab - completes full issuer + acquirer payment certification lab
builder.Services.AddSingleton<IIssuerSchemeSimulator, VisaIssuerSimulator>();
builder.Services.AddSingleton<IIssuerSchemeSimulator, MastercardIssuerSimulator>();
builder.Services.AddSingleton<IIssuerSchemeSimulator, RupayIssuerSimulator>();
builder.Services.AddSingleton<IIssuerSchemeSimulator, NpciNfsIssuerSimulator>();
builder.Services.AddSingleton<IIssuerSchemeSimulator, ProprietaryIssuerSimulator>();
builder.Services.AddSingleton<IIssuerCertificationService, IssuerCertificationService>();
builder.Services.AddSingleton(new ClearingEngineOptions
{
    CutOffTime = TimeSpan.Parse(builder.Configuration["Clearing:CutOffTimeUtc"] ?? "22:00:00"),
    WorkerIntervalMinutes = builder.Configuration.GetValue("Clearing:WorkerIntervalMinutes", 15),
    DefaultCurrencyCode = builder.Configuration["Clearing:DefaultCurrencyCode"] ?? "566",
    ClearingOutputDirectory = builder.Configuration["Clearing:OutputDirectory"] ?? "./clearing-files"
});
builder.Services.AddSingleton<IClearingEngineService, ClearingEngineService>();
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
// V37: Real HSM & Key Management Production Core - HSM vendor adapters, key lifecycle, TR-31/TR-34, DUKPT/UKPT, PIN/CVV/EMV workflows
builder.Services.AddSingleton<IHsmKeyManagementRepository, InMemoryHsmKeyManagementRepository>();
builder.Services.AddSingleton<IHsmCommandAdapter, ThalesPayShieldAdapter>();
builder.Services.AddSingleton<IHsmCommandAdapter, AtallaHsmAdapter>();
builder.Services.AddSingleton<IHsmCommandAdapter, FuturexHsmAdapter>();
builder.Services.AddSingleton<IHsmCommandAdapter, SimulatorHsmAdapter>();
builder.Services.AddSingleton<IHsmKeyManagementService, HsmKeyManagementService>();

// V38: Real Card Network Host Integration Core - Visa Base I/II, Mastercard MIP/IPM/File Express, RuPay/NPCI adapters, network profiles, SAF replay, cutover calendars
builder.Services.AddSingleton<INetworkHostIntegrationRepository, InMemoryNetworkHostIntegrationRepository>();
builder.Services.AddSingleton<INetworkHostAdapter, VisaBaseIAdapter>();
builder.Services.AddSingleton<INetworkHostAdapter, MastercardMipAdapter>();
builder.Services.AddSingleton<INetworkHostAdapter, RupayNpciNfsAdapter>();
builder.Services.AddSingleton<INetworkHostAdapter, NpciNfsAdapter>();
builder.Services.AddSingleton<INetworkHostAdapter, GenericSchemeHostAdapter>();
builder.Services.AddSingleton<INetworkHostIntegrationService, NetworkHostIntegrationService>();

// V40: Operations Command Center & SLA Automation Core - 24x7 dashboards, incidents, SLA breach detection, escalation, RCA, DR drill evidence and regulatory uptime reporting
builder.Services.AddSingleton<IOperationsCommandCenterRepository, InMemoryOperationsCommandCenterRepository>();
builder.Services.AddSingleton<IOperationsCommandCenterService, OperationsCommandCenterService>();

// V41: Regulatory Compliance, Audit & Evidence Pack Core - RBI DPSC, PCI, ISO, audit observations, VAPT/AppSec findings, SDLC evidence, access reviews and retention.
builder.Services.AddSingleton<IComplianceEvidenceRepository, InMemoryComplianceEvidenceRepository>();
builder.Services.AddSingleton<IComplianceEvidenceService, ComplianceEvidenceService>();
// V42: Real-time fraud risk and AML production core
builder.Services.AddSingleton<IRiskFraudAmlRepository, InMemoryRiskFraudAmlRepository>();
builder.Services.AddSingleton<IRiskFraudAmlProductionService, RiskFraudAmlProductionService>();

// V39: Core Banking & Enterprise Integration Production Core - CBS/Finacle, ESB/API Manager, Payment Hub/IPH, ACS/3DS, FRM, DWH/BI, notifications
builder.Services.AddSingleton<IEnterpriseIntegrationRepository, InMemoryEnterpriseIntegrationRepository>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, FinacleCbsAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, GenericCbsAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, EsbApiManagerAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, PaymentHubAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, IphAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, Acs3DsAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, FrmAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, SmsAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, EmailAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, WhatsappAdapter>();
builder.Services.AddSingleton<IEnterpriseConnectorAdapter, IvrsAdapter>();
builder.Services.AddSingleton<IEnterpriseIntegrationService, EnterpriseIntegrationService>();
builder.Services.AddSingleton<IKycProviderClient, StubKycProviderClient>();
builder.Services.AddSingleton<ICardLifecycleService, CardLifecycleService>();
builder.Services.AddSingleton<IDebitCardProductionRepository, InMemoryDebitCardProductionRepository>();
builder.Services.AddSingleton<IPersonalizationBureauClient, StubPersonalizationBureauClient>();
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Visa));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Mastercard));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.RuPay));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Nfs));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Amex));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Discover));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Jcb));
builder.Services.AddSingleton<ICardNetworkHotlistClient>(new StubCardNetworkHotlistClient(DebitCardNetwork.Diners));
builder.Services.AddSingleton<IDebitCardProductionService, DebitCardProductionService>();
builder.Services.AddSingleton(new TokenizationOptions
{
    TokenBinPrefix = builder.Configuration["Tokenization:TokenBinPrefix"] ?? "999999",
    TokenLength = builder.Configuration.GetValue("Tokenization:TokenLength", 16)
});
builder.Services.AddSingleton<ITokenizationService, TokenizationService>();
builder.Services.AddSingleton<ITerminalKeyService, TerminalKeyService>();
builder.Services.AddSingleton<ICardFeeService, CardFeeService>();
builder.Services.AddSingleton<IAdminAuthService, InMemoryAdminAuthService>();
builder.Services.AddSingleton<ISwitchConfigurationService, SwitchConfigurationService>();

// ---------------------------------------------------------------
// B5: Performance capabilities
// ---------------------------------------------------------------
var dbPerfOpts = new DatabasePerformanceOptions
{
    MinPoolSize               = builder.Configuration.GetValue("Database:MinPoolSize", 10),
    MaxPoolSize               = builder.Configuration.GetValue("Database:MaxPoolSize", 200),
    ConnectionTimeoutSeconds  = builder.Configuration.GetValue("Database:ConnectionTimeoutSeconds", 5),
    CommandTimeoutSeconds     = builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 30),
    SlowConnectionThresholdMs = builder.Configuration.GetValue("Database:SlowConnectionThresholdMs", 100)
};
builder.Services.AddSingleton(dbPerfOpts);
builder.Services.AddSingleton<RedisConnectionFactory>();
builder.Services.AddSingleton<IDistributedCacheService>(sp =>
{
    var redis = sp.GetRequiredService<RedisConnectionFactory>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisDistributedCacheService>>();
    return redis.IsAvailable
        ? new RedisDistributedCacheService(redis, logger)
        : (IDistributedCacheService)new InMemoryDistributedCacheService();
});
builder.Services.AddSingleton<IRedisReplayCache, RedisReplayCache>();
builder.Services.AddSingleton<IDistributedIdempotencyStore, RedisDistributedIdempotencyStore>();
builder.Services.AddSingleton<ITpsLoadTestHarness, TpsLoadTestHarness>();
builder.Services.AddSingleton<IClusterCoordinator, ActiveActiveCoordinator>();
builder.Services.AddSingleton<IOutboxPublisher, InMemoryOutboxPublisher>();

// ---------------------------------------------------------------
// B3: Security capabilities
// ---------------------------------------------------------------
// TOTP / MFA (RFC 6238) — replaces TotpValidator.IsValidDevelopmentCode stub
builder.Services.AddSingleton<ITotpEnrollmentRepository, InMemoryTotpEnrollmentRepository>();
builder.Services.AddSingleton<ITotpService, TotpService>();
// PCI DSS v4.0 compliance scanning
builder.Services.AddSingleton<IPciControlResultRepository, InMemoryPciControlResultRepository>();
builder.Services.AddSingleton<IPciComplianceService, PciDssComplianceService>();

// B7 — AML Integration (external feeds, re-screening, CTR/SAR)
builder.Services.AddSingleton<IAmlReportRepository, InMemoryAmlReportRepository>();
builder.Services.AddSingleton<IAmlIntegrationService, AmlIntegrationService>();
builder.Services.AddHostedService<AmlRescreeningWorker>();

// B7 — Fraud Detection Engine (behavioral baseline + 7-rule pipeline)
builder.Services.AddSingleton<ICardBaselineRepository, InMemoryCardBaselineRepository>();
builder.Services.AddSingleton<IFraudRulesEngine, FraudRulesEngine>();

// B7 — OWASP ASVS Verification (18 L1/L2 controls)
builder.Services.AddSingleton<IOwaspResultRepository, InMemoryOwaspResultRepository>();
builder.Services.AddSingleton<IOwaspVerificationService, OwaspVerificationService>();

// B7 — ISO 27001 Readiness (risk register + SoA)
builder.Services.AddSingleton<IIso27001Repository, InMemoryIso27001Repository>();
builder.Services.AddSingleton<IIso27001Service, Iso27001Service>();

// B7 — Full KYC Workflow (state machine + AML cross-check + re-KYC)
builder.Services.AddSingleton<IKycWorkflowService, KycWorkflowService>();
builder.Services.AddHostedService<KycRescreeningWorker>();

// B7 — Audit Evidence Generation (tamper-evident compliance packages)
builder.Services.AddSingleton<IAuditEvidenceService>(sp => new AuditEvidenceService(
    sp.GetRequiredService<IPciComplianceService>(),
    sp.GetRequiredService<IOwaspVerificationService>(),
    sp.GetRequiredService<IIso27001Service>(),
    sp.GetRequiredService<IAmlReportRepository>(),
    sp.GetRequiredService<IEnterpriseProductionRepository>(),
    //sp.GetService<IJournalEngine>(),
    sp.GetService<ILedgerIntegrityService>(),
    sp.GetRequiredService<IClock>()));

// HSM lifecycle (partition health, key load ceremony audit trail)
builder.Services.AddSingleton<IHsmLifecycleRepository, InMemoryHsmLifecycleRepository>();
builder.Services.AddSingleton<IHsmLifecycleService, HsmLifecycleService>();
// DUKPT full lifecycle (ANSI X9.24-1)
builder.Services.AddSingleton<IDukptKeyStateRepository, InMemoryDukptKeyStateRepository>();
builder.Services.AddSingleton<IDukptKeyService, DukptKeyService>();
// Key rotation scheduler
builder.Services.AddSingleton<IKeyRotationScheduler, KeyRotationScheduler>();
// PAN encryption validation
builder.Services.AddSingleton<IPanEncryptionValidator, PanEncryptionValidator>();
// Secrets vault provider (config-backed for dev; swap for AzureKeyVaultSecretProvider in production)
builder.Services.AddSingleton<ISecretVaultProvider>(sp =>
{
    //var vaultType = configuration["Secrets:VaultType"] ?? "Configuration";
    var vaultType = builder.Configuration["Secrets:VaultType"] ?? "Configuration";
    var audit = sp.GetRequiredService<IAuditLogger>();
    return vaultType switch
    {
        "AzureKeyVault" => new AzureKeyVaultSecretProvider(builder.Configuration["Secrets:AzureKeyVaultUri"] ?? string.Empty, audit, builder.Configuration),
        "HashiCorpVault" => new HashiCorpVaultSecretProvider(builder.Configuration["Secrets:VaultAddress"] ?? string.Empty, builder.Configuration["Secrets:VaultMount"] ?? "bankswitch", null, audit, builder.Configuration),
        _ => new ConfigurationSecretVaultProvider(builder.Configuration)  // dev/test fallback
        // "AzureKeyVault" => new AzureKeyVaultSecretProvider(configuration["Secrets:AzureKeyVaultUri"] ?? string.Empty, audit, configuration),
        // "HashiCorpVault" => new HashiCorpVaultSecretProvider(configuration["Secrets:VaultAddress"] ?? string.Empty, configuration["Secrets:VaultMount"] ?? "bankswitch", null, audit, configuration),
        // _ => new ConfigurationSecretVaultProvider(configuration)  // dev/test fallback
    };
});

// ---------------------------------------------------------------
// A4: Domain event bus and handlers
// ---------------------------------------------------------------
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
// GL posting handlers — react to financial events via event bus
builder.Services.AddSingleton<IEventHandler<AuthorizationApprovedEvent>, AuthorizationApprovedGlHandler>();
builder.Services.AddSingleton<IEventHandler<WalletTopUpCompletedEvent>, WalletTopUpGlHandler>();
// Audit handler for all key events
builder.Services.AddSingleton<IEventHandler<CardIssuedEvent>, AuditDomainEventHandler<CardIssuedEvent>>();
builder.Services.AddSingleton<IEventHandler<CardActivatedEvent>, AuditDomainEventHandler<CardActivatedEvent>>();
builder.Services.AddSingleton<IEventHandler<CardBlockedEvent>, AuditDomainEventHandler<CardBlockedEvent>>();
builder.Services.AddSingleton<IEventHandler<CardUnblockedEvent>, AuditDomainEventHandler<CardUnblockedEvent>>();
builder.Services.AddSingleton<IEventHandler<CardReplacedEvent>, AuditDomainEventHandler<CardReplacedEvent>>();
builder.Services.AddSingleton<IEventHandler<KycDocumentSubmittedEvent>, AuditDomainEventHandler<KycDocumentSubmittedEvent>>();
builder.Services.AddSingleton<IEventHandler<KycDocumentVerifiedEvent>, AuditDomainEventHandler<KycDocumentVerifiedEvent>>();
builder.Services.AddSingleton<IEventHandler<CustomerKycStatusUpdatedEvent>, AuditDomainEventHandler<CustomerKycStatusUpdatedEvent>>();
builder.Services.AddSingleton<IEventHandler<EftTransferInitiatedEvent>, AuditDomainEventHandler<EftTransferInitiatedEvent>>();
builder.Services.AddSingleton<IEventHandler<EftTransferSettledEvent>, AuditDomainEventHandler<EftTransferSettledEvent>>();

// ---------------------------------------------------------------
// A3 Monitoring: metrics, alerting, SIEM, health checks
// ---------------------------------------------------------------
var telemetryOptions = builder.Configuration.GetSection("Telemetry").Get<TelemetryOptions>() ?? new TelemetryOptions();
builder.Services.AddSingleton(telemetryOptions);
builder.Services.AddSingleton<BankSwitchInstrumentation>();
builder.Services.AddSingleton<IMetricCollector>(sp => new RingBufferMetricCollector(telemetryOptions.MetricRingBufferCapacity));
builder.Services.AddSingleton<ISlaMetrics, InstrumentedSlaMetrics>();

// Use in-memory transaction queue for Admin (Engine has the real bounded channel)
builder.Services.AddSingleton<ITransactionQueue>(sp => new BoundedTransactionQueue(32));

var alertingOptions = builder.Configuration.GetSection("Alerting").Get<AlertingOptions>() ?? new AlertingOptions();
builder.Services.AddSingleton(alertingOptions);
builder.Services.AddSingleton<IAlertingRepository, InMemoryAlertingRepository>();

// SIEM forwarder: select implementation based on config
builder.Services.AddSingleton<ISiemForwarder>(sp =>
{
    var cfg = builder.Configuration;
    var siemMode = cfg["Siem:Mode"] ?? "LogOnly";
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
    return siemMode switch
    {
        "SplunkHec" => (ISiemForwarder)new SplunkHecSiemForwarder(
            cfg["Siem:SplunkHecUrl"] ?? throw new InvalidOperationException("Siem:SplunkHecUrl is required when Siem:Mode=SplunkHec"),
            cfg["Siem:SplunkHecToken"] ?? throw new InvalidOperationException("Siem:SplunkHecToken is required"),
            logger.CreateLogger<SplunkHecSiemForwarder>()),
        "Sentinel" => new SentinelSiemForwarder(
            cfg["Siem:SentinelWorkspaceId"] ?? throw new InvalidOperationException("Siem:SentinelWorkspaceId is required"),
            cfg["Siem:SentinelSharedKey"] ?? throw new InvalidOperationException("Siem:SentinelSharedKey is required"),
            cfg["Siem:SentinelLogType"] ?? "BankSwitchSecurity",
            logger.CreateLogger<SentinelSiemForwarder>()),
        "Webhook" => new WebhookSiemForwarder(
            cfg["Siem:WebhookUrl"] ?? throw new InvalidOperationException("Siem:WebhookUrl is required"),
            cfg["Siem:WebhookAuthHeader"],
            logger.CreateLogger<WebhookSiemForwarder>()),
        _ => new LogOnlySiemForwarder(logger.CreateLogger<LogOnlySiemForwarder>())
    };
});

builder.Services.AddSingleton<IAlertingService, AlertingService>();
builder.Services.AddSingleton<IMonitoringService, MonitoringService>();

// ASP.NET Core health checks
builder.Services.AddHealthChecks()
    .AddCheck<ClusterHeartbeatHealthCheck>("cluster_heartbeat", tags: new[] { "ready", "live" })
    .AddCheck<QueueDepthHealthCheck>("queue_depth", tags: new[] { "ready", "live" });
// SQL and HSM health checks added only in SqlServer mode (below in DI branch)

var repositoryProvider = builder.Configuration["Repository:Provider"] ?? "InMemory";
if (builder.Environment.IsProduction() && !string.Equals(repositoryProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Production requires Repository:Provider=SqlServer. In-memory repositories are permitted only for development, tests, and explicit simulation environments.");
}

if (string.Equals(repositoryProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    //builder.Services.AddSingleton<SecureSqlConnectionFactory>();
    builder.Services.AddSingleton<SecurePostgresConnectionFactory>();
    builder.Services.AddSingleton<SqlSwitchRepository>();
    // Add SQL and HSM health checks only when using the SQL provider
    builder.Services.AddHealthChecks()
        .AddCheck<SqlHealthCheck>("sql_connectivity", tags: new[] { "ready", "db" })
        .AddCheck<HsmHealthCheck>("hsm_connectivity", tags: new[] { "ready", "hsm" });
    builder.Services.AddSingleton<SqlPrepaidCmsRepository>();
    builder.Services.AddSingleton<SqlOperationalControlRepository>();
    builder.Services.AddSingleton<SqlFinancialOperationsRepository>();
    builder.Services.AddSingleton<SqlEnterpriseProductionRepository>();
    builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<IRouteRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<ISwitchConfigurationRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<ITransactionReportRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<INodeRepository>(sp => sp.GetRequiredService<SqlSwitchRepository>());
    builder.Services.AddSingleton<ITokenizationRepository, SqlTokenizationRepository>();
    builder.Services.AddSingleton<ITerminalKeyRepository, SqlTerminalKeyRepository>();
    builder.Services.AddSingleton<ICardFeeRepository, SqlCardFeeRepository>();
    builder.Services.AddSingleton<IKycRepository, SqlKycRepository>();
    builder.Services.AddSingleton<ICmsRepository>(sp => sp.GetRequiredService<SqlPrepaidCmsRepository>());
    builder.Services.AddSingleton<IOperationalControlRepository>(sp => sp.GetRequiredService<SqlOperationalControlRepository>());
    builder.Services.AddSingleton<IFinancialOperationsRepository>(sp => sp.GetRequiredService<SqlFinancialOperationsRepository>());
    builder.Services.AddSingleton<IEnterpriseProductionRepository>(sp => sp.GetRequiredService<SqlEnterpriseProductionRepository>());
    builder.Services.AddSingleton<IPosTerminalDrivingRepository, SqlPosTerminalDrivingRepository>();
    builder.Services.AddSingleton<INdcProtocolRepository, SqlNdcProtocolRepository>();
    builder.Services.AddSingleton<INdcLodRepository, SqlNdcLodRepository>();
    builder.Services.AddSingleton<ITransactionRecoverySnapshotRepository, SqlTransactionRecoverySnapshotRepository>();
    builder.Services.AddSingleton<IPosAcquiringProductionRepository, SqlPosAcquiringProductionRepository>();
    builder.Services.AddSingleton<IAcquiringCertificationRepository, SqlAcquiringCertificationRepository>();
    builder.Services.AddSingleton<IAcquiringCertificationLabRepository, SqlAcquiringCertificationLabRepository>();
    builder.Services.AddSingleton<IIssuerCertificationRepository, SqlIssuerCertificationRepository>();
    builder.Services.AddSingleton<IMakerCheckerService, SqlMakerCheckerService>();
    builder.Services.AddSingleton<IEnterpriseConfigurationRepository, SqlEnterpriseConfigurationRepository>();
    builder.Services.AddSingleton<IConfigurationRuntimeProbe, ConfigurationRuntimeProbe>();
}
else
{
    builder.Services.AddSingleton<IMakerCheckerService, InMemoryMakerCheckerService>();
    builder.Services.AddSingleton<InMemorySwitchStore>();
    builder.Services.AddSingleton<InMemoryPrepaidCmsRepository>();
    builder.Services.AddSingleton<InMemoryOperationalControlRepository>();
    builder.Services.AddSingleton<InMemoryFinancialOperationsRepository>();
    builder.Services.AddSingleton<InMemoryEnterpriseProductionRepository>();
    builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<IRouteRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<ISwitchConfigurationRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<ITransactionReportRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<INodeRepository>(sp => sp.GetRequiredService<InMemorySwitchStore>());
    builder.Services.AddSingleton<ITokenizationRepository, InMemoryTokenizationRepository>();
    builder.Services.AddSingleton<ITerminalKeyRepository, InMemoryTerminalKeyRepository>();
    builder.Services.AddSingleton<INdcProtocolRepository, InMemoryNdcProtocolRepository>();
    builder.Services.AddSingleton<INdcLodRepository, InMemoryNdcLodRepository>();
    builder.Services.AddSingleton<ITransactionRecoverySnapshotRepository, InMemoryTransactionRecoverySnapshotRepository>();
    builder.Services.AddSingleton<ICardFeeRepository, InMemoryCardFeeRepository>();
    builder.Services.AddSingleton<IKycRepository, InMemoryKycRepository>();
    builder.Services.AddSingleton<ICmsRepository>(sp => sp.GetRequiredService<InMemoryPrepaidCmsRepository>());
    builder.Services.AddSingleton<IOperationalControlRepository>(sp => sp.GetRequiredService<InMemoryOperationalControlRepository>());
    builder.Services.AddSingleton<IFinancialOperationsRepository>(sp => sp.GetRequiredService<InMemoryFinancialOperationsRepository>());
    builder.Services.AddSingleton<IEnterpriseProductionRepository>(sp => sp.GetRequiredService<InMemoryEnterpriseProductionRepository>());
    builder.Services.AddSingleton<IPosAcquiringProductionRepository, InMemoryPosAcquiringProductionRepository>();
    builder.Services.AddSingleton<IAcquiringCertificationRepository, InMemoryAcquiringCertificationRepository>();
    builder.Services.AddSingleton<IAcquiringCertificationLabRepository, InMemoryAcquiringCertificationLabRepository>();
    builder.Services.AddSingleton<IIssuerCertificationRepository, InMemoryIssuerCertificationRepository>();
    builder.Services.AddSingleton<IEnterpriseConfigurationRepository, InMemoryEnterpriseConfigurationRepository>();
    builder.Services.AddSingleton<IConfigurationRuntimeProbe, InMemoryConfigurationRuntimeProbe>();
}

builder.Services.AddSingleton<IConfigurationRuntimeApplicator, EnterpriseConfigurationRuntimeApplicator>();
builder.Services.AddSingleton<IConfigurationCompletenessService, EnterpriseConfigurationCompletenessService>();
builder.Services.AddSingleton<IEnterpriseConfigurationControlPlane, EnterpriseConfigurationControlPlane>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

// B7 — OWASP security headers (CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy)
app.UseOwaspSecurityHeaders();
// B7 — API rate limiting (5 req/min auth, 30 req/min compliance, 200 req/min default)
app.UseApiRateLimiting();

app.UseRouting();
app.UseIpAllowList(builder.Configuration);
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<CommandCenterHub>("/hubs/command-center").RequireAuthorization("Viewer");
app.MapCorePrepaidCmsEndpoints();
app.MapOperationalControlEndpoints();
app.MapFinancialOperationsEndpoints();
app.MapEnterpriseProductionEndpoints();
app.MapSecurityEndpoints();
app.MapB3SecurityEndpoints();
app.MapB5PerformanceEndpoints();
app.MapCardFeeEndpoints();
app.MapCardLifecycleEndpoints();
app.MapDebitCardProductionEndpoints();
app.MapAtmDrivingEndpoints();
app.MapPosTerminalDrivingEndpoints();
app.MapPosAcquiringProductionEndpoints();
app.MapAcquiringCertificationEndpoints();
app.MapAcquiringCertificationLabEndpoints();
app.MapIssuerCertificationEndpoints();
app.MapHsmKeyManagementEndpoints();
app.MapNetworkHostIntegrationEndpoints();
app.MapEnterpriseIntegrationEndpoints();
app.MapOperationsCommandCenterEndpoints();
app.MapComplianceEvidenceEndpoints();
app.MapRiskFraudAmlEndpoints();
app.MapMonitoringEndpoints();
app.MapB2Endpoints();
app.MapComplianceEndpoints(); // B7 — AML, Fraud, OWASP, ISO27001, KYC Workflow, Audit Evidence
app.MapEnterpriseConfigurationEndpoints(); // v44.1 — enterprise settings/configuration control plane
app.MapCommandCenterEndpoints(); // v44.2C — HTML5/ES6 enterprise command center facade

// CD-04 GUARD: Reject startup if security bypass modes are active in Production
var hsmMode = app.Configuration["Hsm:Mode"] ?? "Http";
var sensitiveMode = app.Configuration["SensitiveData:Mode"] ?? "AesGcm";
if (app.Environment.IsProduction())
{
    if (string.Equals(hsmMode, "BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "SECURITY STARTUP GUARD: Hsm:Mode=BypassForDevelopmentOnly must never run in Production. " +
            "All MAC operations would be skipped and any transaction would be accepted unchecked. " +
            "Configure Hsm:Mode=Http and supply Hsm:BaseUrl pointing to your production HSM.");
    if (string.Equals(sensitiveMode, "Development", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "SECURITY STARTUP GUARD: SensitiveData:Mode=Development must never run in Production. " +
            "PAN data would be stored as trivially reversible Base64. Configure AesGcm mode.");
}

// A3 FIX: Proper health check endpoints replacing the previous static string response.
// /health/live  — liveness probe (only basic liveness checks — no external deps)
// /health/ready — readiness probe (includes SQL, HSM, queue depth, cluster heartbeat)
// /health       — full detail (all checks with descriptions, for ops dashboards)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

// B6: Prometheus-compatible /metrics endpoint for scraping by Prometheus or Grafana Cloud
// Exposes BankSwitch OpenTelemetry metrics in Prometheus text exposition format.
app.MapGet("/metrics", async (BankSwitchInstrumentation instrumentation, HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
    var snapshot = instrumentation.CollectSnapshot();
    await ctx.Response.WriteAsync(snapshot).ConfigureAwait(false);
}).AllowAnonymous();

app.Run();

/// <summary>JSON health response writer — returns 200 for Healthy/Degraded, 503 for Unhealthy.</summary>
static async System.Threading.Tasks.Task WriteHealthResponse(
    Microsoft.AspNetCore.Http.HttpContext context,
    Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy ? 503 : 200;
    var result = System.Text.Json.JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        totalDurationMs = (int)report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            durationMs = (int)e.Value.Duration.TotalMilliseconds,
            data = e.Value.Data.Count > 0 ? e.Value.Data : (IReadOnlyDictionary<string, object>?)null
        })
    });
    await context.Response.WriteAsync(result).ConfigureAwait(false);
}
