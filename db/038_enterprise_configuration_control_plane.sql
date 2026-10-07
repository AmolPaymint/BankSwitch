/*
 BankSwitch v44.1 — Enterprise Configuration Control Plane
 Persistent, versioned, maker-checker governed configuration management.
 PostgreSQL version
*/

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

-- ============================================================
-- 1. Configuration Domains
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationdomains
(
    code varchar(64) NOT NULL
        CONSTRAINT pk_configurationdomains PRIMARY KEY,

    name varchar(128) NOT NULL,

    description varchar(512) NOT NULL
        CONSTRAINT df_configdomains_description DEFAULT '',

    displayorder integer NOT NULL
        CONSTRAINT df_configdomains_order DEFAULT 0,

    enabled boolean NOT NULL
        CONSTRAINT df_configdomains_enabled DEFAULT true,

    createdat timestamptz NOT NULL
        CONSTRAINT df_configdomains_created DEFAULT CURRENT_TIMESTAMP
);


-- ============================================================
-- 2. Configuration Definitions
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationdefinitions
(
    id uuid NOT NULL
        CONSTRAINT pk_configurationdefinitions PRIMARY KEY,

    domaincode varchar(64) NOT NULL,

    key varchar(160) NOT NULL,

    displayname varchar(160) NOT NULL,

    description varchar(1024) NOT NULL
        CONSTRAINT df_configdefinitions_description DEFAULT '',

    valuetype varchar(32) NOT NULL,

    defaultvalue text NULL,

    allowedvaluesjson text NULL,

    minimumvalue numeric(28,8) NULL,

    maximumvalue numeric(28,8) NULL,

    sensitivity varchar(32) NOT NULL,

    reloadpolicy varchar(32) NOT NULL,

    requiresapproval boolean NOT NULL,

    issecret boolean NOT NULL
        CONSTRAINT df_configdefinitions_issecret DEFAULT false,

    issensitive boolean NOT NULL
        CONSTRAINT df_configdefinitions_issensitive DEFAULT false,

    productionlocked boolean NOT NULL
        CONSTRAINT df_configdefinitions_prodlocked DEFAULT false,

    validationpattern varchar(1000) NULL,

    validationexpression varchar(2000) NULL,

    displayorder integer NOT NULL
        CONSTRAINT df_configdefinitions_order DEFAULT 0,

    enabled boolean NOT NULL
        CONSTRAINT df_configdefinitions_enabled DEFAULT true,

    createdat timestamptz NOT NULL
        CONSTRAINT df_configdefinitions_created DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_configurationdefinitions_domain
        FOREIGN KEY (domaincode)
        REFERENCES dbo.configurationdomains(code),

    CONSTRAINT uq_configurationdefinitions
        UNIQUE (domaincode, key),

    CONSTRAINT ck_configurationdefinitions_allowedjson
        CHECK (
            allowedvaluesjson IS NULL
            OR (
                allowedvaluesjson ~ '^\s*[\{\[]'
                AND (
                    CASE
                        WHEN allowedvaluesjson ~ '^\s*\['
                            THEN jsonb_typeof(allowedvaluesjson::jsonb) = 'array'
                        WHEN allowedvaluesjson ~ '^\s*\{'
                            THEN jsonb_typeof(allowedvaluesjson::jsonb) = 'object'
                        ELSE false
                    END
                )
            )
        )
);


-- ============================================================
-- 3. Configuration Values
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationvalues
(
    id uuid NOT NULL
        CONSTRAINT pk_configurationvalues PRIMARY KEY,

    definitionid uuid NOT NULL,

    environment varchar(32) NOT NULL,

    institutionscope varchar(128) NOT NULL,

    value text NOT NULL,

    version bigint NOT NULL,

    effectivefrom timestamptz NOT NULL,

    effectiveto timestamptz NULL,

    updatedby varchar(160) NOT NULL,

    updatedat timestamptz NOT NULL,

    rowversion bytea NOT NULL,

    CONSTRAINT fk_configurationvalues_definition
        FOREIGN KEY (definitionid)
        REFERENCES dbo.configurationdefinitions(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_configurationvalues_active
    ON dbo.configurationvalues
    (
        definitionid,
        environment,
        institutionscope
    )
    WHERE effectiveto IS NULL;

CREATE INDEX IF NOT EXISTS ix_configurationvalues_scope
    ON dbo.configurationvalues
    (
        environment,
        institutionscope,
        version DESC
    );


-- ============================================================
-- 4. Configuration Change Requests
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationchangerequests
(
    id uuid NOT NULL
        CONSTRAINT pk_configurationchangerequests PRIMARY KEY,

    correlationid varchar(64) NOT NULL,

    environment varchar(32) NOT NULL,

    institutionscope varchar(128) NOT NULL,

    maker varchar(160) NOT NULL,

    checker varchar(160) NULL,

    reason varchar(1000) NOT NULL,

    ticketreference varchar(128) NOT NULL,

    effectiveat timestamptz NULL,

    state varchar(32) NOT NULL,

    createdat timestamptz NOT NULL,

    updatedat timestamptz NOT NULL,

    submittedat timestamptz NULL,

    approvedat timestamptz NULL,

    appliedat timestamptz NULL,

    rejectionreason varchar(1000) NULL,

    rowversion bytea NOT NULL,

    CONSTRAINT uq_configurationchangerequests_correlation
        UNIQUE (correlationid)
);

CREATE INDEX IF NOT EXISTS ix_configurationchangerequests_state
    ON dbo.configurationchangerequests
    (
        state,
        createdat DESC
    );

CREATE INDEX IF NOT EXISTS ix_configurationchangerequests_scope
    ON dbo.configurationchangerequests
    (
        environment,
        institutionscope,
        createdat DESC
    );


-- ============================================================
-- 5. Configuration Change Items
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationchangeitems
(
    id uuid NOT NULL
        CONSTRAINT pk_configurationchangeitems PRIMARY KEY,

    changerequestid uuid NOT NULL,

    definitionid uuid NOT NULL,

    domaincode varchar(64) NOT NULL,

    key varchar(160) NOT NULL,

    oldvalue text NULL,

    newvalue text NOT NULL,

    issecretreference boolean NOT NULL,

    reloadpolicy varchar(32) NOT NULL,

    CONSTRAINT fk_configurationchangeitems_request
        FOREIGN KEY (changerequestid)
        REFERENCES dbo.configurationchangerequests(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_configurationchangeitems_definition
        FOREIGN KEY (definitionid)
        REFERENCES dbo.configurationdefinitions(id)
);

CREATE INDEX IF NOT EXISTS ix_configurationchangeitems_request
    ON dbo.configurationchangeitems
    (
        changerequestid
    );


-- ============================================================
-- 6. Configuration History
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationhistory
(
    id bigint GENERATED BY DEFAULT AS IDENTITY
        CONSTRAINT pk_configurationhistory PRIMARY KEY,

    version bigint NOT NULL,

    definitionid uuid NOT NULL,

    domaincode varchar(64) NOT NULL,

    key varchar(160) NOT NULL,

    environment varchar(32) NOT NULL,

    institutionscope varchar(128) NOT NULL,

    oldvalue text NULL,

    newvalue text NOT NULL,

    changedby varchar(160) NOT NULL,

    changerequestid uuid NULL,

    changedat timestamptz NOT NULL,

    reason varchar(1000) NOT NULL,

    hash char(64) NOT NULL,

    CONSTRAINT fk_configurationhistory_definition
        FOREIGN KEY (definitionid)
        REFERENCES dbo.configurationdefinitions(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_configurationhistory_version
    ON dbo.configurationhistory
    (
        version
    );

CREATE INDEX IF NOT EXISTS ix_configurationhistory_scope
    ON dbo.configurationhistory
    (
        environment,
        institutionscope,
        changedat DESC
    );

CREATE INDEX IF NOT EXISTS ix_configurationhistory_key
    ON dbo.configurationhistory
    (
        domaincode,
        key,
        changedat DESC
    );


-- ============================================================
-- 7. Configuration Snapshots
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationsnapshots
(
    id uuid NOT NULL
        CONSTRAINT pk_configurationsnapshots PRIMARY KEY,

    name varchar(200) NOT NULL,

    environment varchar(32) NOT NULL,

    institutionscope varchar(128) NOT NULL,

    version bigint NOT NULL,

    checksum char(64) NOT NULL,

    createdby varchar(160) NOT NULL,

    createdat timestamptz NOT NULL,

    payloadjson text NOT NULL,

    CONSTRAINT ck_configurationsnapshots_json
        CHECK (
            payloadjson ~ '^\s*[\{\[]'
            AND jsonb_typeof(payloadjson::jsonb) IN ('object', 'array')
        )
);

CREATE INDEX IF NOT EXISTS ix_configurationsnapshots_scope
    ON dbo.configurationsnapshots
    (
        environment,
        institutionscope,
        createdat DESC
    );


-- ============================================================
-- 8. Configuration Deployments
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.configurationdeployments
(
    id uuid NOT NULL
        CONSTRAINT pk_configurationdeployments PRIMARY KEY,

    changerequestid uuid NOT NULL,

    success boolean NOT NULL,

    status varchar(64) NOT NULL,

    highestreloadpolicy varchar(32) NOT NULL,

    startedat timestamptz NOT NULL,

    completedat timestamptz NOT NULL,

    message varchar(2000) NOT NULL,

    rolledback boolean NOT NULL,

    CONSTRAINT fk_configurationdeployments_request
        FOREIGN KEY (changerequestid)
        REFERENCES dbo.configurationchangerequests(id)
);

CREATE INDEX IF NOT EXISTS ix_configurationdeployments_request
    ON dbo.configurationdeployments
    (
        changerequestid,
        startedat DESC
    );


-- ============================================================
-- 9. Feature Flags
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.featureflags
(
    id uuid NOT NULL
        CONSTRAINT pk_featureflags PRIMARY KEY,

    key varchar(160) NOT NULL,

    description varchar(512) NOT NULL,

    enabled boolean NOT NULL,

    environment varchar(32) NOT NULL,

    institutionscope varchar(128) NOT NULL,

    rolloutpercentage integer NOT NULL,

    effectivefrom timestamptz NULL,

    effectiveto timestamptz NULL,

    updatedby varchar(160) NOT NULL,

    updatedat timestamptz NOT NULL,

    rowversion bytea NOT NULL,

    CONSTRAINT ck_featureflags_rollout
        CHECK (rolloutpercentage BETWEEN 0 AND 100),

    CONSTRAINT uq_featureflags
        UNIQUE (key, environment, institutionscope)
);


-- ============================================================
-- 10. Certificate Inventory
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.certificateinventory
(
    id uuid NOT NULL
        CONSTRAINT pk_certificateinventory PRIMARY KEY,

    name varchar(160) NOT NULL,

    purpose varchar(256) NOT NULL,

    environment varchar(32) NOT NULL,

    subject varchar(512) NOT NULL,

    issuer varchar(512) NOT NULL,

    thumbprint varchar(128) NOT NULL,

    validfrom timestamptz NOT NULL,

    validto timestamptz NOT NULL,

    secretreference varchar(512) NOT NULL,

    status varchar(32) NOT NULL,

    createdat timestamptz NOT NULL
        CONSTRAINT df_certificateinventory_created DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_certificateinventory_expiry
    ON dbo.certificateinventory
    (
        environment,
        validto
    );


-- ============================================================
-- 11. Secret References
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.secretreferences
(
    id uuid NOT NULL
        CONSTRAINT pk_secretreferences PRIMARY KEY,

    name varchar(160) NOT NULL,

    provider varchar(64) NOT NULL,

    reference varchar(512) NOT NULL,

    environment varchar(32) NOT NULL,

    version varchar(128) NOT NULL,

    lastrotatedat timestamptz NULL,

    expiresat timestamptz NULL,

    status varchar(32) NOT NULL,

    createdat timestamptz NOT NULL
        CONSTRAINT df_secretreferences_created DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT uq_secretreferences
        UNIQUE (name, environment)
);


-- ============================================================
-- 12. Seed 34 Configuration Domains
-- ============================================================

INSERT INTO dbo.configurationdomains
(
    code,
    name,
    description,
    displayorder,
    enabled
)
VALUES
(
    'general',
    'General / System',
    'Institution, environment and business-date settings',
    10,
    true
),
(
    'api',
    'API & Backend',
    'API runtime, timeout and retry settings',
    20,
    true
),
(
    'realtime',
    'Realtime / SignalR',
    'Realtime event and reconnect settings',
    30,
    true
),
(
    'database',
    'Database & Repositories',
    'Repository provider and database performance settings',
    40,
    true
),
(
    'transactions',
    'Transaction Processing',
    'Authorization, reversal, SAF and idempotency settings',
    50,
    true
),
(
    'iso8583',
    'ISO 8583',
    'Message profile and field configuration',
    60,
    true
),
(
    'routing',
    'Routing',
    'Advanced switch routing controls',
    70,
    true
),
(
    'network-hosts',
    'Network Hosts',
    'Visa, Mastercard, RuPay and NPCI host controls',
    80,
    true
),
(
    'atm',
    'ATM',
    'ATM protocol and runtime controls',
    90,
    true
),
(
    'pos',
    'POS / mPOS',
    'POS and terminal-driving controls',
    100,
    true
),
(
    'merchant',
    'Merchant Acquiring',
    'Merchant, MDR and settlement controls',
    110,
    true
),
(
    'cards',
    'Card Management',
    'Card product and lifecycle controls',
    120,
    true
),
(
    'hsm',
    'HSM & Key Management',
    'HSM profile, key policy and rotation controls',
    130,
    true
),
(
    'fraud',
    'Fraud / Risk',
    'Fraud rules and scoring thresholds',
    140,
    true
),
(
    'aml',
    'AML',
    'AML, sanctions and PEP screening controls',
    150,
    true
),
(
    'settlement',
    'Settlement',
    'Network and merchant settlement controls',
    160,
    true
),
(
    'gl',
    'GL / Accounting',
    'GL mappings and financial posting controls',
    170,
    true
),
(
    'reconciliation',
    'Reconciliation',
    'Matching, tolerance and exception controls',
    180,
    true
),
(
    'disputes',
    'Disputes / Chargeback',
    'Dispute SLA and evidence controls',
    190,
    true
),
(
    'cbs',
    'CBS / Finacle',
    'Core banking integration controls',
    200,
    true
),
(
    'enterprise',
    'Enterprise Integrations',
    'ESB, ACS, FRM, DWH and notification controls',
    210,
    true
),
(
    'certification',
    'Certification Lab',
    'Simulator, test and evidence controls',
    220,
    true
),
(
    'security',
    'Security',
    'Identity, MFA, session and TLS controls',
    230,
    true
),
(
    'rbac',
    'Users & RBAC',
    'Role and permission control-plane settings',
    240,
    true
),
(
    'maker-checker',
    'Maker / Checker',
    'Four-eyes governance controls',
    250,
    true
),
(
    'audit',
    'Audit',
    'Audit retention and SIEM forwarding controls',
    260,
    true
),
(
    'compliance',
    'Compliance',
    'PCI, RBI, NPCI and ISO evidence controls',
    270,
    true
),
(
    'monitoring',
    'Monitoring & SLA',
    'Health and SLA threshold controls',
    280,
    true
),
(
    'alerts',
    'Alerts',
    'Alert channel and escalation controls',
    290,
    true
),
(
    'observability',
    'Logging & Observability',
    'Logging, metrics and tracing controls',
    300,
    true
),
(
    'dr',
    'Disaster Recovery',
    'RPO, RTO, failover and DR controls',
    310,
    true
),
(
    'retention',
    'Data Retention',
    'Archival and purge controls',
    320,
    true
),
(
    'feature-flags',
    'Feature Flags',
    'Controlled functional rollout settings',
    330,
    true
),
(
    'diagnostics',
    'Diagnostics',
    'Runtime health and diagnostics controls',
    340,
    true
)
ON CONFLICT (code)
DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    displayorder = EXCLUDED.displayorder,
    enabled = true;


-- ============================================================
-- 13. Core Configuration Definitions
-- ============================================================

WITH defs
(
    domaincode,
    key,
    displayname,
    description,
    valuetype,
    defaultvalue,
    allowedvaluesjson,
    minimumvalue,
    maximumvalue,
    sensitivity,
    reloadpolicy,
    requiresapproval,
    issecret,
    issensitive,
    productionlocked,
    displayorder
)
AS
(
    VALUES

    (
        'general',
        'Environment',
        'Environment',
        'DEV/SIT/UAT/PREPROD/PROD/DR',
        'Enum',
        'DEV',
        '["DEV","SIT","UAT","PREPROD","PROD","DR"]',
        NULL,
        NULL,
        'Critical',
        'ClusterRestart',
        true,
        false,
        true,
        true,
        10
    ),

    (
        'general',
        'InstitutionCode',
        'Institution Code',
        'Authoritative institution identifier',
        'String',
        'BANK',
        NULL,
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        false,
        true,
        true,
        20
    ),

    (
        'general',
        'BaseCurrency',
        'Base Currency',
        'ISO numeric/alphabetic base currency',
        'String',
        'INR',
        NULL,
        NULL,
        NULL,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        30
    ),

    (
        'general',
        'TimeZone',
        'Time Zone',
        'Business timezone',
        'String',
        'Asia/Kolkata',
        NULL,
        NULL,
        NULL,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        40
    ),

    (
        'api',
        'RequestTimeoutSeconds',
        'Request Timeout',
        'Backend request timeout in seconds',
        'Integer',
        '30',
        NULL,
        1,
        300,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        10
    ),

    (
        'api',
        'RetryCount',
        'Retry Count',
        'Transient retry count',
        'Integer',
        '3',
        NULL,
        0,
        10,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        20
    ),

    (
        'realtime',
        'SignalREnabled',
        'SignalR Enabled',
        'Enable realtime operational events',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        10
    ),

    (
        'realtime',
        'HeartbeatSeconds',
        'Heartbeat Interval',
        'Realtime heartbeat interval',
        'Integer',
        '15',
        NULL,
        5,
        300,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        20
    ),

    (
        'database',
        'RepositoryProvider',
        'Repository Provider',
        'Persistence provider',
        'Enum',
        'SqlServer',
        '["SqlServer","InMemory"]',
        NULL,
        NULL,
        'Critical',
        'ClusterRestart',
        true,
        false,
        true,
        true,
        10
    ),

    (
        'database',
        'CommandTimeoutSeconds',
        'SQL Command Timeout',
        'SQL command timeout',
        'Integer',
        '30',
        NULL,
        1,
        300,
        'Sensitive',
        'ServiceRestart',
        true,
        false,
        false,
        false,
        20
    ),

    (
        'database',
        'MaxPoolSize',
        'SQL Max Pool Size',
        'Maximum ADO.NET connection pool size',
        'Integer',
        '200',
        NULL,
        10,
        2000,
        'Sensitive',
        'ServiceRestart',
        true,
        false,
        false,
        false,
        30
    ),

    (
        'transactions',
        'AuthorizationTimeoutSeconds',
        'Authorization Timeout',
        'Authorization processing timeout',
        'Integer',
        '30',
        NULL,
        1,
        120,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'transactions',
        'DuplicateWindowSeconds',
        'Duplicate Detection Window',
        'Duplicate transaction protection window',
        'Integer',
        '300',
        NULL,
        1,
        86400,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        20
    ),

    (
        'transactions',
        'SafEnabled',
        'SAF Enabled',
        'Enable store-and-forward',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        30
    ),

    (
        'transactions',
        'AutoReversalEnabled',
        'Auto Reversal',
        'Enable automatic timeout reversal',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        40
    ),

    (
        'iso8583',
        'DefaultProfile',
        'Default ISO Profile',
        'Default network message profile',
        'String',
        'ISO8583-1987',
        NULL,
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'iso8583',
        'MacField',
        'MAC Field',
        'ISO MAC field number',
        'Enum',
        '64',
        '["64","128"]',
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        false,
        false,
        false,
        20
    ),

    (
        'routing',
        'FallbackEnabled',
        'Routing Fallback',
        'Enable fallback route selection',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'network-hosts',
        'TlsEnabled',
        'Network TLS',
        'Require TLS for network hosts',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Critical',
        'ConnectionRestart',
        true,
        false,
        true,
        true,
        10
    ),

    (
        'network-hosts',
        'EchoIntervalSeconds',
        'Echo Interval',
        'Network management echo interval',
        'Integer',
        '30',
        NULL,
        5,
        600,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        20
    ),

    (
        'atm',
        'HeartbeatSeconds',
        'ATM Heartbeat',
        'ATM heartbeat interval',
        'Integer',
        '30',
        NULL,
        5,
        600,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        10
    ),

    (
        'pos',
        'OfflineFloorLimit',
        'Offline Floor Limit',
        'Maximum offline contactless floor limit',
        'Decimal',
        '0',
        NULL,
        0,
        1000000,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'merchant',
        'DefaultSettlementCycle',
        'Settlement Cycle',
        'Default merchant settlement cycle',
        'Enum',
        'T+1',
        '["T+0","T+1","T+2"]',
        NULL,
        NULL,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'cards',
        'PinRetryLimit',
        'PIN Retry Limit',
        'Maximum PIN retry attempts',
        'Integer',
        '3',
        NULL,
        1,
        10,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'hsm',
        'HsmMode',
        'HSM Mode',
        'HSM operating mode',
        'Enum',
        'Http',
        '["Http","Thales","Atalla","Futurex","Mock","BypassForDevelopmentOnly"]',
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        false,
        true,
        true,
        10
    ),

    (
        'hsm',
        'KeyRotationDays',
        'Key Rotation Days',
        'Default key rotation cadence',
        'Integer',
        '90',
        NULL,
        1,
        365,
        'Critical',
        'HotReload',
        true,
        false,
        true,
        false,
        20
    ),

    (
        'fraud',
        'CriticalScoreThreshold',
        'Critical Risk Score',
        'Score at which transaction is critical',
        'Integer',
        '90',
        NULL,
        1,
        100,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'aml',
        'RescreenHours',
        'AML Rescreen Interval',
        'Customer AML rescreen interval',
        'Integer',
        '24',
        NULL,
        1,
        720,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'settlement',
        'CutoffTimeUtc',
        'Settlement Cutoff',
        'Daily settlement cut-off UTC',
        'String',
        '22:00:00',
        NULL,
        NULL,
        NULL,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'gl',
        'BalanceTolerance',
        'Balance Tolerance',
        'Maximum GL imbalance tolerance',
        'Decimal',
        '0',
        NULL,
        0,
        1000,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'reconciliation',
        'AmountTolerance',
        'Amount Tolerance',
        'Automatic reconciliation amount tolerance',
        'Decimal',
        '0',
        NULL,
        0,
        1000,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'disputes',
        'AutoEscalationEnabled',
        'Dispute Escalation',
        'Enable automatic dispute escalation',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'cbs',
        'Endpoint',
        'CBS Endpoint',
        'Primary CBS integration endpoint',
        'Uri',
        'https://cbs.invalid',
        NULL,
        NULL,
        NULL,
        'Critical',
        'ConnectionRestart',
        true,
        false,
        true,
        false,
        10
    ),

    (
        'enterprise',
        'CircuitBreakerThreshold',
        'Circuit Breaker Threshold',
        'Enterprise integration failure threshold',
        'Integer',
        '5',
        NULL,
        1,
        100,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'certification',
        'SimulatorMode',
        'Simulator Mode',
        'Allow scheme simulator execution',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Sensitive',
        'ServiceRestart',
        true,
        false,
        false,
        true,
        10
    ),

    (
        'security',
        'AuthenticationMode',
        'Authentication Mode',
        'Administrative identity mode',
        'Enum',
        'OIDC',
        '["OIDC","AzureAD","Keycloak","Cookie","Disabled"]',
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        false,
        true,
        true,
        10
    ),

    (
        'security',
        'MfaRequired',
        'MFA Required',
        'Require MFA for privileged administration',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Critical',
        'HotReload',
        true,
        false,
        true,
        true,
        20
    ),

    (
        'security',
        'TlsMinimumVersion',
        'TLS Minimum Version',
        'Minimum TLS version',
        'Enum',
        '1.2',
        '["1.2","1.3"]',
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        false,
        true,
        true,
        30
    ),

    (
        'security',
        'AdminClientSecretRef',
        'Admin Client Secret Reference',
        'Vault/HSM reference only; never a secret value',
        'SecretReference',
        NULL,
        NULL,
        NULL,
        NULL,
        'Critical',
        'ServiceRestart',
        true,
        true,
        true,
        false,
        40
    ),

    (
        'maker-checker',
        'CriticalApprovalRequired',
        'Critical Approval',
        'Force maker-checker for critical settings',
        'Boolean',
        'true',
        NULL,
        NULL,
        NULL,
        'Critical',
        'HotReload',
        true,
        false,
        true,
        true,
        10
    ),

    (
        'audit',
        'RetentionDays',
        'Audit Retention',
        'Audit retention days',
        'Integer',
        '2555',
        NULL,
        365,
        3650,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'monitoring',
        'LatencyCriticalMs',
        'Critical Latency',
        'Critical transaction latency threshold',
        'Integer',
        '1000',
        NULL,
        10,
        60000,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'alerts',
        'CertificateExpiryDays',
        'Certificate Warning',
        'Certificate-expiry warning threshold',
        'Integer',
        '30',
        NULL,
        1,
        365,
        'Sensitive',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'observability',
        'LogLevel',
        'Log Level',
        'Minimum structured log level',
        'Enum',
        'Information',
        '["Debug","Information","Warning","Error","Critical"]',
        NULL,
        NULL,
        'Operational',
        'HotReload',
        false,
        false,
        false,
        false,
        10
    ),

    (
        'dr',
        'RpoMinutes',
        'RPO',
        'Recovery point objective in minutes',
        'Integer',
        '5',
        NULL,
        0,
        1440,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ),

    (
        'dr',
        'RtoMinutes',
        'RTO',
        'Recovery time objective in minutes',
        'Integer',
        '30',
        NULL,
        1,
        1440,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        20
    ),

    (
        'retention',
        'TransactionDays',
        'Transaction Retention',
        'Online transaction retention days',
        'Integer',
        '365',
        NULL,
        30,
        3650,
        'Critical',
        'HotReload',
        true,
        false,
        false,
        false,
        10
    ), (
        'diagnostics', 'ConnectionTestEnabled', 'Connection Test', 'Allow privileged live dependency connectivity tests', 
		'Boolean', 'true', NULL, NULL, NULL, 'Sensitive', 'HotReload', true, false, false, false, 10))
		
INSERT INTO dbo.configurationdefinitions(id, domaincode, key, displayname,description,valuetype,defaultvalue, allowedvaluesjson,
	minimumvalue, maximumvalue, sensitivity, reloadpolicy, requiresapproval, issecret, issensitive, productionlocked, displayorder, 
	enabled )
SELECT gen_random_uuid(),domaincode,key, displayname, description, valuetype, defaultvalue, allowedvaluesjson, minimumvalue, 
	maximumvalue, sensitivity, reloadpolicy, requiresapproval, issecret, issensitive, productionlocked, displayorder, true
FROM defs ON CONFLICT (domaincode, key)
DO UPDATE SET displayname = EXCLUDED.displayname, description = EXCLUDED.description, valuetype = EXCLUDED.valuetype, 
	defaultvalue = EXCLUDED.defaultvalue, allowedvaluesjson = EXCLUDED.allowedvaluesjson, minimumvalue = EXCLUDED.minimumvalue,
    maximumvalue = EXCLUDED.maximumvalue, sensitivity = EXCLUDED.sensitivity, reloadpolicy = EXCLUDED.reloadpolicy, 
	requiresapproval = EXCLUDED.requiresapproval, issecret = EXCLUDED.issecret, issensitive = EXCLUDED.issensitive,
    productionlocked = EXCLUDED.productionlocked, displayorder = EXCLUDED.displayorder, enabled = true;


COMMIT;