-- BankSwitch v44.6 SQL Server -> PostgreSQL conversion
-- Target schema: dbo
-- Naming: lowercase table/column/constraint/index identifiers
-- Source business logic is preserved; SQL Server-only storage/session syntax is removed or replaced by PostgreSQL equivalents.

CREATE SCHEMA IF NOT EXISTS dbo;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
SET search_path TO dbo, public;

-- ===== 001_production_schema.sql =====

/* BankSwitch v21 production baseline schema for SQL Server.

   Execute as DBA, then grant app login membership in db_datareader/db_datawriter only for required tables or custom least-privilege role.

*/

create table if not exists dbo.sourcenodes (

    id uuid not null constraint pk_sourcenodes primary key,

    nodeid varchar(64) not null constraint uq_sourcenodes_nodeid unique,

    name varchar(200) not null,

    isactive boolean not null,

    requiremtls boolean not null,

    requireprivatenetwork boolean not null,

    allowedcidrs text not null constraint df_sourcenodes_allowedcidrs default(''),

    certificatethumbprint varchar(128) not null constraint df_sourcenodes_cert default(''),

    tpslimit int not null,

    dailyamountlimit decimal(19,2) not null,

    maxmessagebytes int not null,

    idletimeoutseconds int not null,

    permittedmtis text not null,

    permittedchannels text not null,

    allowedbinranges text not null,

    keyprofile varchar(128) not null,

    settlementprofile varchar(128) not null,

    createdat timestamptz not null constraint df_sourcenodes_createdat default(clock_timestamp()),

    updatedat timestamptz null

);

create table if not exists dbo.sinknodes (

    id uuid not null constraint pk_sinknodes primary key,

    nodeid varchar(64) not null constraint uq_sinknodes_nodeid unique,

    name varchar(200) not null,

    host varchar(255) not null,

    port int not null,

    isactive boolean not null,

    requiremtls boolean not null,

    requireprivatenetwork boolean not null,

    allowedcidrs text not null constraint df_sinknodes_allowedcidrs default(''),

    certificatethumbprint varchar(128) not null constraint df_sinknodes_cert default(''),

    tpslimit int not null,

    dailyamountlimit decimal(19,2) not null,

    maxmessagebytes int not null,

    idletimeoutseconds int not null,

    permittedmtis text not null,

    permittedchannels text not null,

    allowedbinranges text not null,

    keyprofile varchar(128) not null,

    settlementprofile varchar(128) not null,

    createdat timestamptz not null constraint df_sinknodes_createdat default(clock_timestamp()),

    updatedat timestamptz null

);

create table if not exists dbo.routes (

    id uuid not null constraint pk_routes primary key,

    binprefix varchar(12) not null,

    sinknodeid uuid not null constraint fk_routes_sinknodes references dbo.sinknodes(id),

    isactive boolean not null,

    priority int not null constraint df_routes_priority default(0),

    countrycodes text not null constraint df_routes_countrycodes default(''),

    merchantcategorycodes text not null constraint df_routes_mcc default(''),

    currencycodes text not null constraint df_routes_currencycodes default(''),

    devicecodes text not null constraint df_routes_devicecodes default(''),

    interchangecodes text not null constraint df_routes_interchangecodes default(''),

    cardrangeprefixes text not null constraint df_routes_cardrangeprefixes default(''),

    institutioncodes text not null constraint df_routes_institutioncodes default(''),

    productcodes text not null constraint df_routes_productcodes default(''),

    networkcodes text not null constraint df_routes_networkcodes default(''),

    accountranges text not null constraint df_routes_accountranges default(''),

    createdat timestamptz not null constraint df_routes_createdat default(clock_timestamp())

);

create index if not exists ix_routes_advancedlookup on dbo.routes(isactive, priority desc, binprefix);

create table if not exists dbo.fees (

    id uuid not null constraint pk_fees primary key,

    name varchar(200) not null,

    flatamount decimal(19,2) not null,

    percentageoftransaction decimal(9,4) not null,

    minimum decimal(19,2) not null,

    maximum decimal(19,2) not null,

    isactive boolean not null,

    createdat timestamptz not null constraint df_fees_createdat default(clock_timestamp())

);

create table if not exists dbo.schemes (

    id uuid not null constraint pk_schemes primary key,

    name varchar(200) not null,

    sourcenodeid uuid not null,

    routeid uuid not null constraint fk_schemes_routes references dbo.routes(id),

    isactive boolean not null,

    createdat timestamptz not null constraint df_schemes_createdat default(clock_timestamp())

);

create index if not exists ix_schemes_sourceroute on dbo.schemes(sourcenodeid, routeid, isactive);

create table if not exists dbo.schemepermissions (

    schemeid uuid not null constraint fk_schemepermissions_schemes references dbo.schemes(id),

    transactiontypecode varchar(2) not null,

    channelcode varchar(2) not null,

    feeid uuid not null constraint fk_schemepermissions_fees references dbo.fees(id),

    constraint pk_schemepermissions primary key (schemeid, transactiontypecode, channelcode)

);

create table if not exists dbo.transactionlogs (

    id uuid not null constraint pk_transactionlogs primary key,

    correlationid varchar(64) not null,

    mti varchar(4) not null,

    sourcenodeid varchar(64) not null,

    sinknodeid varchar(64) not null,

    maskedpan varchar(32) not null,

    pantoken text not null,

    panhash varchar(128) not null,

    stan varchar(6) not null,

    rrn varchar(12) not null,

    amount decimal(19,2) not null,

    currencycode varchar(3) not null,

    responsecode varchar(2) not null,

    latencymilliseconds bigint not null,

    routeused varchar(50) not null,

    schemeused varchar(200) not null,

    feeapplied varchar(200) not null,

    reversalstate varchar(32) not null,

    macvalidationstatus varchar(50) not null,

    createdat timestamptz not null,

    businessdate date generated always as (createdat::timestamptz) stored

);

create index if not exists ix_transactionlogs_stan on dbo.transactionlogs(stan);

create index if not exists ix_transactionlogs_rrn on dbo.transactionlogs(rrn);

create index if not exists ix_transactionlogs_date on dbo.transactionlogs(createdat);

create index if not exists ix_transactionlogs_sourcedate on dbo.transactionlogs(sourcenodeid, createdat);

create index if not exists ix_transactionlogs_panhash on dbo.transactionlogs(panhash);

create unique index if not exists ux_transactionlogs_duplicate on dbo.transactionlogs(sourcenodeid, stan, rrn, amount, businessdate) where mti in ('0100','0200','0220');

create table if not exists dbo.reversalworkitems (

    originaltransactionid uuid not null constraint pk_reversalworkitems primary key,

    originaldataelement varchar(128) not null,

    reversalmessagebase64 text not null,

    sinknodeid uuid not null constraint fk_reversalworkitems_sinknodes references dbo.sinknodes(id),

    correlationid varchar(64) not null,

    attemptcount int not null,

    state varchar(32) not null,

    nextattemptat timestamptz not null,

    lastattemptat timestamptz null,

    lastresponsecode varchar(2) null,

    lasterror varchar(2048) null,

    createdat timestamptz not null,

    updatedat timestamptz null

);

create unique index if not exists ux_reversalworkitems_acceptedoriginal on dbo.reversalworkitems(originaltransactionid) where state = 'Accepted';

create index if not exists ix_reversalworkitems_due on dbo.reversalworkitems(state, nextattemptat, attemptcount);

-- Development seed data matching the in-memory package.
do $$
declare
    sinkid uuid := gen_random_uuid();
    routeid uuid := gen_random_uuid();
    sourceid uuid := gen_random_uuid();
    feeid uuid := gen_random_uuid();
    schemeid uuid := gen_random_uuid();
begin
    insert into dbo.sinknodes(id, nodeid, name, host, port, isactive, requiremtls, requireprivatenetwork, allowedcidrs, certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds, permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile)

    values(sinkid, 'SNK-DEV-001', 'Development Sink', '127.0.0.1', 5001, true, false, false, '127.0.0.1', '', 100, 0, 4096, 65, '0200;0420', '01', '539983', 'DEV-SINK', 'DEV-SETTLEMENT');

    insert into dbo.sourcenodes(id, nodeid, name, isactive, requiremtls, requireprivatenetwork, allowedcidrs, certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds, permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile)

    values(sourceid, 'SRC-DEV-001', 'Development Source', true, false, false, '127.0.0.1', '', 100, 0, 4096, 65, '0200;0420', '01', '539983', 'DEV-SOURCE', 'DEV-SETTLEMENT');

    insert into dbo.routes(id, binprefix, sinknodeid, isactive) values(routeid, '539983', sinkid, true);

    insert into dbo.fees(id, name, flatamount, percentageoftransaction, minimum, maximum, isactive) values(feeid, 'Development flat fee', 10.00, 0.0000, 0.00, 0.00, true);

    insert into dbo.schemes(id, name, sourcenodeid, routeid, isactive) values(schemeid, 'Development scheme', sourceid, routeid, true);

    insert into dbo.schemepermissions(schemeid, transactiontypecode, channelcode, feeid) values(schemeid, '00', '01', feeid), (schemeid, '20', '01', feeid);

end $$;

create table if not exists dbo.configchangerequests (

    id uuid not null constraint pk_configchangerequests primary key,

    correlationid varchar(64) not null,

    area varchar(64) not null,

    oldvalue text not null,

    newvalue text not null,

    maker varchar(100) not null,

    checker varchar(100) not null constraint df_configchangerequests_checker default(''),

    approvedat timestamptz null,

    effectiveat timestamptz null,

    reason varchar(512) not null,

    ticketreference varchar(100) not null,

    createdat timestamptz not null,

    updatedat timestamptz null,

    state varchar(32) not null

);

create index if not exists ix_configchangerequests_stateeffective on dbo.configchangerequests(state, effectiveat);

create index if not exists ix_configchangerequests_ticket on dbo.configchangerequests(ticketreference);

-- ===== 002_core_prepaid_cms_phase1.sql =====

/*

  Phase 1 Core Prepaid CMS schema.

  Run after db/001_production_schema.sql.

*/

create table if not exists dbo.prepaidprograms

(

    id uuid not null constraint pk_prepaidprograms primary key,

    programcode varchar(50) not null,

    name varchar(200) not null,

    description varchar(1000) not null constraint df_prepaidprograms_description default(''),

    currencycode char(3) not null,

    reloadable boolean not null,

    allowedchannels varchar(400) not null,

    allowedtransactiontypes varchar(400) not null,

    status varchar(30) not null,

    createdat timestamptz not null,

    activatedat timestamptz null,

    constraint ux_prepaidprograms_programcode unique (programcode)

);

create table if not exists dbo.limitprofiles

(

    id uuid not null constraint pk_limitprofiles primary key,

    programid uuid not null,

    productid uuid null,

    name varchar(200) not null,

    kyctier varchar(20) not null,

    maxbalance decimal(19,4) not null,

    pertransactionlimit decimal(19,4) not null,

    dailyloadlimit decimal(19,4) not null,

    monthlyloadlimit decimal(19,4) not null,

    dailyspendlimit decimal(19,4) not null,

    monthlyspendlimit decimal(19,4) not null,

    dailytransactioncountlimit int not null,

    isactive boolean not null,

    constraint fk_limitprofiles_prepaidprograms foreign key (programid) references dbo.prepaidprograms(id)

);

create table if not exists dbo.cardproducts

(

    id uuid not null constraint pk_cardproducts primary key,

    programid uuid not null,

    productcode varchar(50) not null,

    name varchar(200) not null,

    currencycode char(3) not null,

    cardkind varchar(20) not null,

    reloadable boolean not null,

    expiryperiodmonths int not null,

    binprefix varchar(12) not null,

    defaultfeeid uuid null,

    topupfeeid uuid null,

    purchasefeeid uuid null,

    limitprofileid uuid not null,

    allowedchannels varchar(400) not null,

    allowedtransactiontypes varchar(400) not null,

    status varchar(30) not null,

    createdat timestamptz not null,

    constraint ux_cardproducts_productcode unique (productcode),

    constraint fk_cardproducts_prepaidprograms foreign key (programid) references dbo.prepaidprograms(id),

    constraint fk_cardproducts_limitprofiles foreign key (limitprofileid) references dbo.limitprofiles(id)

);

alter table dbo.limitprofiles add constraint fk_limitprofiles_cardproducts foreign key (productid) references dbo.cardproducts(id);

create table if not exists dbo.customers

(

    id uuid not null constraint pk_customers primary key,

    customernumber varchar(50) not null,

    fullname varchar(200) not null,

    mobilenumber varchar(50) not null,

    email varchar(200) not null,

    kyctier varchar(20) not null,

    kycstatus varchar(30) not null,

    status varchar(30) not null,

    riskrating varchar(30) not null,

    createdat timestamptz not null,

    constraint ux_customers_customernumber unique (customernumber)

);

create table if not exists dbo.walletaccounts

(

    id uuid not null constraint pk_walletaccounts primary key,

    accountnumber varchar(30) not null,

    customerid uuid not null,

    productid uuid not null,

    currencycode char(3) not null,

    ledgerbalance decimal(19,4) not null,

    availablebalance decimal(19,4) not null,

    reservedbalance decimal(19,4) not null,

    status varchar(30) not null,

    createdat timestamptz not null,

    rowversion bytea not null,

    constraint ux_walletaccounts_accountnumber unique (accountnumber),

    constraint fk_walletaccounts_customers foreign key (customerid) references dbo.customers(id),

    constraint fk_walletaccounts_cardproducts foreign key (productid) references dbo.cardproducts(id)

);

create table if not exists dbo.prepaidcards

(

    id uuid not null constraint pk_prepaidcards primary key,

    customerid uuid not null,

    productid uuid not null,

    walletaccountid uuid not null,

    cardnumbertoken text not null,

    maskedpan varchar(32) not null,

    panhash varchar(128) not null,

    expirymonth int not null,

    expiryyear int not null,

    cardkind varchar(20) not null,

    status varchar(30) not null,

    createdat timestamptz not null,

    activatedat timestamptz null,

    constraint ux_prepaidcards_panhash unique (panhash),

    constraint fk_prepaidcards_customers foreign key (customerid) references dbo.customers(id),

    constraint fk_prepaidcards_cardproducts foreign key (productid) references dbo.cardproducts(id),

    constraint fk_prepaidcards_walletaccounts foreign key (walletaccountid) references dbo.walletaccounts(id),

    constraint ck_prepaidcards_expirymonth check (expirymonth between 1 and 12)

);

create table if not exists dbo.ledgerentries

(

    id uuid not null constraint pk_ledgerentries primary key,

    walletaccountid uuid not null,

    correlationid varchar(64) not null,

    entrytype varchar(40) not null,

    direction varchar(10) not null,

    amount decimal(19,4) not null,

    currencycode char(3) not null,

    balanceafter decimal(19,4) not null,

    reference varchar(100) not null,

    narrative varchar(500) not null,

    createdat timestamptz not null,

    constraint fk_ledgerentries_walletaccounts foreign key (walletaccountid) references dbo.walletaccounts(id),

    constraint ck_ledgerentries_amount_positive check (amount >= 0)

);

create table if not exists dbo.cmstransactionlogs

(

    id uuid not null constraint pk_cmstransactionlogs primary key,

    correlationid varchar(64) not null,

    cardid uuid null,

    walletaccountid uuid null,

    maskedpan varchar(32) not null,

    panhash varchar(128) not null,

    transactiontypecode varchar(10) not null,

    channelcode varchar(10) not null,

    stan varchar(20) not null,

    rrn varchar(50) not null,

    amount decimal(19,4) not null,

    feeamount decimal(19,4) not null,

    currencycode char(3) not null,

    responsecode varchar(5) not null,

    responsedescription varchar(200) not null,

    authorizationcode varchar(20) not null,

    createdat timestamptz not null,

    constraint fk_cmstransactionlogs_cards foreign key (cardid) references dbo.prepaidcards(id),

    constraint fk_cmstransactionlogs_wallets foreign key (walletaccountid) references dbo.walletaccounts(id)

);

create index if not exists ix_cardproducts_program on dbo.cardproducts(programid, status);

create index if not exists ix_customers_status on dbo.customers(status, kycstatus);

create index if not exists ix_walletaccounts_customer on dbo.walletaccounts(customerid);

create index if not exists ix_prepaidcards_customerstatus on dbo.prepaidcards(customerid, status);

create index if not exists ix_prepaidcards_panhash on dbo.prepaidcards(panhash);

create index if not exists ix_ledgerentries_walletdatetype on dbo.ledgerentries(walletaccountid, createdat, entrytype);

create index if not exists ix_cmstransactionlogs_rrnstanpanhash on dbo.cmstransactionlogs(rrn, stan, panhash);

create index if not exists ix_cmstransactionlogs_createdat on dbo.cmstransactionlogs(createdat);

-- ===== 003_operational_control_phase2.sql =====

/*

Phase 2 Operational Control schema for prepaid CMS.

Apply after:

  db/001_production_schema.sql

  db/002_core_prepaid_cms_phase1.sql

*/

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'prepaidcards'
          AND column_name = 'ownertype'
    ) THEN

        ALTER TABLE dbo.prepaidcards
            ADD COLUMN ownertype varchar(40) NOT NULL DEFAULT 'Customer',
            ADD COLUMN agencyid uuid NULL,
            ADD COLUMN corporateid uuid NULL,
            ADD COLUMN corporatedepartmentid uuid NULL,
            ADD COLUMN corporateemployeeid uuid NULL,
            ADD COLUMN inventorybatchreference varchar(80) NOT NULL DEFAULT '';

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.agencyprofiles') is null THEN

            create table if not exists dbo.agencyprofiles

            (

                id uuid not null constraint pk_agencyprofiles primary key,

                agencycode varchar(60) not null,

                name varchar(200) not null,

                parentagencyid uuid null,

                contactname varchar(160) not null,

                mobilenumber varchar(60) not null,

                email varchar(200) not null,

                countrycode varchar(10) not null,

                creditmode varchar(40) not null,

                creditlimit decimal(18,2) not null,

                availablecredit decimal(18,2) not null,

                usedcredit decimal(18,2) not null,

                reservedcredit decimal(18,2) not null,

                commissionprofilecode varchar(80) not null,

                settlementaccountnumber varchar(80) not null,

                status varchar(40) not null,

                createdat timestamptz not null

            );

            create unique index if not exists ux_agencyprofiles_agencycode on dbo.agencyprofiles(agencycode);

            create index if not exists ix_agencyprofiles_status on dbo.agencyprofiles(status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.agencycreditledgerentries') is null THEN

            create table if not exists dbo.agencycreditledgerentries

            (

                id uuid not null constraint pk_agencycreditledgerentries primary key,

                agencyid uuid not null,

                direction varchar(40) not null,

                amount decimal(18,2) not null,

                availablecreditafter decimal(18,2) not null,

                reference varchar(100) not null,

                narrative varchar(500) not null,

                correlationid varchar(64) not null,

                createdat timestamptz not null,

                constraint fk_agencycreditledger_agency foreign key (agencyid) references dbo.agencyprofiles(id)

            );

            create index if not exists ix_agencycreditledger_agencydate on dbo.agencycreditledgerentries(agencyid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.corporateprofiles') is null THEN

            create table if not exists dbo.corporateprofiles

            (

                id uuid not null constraint pk_corporateprofiles primary key,

                corporatecode varchar(60) not null,

                name varchar(200) not null,

                registrationnumber varchar(100) not null,

                contactname varchar(160) not null,

                mobilenumber varchar(60) not null,

                email varchar(200) not null,

                currencycode varchar(3) not null,

                riskrating varchar(30) not null,

                fundingbalance decimal(18,2) not null,

                availablefundingbalance decimal(18,2) not null,

                status varchar(40) not null,

                createdat timestamptz not null

            );

            create unique index if not exists ux_corporateprofiles_corporatecode on dbo.corporateprofiles(corporatecode);

            create index if not exists ix_corporateprofiles_status on dbo.corporateprofiles(status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.corporatedepartments') is null THEN

            create table if not exists dbo.corporatedepartments

            (

                id uuid not null constraint pk_corporatedepartments primary key,

                corporateid uuid not null,

                departmentcode varchar(60) not null,

                name varchar(200) not null,

                costcentercode varchar(80) not null,

                status varchar(40) not null,

                createdat timestamptz not null,

                constraint fk_corporatedepartments_corporate foreign key (corporateid) references dbo.corporateprofiles(id)

            );

            create unique index if not exists ux_corporatedepartments_corpcode on dbo.corporatedepartments(corporateid, departmentcode);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.corporateemployees') is null THEN

            create table if not exists dbo.corporateemployees

            (

                id uuid not null constraint pk_corporateemployees primary key,

                corporateid uuid not null,

                departmentid uuid null,

                employeenumber varchar(60) not null,

                fullname varchar(200) not null,

                mobilenumber varchar(60) not null,

                email varchar(200) not null,

                status varchar(40) not null,

                createdat timestamptz not null,

                constraint fk_corporateemployees_corporate foreign key (corporateid) references dbo.corporateprofiles(id),

                constraint fk_corporateemployees_department foreign key (departmentid) references dbo.corporatedepartments(id)

            );

            create unique index if not exists ux_corporateemployees_corpemployee on dbo.corporateemployees(corporateid, employeenumber);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.corporatebudgets') is null THEN

            create table if not exists dbo.corporatebudgets

            (

                id uuid not null constraint pk_corporatebudgets primary key,

                corporateid uuid not null,

                departmentid uuid null,

                employeeid uuid null,

                budgetcode varchar(80) not null,

                currencycode varchar(3) not null,

                budgetamount decimal(18,2) not null,

                availableamount decimal(18,2) not null,

                periodstart date not null,

                periodend date not null,

                status varchar(40) not null,

                createdat timestamptz not null,

                constraint fk_corporatebudgets_corporate foreign key (corporateid) references dbo.corporateprofiles(id),

                constraint fk_corporatebudgets_department foreign key (departmentid) references dbo.corporatedepartments(id),

                constraint fk_corporatebudgets_employee foreign key (employeeid) references dbo.corporateemployees(id)

            );

            create index if not exists ix_corporatebudgets_corpperiod on dbo.corporatebudgets(corporateid, periodstart, periodend, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.cardstockbatches') is null THEN

            create table if not exists dbo.cardstockbatches

            (

                id uuid not null constraint pk_cardstockbatches primary key,

                productid uuid not null,

                batchreference varchar(80) not null,

                ownertype varchar(40) not null,

                ownerid uuid null,

                quantity int not null,

                availablequantity int not null,

                reservedquantity int not null,

                issuedquantity int not null,

                status varchar(40) not null,

                createdat timestamptz not null,

                constraint fk_cardstockbatches_product foreign key (productid) references dbo.cardproducts(id)

            );

            create unique index if not exists ux_cardstockbatches_batchreference on dbo.cardstockbatches(batchreference);

            create index if not exists ix_cardstockbatches_productowner on dbo.cardstockbatches(productid, ownertype, ownerid, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.advancedlimitrules') is null THEN

            create table if not exists dbo.advancedlimitrules

            (

                id uuid not null constraint pk_advancedlimitrules primary key,

                rulecode varchar(80) not null,

                name varchar(200) not null,

                scope varchar(40) not null,

                scopeid uuid null,

                transactiontypecode varchar(20) not null,

                channelcode varchar(20) not null,

                currencycode varchar(3) not null,

                period varchar(40) not null,

                amountlimit decimal(18,2) not null,

                countlimit int not null,

                priority int not null,

                isactive boolean not null,

                createdat timestamptz not null

            );

            create unique index if not exists ux_advancedlimitrules_rulecode on dbo.advancedlimitrules(rulecode);

            create index if not exists ix_advancedlimitrules_activepriority on dbo.advancedlimitrules(isactive, priority);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.riskrules') is null THEN

            create table if not exists dbo.riskrules

            (

                id uuid not null constraint pk_riskrules primary key,

                rulecode varchar(80) not null,

                name varchar(200) not null,

                ruletype varchar(60) not null,

                matchvalue varchar(500) not null,

                action varchar(40) not null,

                responsecode varchar(4) not null,

                amountthreshold decimal(18,2) null,

                priority int not null,

                isactive boolean not null,

                alerttemplatecode varchar(80) not null,

                createdat timestamptz not null

            );

            create unique index if not exists ux_riskrules_rulecode on dbo.riskrules(rulecode);

            create index if not exists ix_riskrules_activepriority on dbo.riskrules(isactive, priority);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.notificationmessages') is null THEN

            create table if not exists dbo.notificationmessages

            (

                id uuid not null constraint pk_notificationmessages primary key,

                channel varchar(40) not null,

                recipient varchar(256) not null,

                templatecode varchar(80) not null,

                subject varchar(200) not null,

                payloadjson text not null,

                reference varchar(100) not null,

                correlationid varchar(64) not null,

                status varchar(40) not null,

                attempts int not null,

                createdat timestamptz not null,

                sentat timestamptz null

            );

            create index if not exists ix_notificationmessages_statusdate on dbo.notificationmessages(status, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.statementdocuments') is null THEN

            create table if not exists dbo.statementdocuments

            (

                id uuid not null constraint pk_statementdocuments primary key,

                ownertype varchar(40) not null,

                ownerid uuid not null,

                statementnumber varchar(80) not null,

                currencycode varchar(3) not null,

                periodstart date not null,

                periodend date not null,

                openingbalance decimal(18,2) not null,

                closingbalance decimal(18,2) not null,

                debittotal decimal(18,2) not null,

                credittotal decimal(18,2) not null,

                transactioncount int not null,

                status varchar(40) not null,

                createdat timestamptz not null

            );

            create unique index if not exists ux_statementdocuments_statementnumber on dbo.statementdocuments(statementnumber);

            create index if not exists ix_statementdocuments_ownerperiod on dbo.statementdocuments(ownertype, ownerid, periodstart, periodend);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.statementlines') is null THEN

            create table if not exists dbo.statementlines

            (

                id uuid not null constraint pk_statementlines primary key,

                statementid uuid not null,

                transactiondate timestamptz not null,

                reference varchar(100) not null,

                narrative varchar(500) not null,

                direction varchar(20) not null,

                amount decimal(18,2) not null,

                balanceafter decimal(18,2) not null,

                constraint fk_statementlines_statement foreign key (statementid) references dbo.statementdocuments(id)

            );

            create index if not exists ix_statementlines_statementdate on dbo.statementlines(statementid, transactiondate);

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='prepaidcards' and indexname='ix_prepaidcards_agency') THEN
            create index if not exists ix_prepaidcards_agency on dbo.prepaidcards(agencyid) where agencyid is not null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='prepaidcards' and indexname='ix_prepaidcards_corporate') THEN
            create index if not exists ix_prepaidcards_corporate on dbo.prepaidcards(corporateid) where corporateid is not null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='prepaidcards' and indexname='ix_prepaidcards_employee') THEN
            create index if not exists ix_prepaidcards_employee on dbo.prepaidcards(corporateemployeeid) where corporateemployeeid is not null;
    END IF;
END $$;

-- ===== 004_financial_operations_phase3.sql =====

/* Phase 3 - Financial Operations schema for Core Prepaid CMS. Apply after 001, 002 and 003. */

DO $$
BEGIN
    IF to_regclass('dbo.settlementbatches') is null THEN

            create table if not exists dbo.settlementbatches

            (

                id uuid not null constraint pk_settlementbatches primary key,

                batchreference varchar(64) not null,

                filename varchar(260) not null default(''),

                sourcesystem varchar(80) not null default(''),

                currencycode char(3) not null,

                settlementdate date not null,

                recordcount int not null,

                totaldebitamount decimal(18,2) not null default(0),

                totalcreditamount decimal(18,2) not null default(0),

                status varchar(32) not null,

                importedby varchar(120) not null default(''),

                importedat timestamptz not null,

                processedat timestamptz null

            );

            create unique index if not exists ux_settlementbatches_batchreference on dbo.settlementbatches(batchreference);

            create index if not exists ix_settlementbatches_statusdate on dbo.settlementbatches(status, settlementdate);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.settlementrecords') is null THEN

            create table if not exists dbo.settlementrecords

            (

                id uuid not null constraint pk_settlementrecords primary key,

                batchid uuid not null,

                externalreference varchar(80) not null default(''),

                rrn varchar(20) not null default(''),

                stan varchar(12) not null default(''),

                maskedpan varchar(32) not null default(''),

                panhash varchar(128) not null default(''),

                recordtype varchar(32) not null,

                amount decimal(18,2) not null,

                feeamount decimal(18,2) not null default(0),

                currencycode char(3) not null,

                transactiondate timestamptz not null,

                status varchar(32) not null,

                matchedcmstransactionid uuid null,

                responsecode varchar(8) not null default(''),

                narrative varchar(500) not null default(''),

                constraint fk_settlementrecords_batch foreign key (batchid) references dbo.settlementbatches(id)

            );

            create index if not exists ix_settlementrecords_batch on dbo.settlementrecords(batchid);

            create index if not exists ix_settlementrecords_matching on dbo.settlementrecords(rrn, stan, panhash);

            create index if not exists ix_settlementrecords_status on dbo.settlementrecords(status, recordtype);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.reconciliationexceptions') is null THEN

            create table if not exists dbo.reconciliationexceptions

            (

                id uuid not null constraint pk_reconciliationexceptions primary key,

                batchid uuid null,

                settlementrecordid uuid null,

                exceptiontype varchar(64) not null,

                status varchar(32) not null,

                severity varchar(16) not null,

                correlationid varchar(64) not null default(''),

                reference varchar(80) not null default(''),

                expectedamount decimal(18,2) not null default(0),

                actualamount decimal(18,2) not null default(0),

                differenceamount decimal(18,2) not null default(0),

                currencycode char(3) not null default(''),

                reason varchar(1000) not null default(''),

                assignedto varchar(120) not null default(''),

                resolutionnotes varchar(1000) not null default(''),

                createdat timestamptz not null,

                resolvedat timestamptz null,

                constraint fk_reconciliationexceptions_batch foreign key (batchid) references dbo.settlementbatches(id),

                constraint fk_reconciliationexceptions_record foreign key (settlementrecordid) references dbo.settlementrecords(id)

            );

            create index if not exists ix_reconciliationexceptions_status on dbo.reconciliationexceptions(status, createdat);

            create index if not exists ix_reconciliationexceptions_reference on dbo.reconciliationexceptions(reference);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournalentries') is null THEN

            create table if not exists dbo.gljournalentries

            (

                id uuid not null constraint pk_gljournalentries primary key,

                journalnumber varchar(64) not null,

                correlationid varchar(64) not null default(''),

                sourcemodule varchar(64) not null,

                reference varchar(80) not null default(''),

                narrative varchar(500) not null default(''),

                currencycode char(3) not null,

                debittotal decimal(18,2) not null,

                credittotal decimal(18,2) not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                postedat timestamptz null,

                constraint ck_gljournalentries_balanced check (debittotal = credittotal)

            );

            create unique index if not exists ux_gljournalentries_journalnumber on dbo.gljournalentries(journalnumber);

            create index if not exists ix_gljournalentries_sourcereference on dbo.gljournalentries(sourcemodule, reference);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournallines') is null THEN

            create table if not exists dbo.gljournallines

            (

                id uuid not null constraint pk_gljournallines primary key,

                journalentryid uuid not null,

                accountcode varchar(64) not null,

                direction varchar(16) not null,

                amount decimal(18,2) not null,

                currencycode char(3) not null,

                narrative varchar(500) not null default(''),

                constraint fk_gljournallines_journal foreign key (journalentryid) references dbo.gljournalentries(id)

            );

            create index if not exists ix_gljournallines_journal on dbo.gljournallines(journalentryid);

            create index if not exists ix_gljournallines_account on dbo.gljournallines(accountcode);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.financialoperations') is null THEN

            create table if not exists dbo.financialoperations

            (

                id uuid not null constraint pk_financialoperations primary key,

                operationtype varchar(32) not null,

                status varchar(32) not null,

                cardid uuid null,

                walletaccountid uuid null,

                originalcmstransactionid uuid null,

                originalrrn varchar(20) not null default(''),

                originalstan varchar(12) not null default(''),

                newrrn varchar(20) not null default(''),

                newstan varchar(12) not null default(''),

                maskedpan varchar(32) not null default(''),

                panhash varchar(128) not null default(''),

                direction varchar(32) not null,

                amount decimal(18,2) not null,

                feeamount decimal(18,2) not null default(0),

                currencycode char(3) not null,

                reason varchar(1000) not null default(''),

                ticketreference varchar(80) not null default(''),

                maker varchar(120) not null default(''),

                checker varchar(120) not null default(''),

                createdat timestamptz not null,

                approvedat timestamptz null,

                postedat timestamptz null,

                constraint fk_financialoperations_card foreign key (cardid) references dbo.prepaidcards(id),

                constraint fk_financialoperations_wallet foreign key (walletaccountid) references dbo.walletaccounts(id)

            );

            create index if not exists ix_financialoperations_original on dbo.financialoperations(operationtype, originalrrn, originalstan, panhash, status);

            create index if not exists ix_financialoperations_statusdate on dbo.financialoperations(status, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.settlementstatements') is null THEN

            create table if not exists dbo.settlementstatements

            (

                id uuid not null constraint pk_settlementstatements primary key,

                partytype varchar(32) not null,

                partyid uuid not null,

                statementnumber varchar(64) not null,

                currencycode char(3) not null,

                periodstart date not null,

                periodend date not null,

                grossdebitamount decimal(18,2) not null default(0),

                grosscreditamount decimal(18,2) not null default(0),

                feeamount decimal(18,2) not null default(0),

                commissionamount decimal(18,2) not null default(0),

                netsettlementamount decimal(18,2) not null default(0),

                status varchar(32) not null,

                createdat timestamptz not null,

                postedat timestamptz null

            );

            create unique index if not exists ux_settlementstatements_number on dbo.settlementstatements(statementnumber);

            create index if not exists ix_settlementstatements_partyperiod on dbo.settlementstatements(partytype, partyid, periodstart, periodend);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.settlementstatementlines') is null THEN

            create table if not exists dbo.settlementstatementlines

            (

                id uuid not null constraint pk_settlementstatementlines primary key,

                settlementstatementid uuid not null,

                transactiondate timestamptz not null,

                sourcetype varchar(32) not null,

                reference varchar(80) not null default(''),

                narrative varchar(500) not null default(''),

                direction varchar(16) not null,

                amount decimal(18,2) not null,

                currencycode char(3) not null,

                constraint fk_settlementstatementlines_statement foreign key (settlementstatementid) references dbo.settlementstatements(id)

            );

            create index if not exists ix_settlementstatementlines_statement on dbo.settlementstatementlines(settlementstatementid);

    END IF;
END $$;

-- ===== 005_enterprise_production_phase4.sql =====

-- Phase 4 Enterprise Production CMS migration

-- Apply after 001_production_schema.sql, 002_core_prepaid_cms_phase1.sql,

-- 003_operational_control_phase2.sql, and 004_financial_operations_phase3.sql.

-- This schema matches SqlEnterpriseProductionRepository and the Phase 4 domain model.

DO $$
BEGIN
    IF to_regclass('dbo.cryptokeyprofiles') is null THEN
        create table if not exists dbo.cryptokeyprofiles
        
        (
        
            id uuid not null constraint pk_cryptokeyprofiles primary key,
        
            keyprofilecode varchar(80) not null,
        
            name varchar(200) not null,
        
            purpose varchar(40) not null,
        
            hsmkeyalias varchar(250) not null,
        
            hsmpartition varchar(120) not null,
        
            algorithm varchar(80) not null,
        
            keyversion int not null,
        
            effectivefrom timestamptz not null,
        
            rotationdueat timestamptz null,
        
            status varchar(40) not null,
        
            createdby varchar(120) not null,
        
            createdat timestamptz not null,
        
            constraint ux_cryptokeyprofiles_code unique(keyprofilecode)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.amlwatchlistentries') is null THEN
        create table if not exists dbo.amlwatchlistentries
        
        (
        
            id uuid not null constraint pk_amlwatchlistentries primary key,
        
            listcode varchar(80) not null,
        
            listtype varchar(40) not null,
        
            entityname varchar(250) not null,
        
            countrycode varchar(8) not null,
        
            externalreference varchar(120) not null,
        
            matchkeywords varchar(1000) not null,
        
            isactive boolean not null,
        
            createdat timestamptz not null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.amlscreeningrecords') is null THEN
        create table if not exists dbo.amlscreeningrecords
        
        (
        
            id uuid not null constraint pk_amlscreeningrecords primary key,
        
            correlationid varchar(80) not null,
        
            entitytype varchar(40) not null,
        
            entityreference varchar(120) not null,
        
            entityname varchar(250) not null,
        
            countrycode varchar(8) not null,
        
            matchsummary varchar(1000) not null,
        
            status varchar(40) not null,
        
            decision varchar(40) not null,
        
            score decimal(18,2) not null,
        
            reviewer varchar(120) not null,
        
            resolutionnotes varchar(1000) not null,
        
            createdat timestamptz not null,
        
            resolvedat timestamptz null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.threedsauthenticationrecords') is null THEN
        create table if not exists dbo.threedsauthenticationrecords
        
        (
        
            id uuid not null constraint pk_threedsauthenticationrecords primary key,
        
            correlationid varchar(80) not null,
        
            cardid uuid null,
        
            maskedpan varchar(32) not null,
        
            panhash varchar(128) not null,
        
            rrn varchar(20) not null,
        
            stan varchar(12) not null,
        
            amount decimal(18,2) not null,
        
            currencycode varchar(8) not null,
        
            merchantid varchar(80) not null,
        
            merchantname varchar(250) not null,
        
            merchantcountrycode varchar(8) not null,
        
            protocolversion varchar(40) not null,
        
            directoryservertransactionid varchar(120) not null,
        
            acstransactionid varchar(120) not null,
        
            eci varchar(20) not null,
        
            cavvtoken varchar(512) not null,
        
            status varchar(40) not null,
        
            createdat timestamptz not null,
        
            expiresat timestamptz not null,
        
            completedat timestamptz null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.fraudmonitoringevents') is null THEN
        create table if not exists dbo.fraudmonitoringevents
        
        (
        
            id uuid not null constraint pk_fraudmonitoringevents primary key,
        
            correlationid varchar(80) not null,
        
            eventtype varchar(40) not null,
        
            cardid uuid null,
        
            customerid uuid null,
        
            maskedpan varchar(32) not null,
        
            panhash varchar(128) not null,
        
            amount decimal(18,2) not null,
        
            currencycode varchar(8) not null,
        
            merchantid varchar(80) not null,
        
            merchantcategorycode varchar(20) not null,
        
            merchantcountrycode varchar(8) not null,
        
            deviceid varchar(120) not null,
        
            ipaddress varchar(64) not null,
        
            score int not null,
        
            signalsjson text not null,
        
            createdat timestamptz not null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.fraudalerts') is null THEN
        create table if not exists dbo.fraudalerts
        
        (
        
            id uuid not null constraint pk_fraudalerts primary key,
        
            correlationid varchar(80) not null,
        
            fraudeventid uuid null,
        
            severity varchar(40) not null,
        
            status varchar(40) not null,
        
            rulesummary varchar(1000) not null,
        
            responsecode varchar(8) not null,
        
            assignedto varchar(120) not null,
        
            resolutionnotes varchar(1000) not null,
        
            createdat timestamptz not null,
        
            closedat timestamptz null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.siemsecurityevents') is null THEN
        create table if not exists dbo.siemsecurityevents
        
        (
        
            id uuid not null constraint pk_siemsecurityevents primary key,
        
            correlationid varchar(80) not null,
        
            eventtype varchar(100) not null,
        
            severity varchar(40) not null,
        
            sourcesystem varchar(80) not null,
        
            actor varchar(120) not null,
        
            entityreference varchar(120) not null,
        
            message varchar(1000) not null,
        
            payloadjson text not null,
        
            deliverystatus varchar(40) not null,
        
            attempts int not null,
        
            createdat timestamptz not null,
        
            deliveredat timestamptz null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.datawarehouseexportjobs') is null THEN
        create table if not exists dbo.datawarehouseexportjobs
        
        (
        
            id uuid not null constraint pk_datawarehouseexportjobs primary key,
        
            jobreference varchar(120) not null,
        
            exporttype varchar(40) not null,
        
            businessdate date not null,
        
            outputlocation varchar(500) not null,
        
            status varchar(40) not null,
        
            exportedrecordcount int not null,
        
            checksum varchar(128) not null,
        
            errormessage varchar(1000) not null,
        
            createdat timestamptz not null,
        
            completedat timestamptz null,
        
            constraint ux_datawarehouseexportjobs_reference unique(jobreference)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.clusternodeheartbeats') is null THEN
        create table if not exists dbo.clusternodeheartbeats
        
        (
        
            id uuid not null constraint pk_clusternodeheartbeats primary key,
        
            nodename varchar(120) not null,
        
            instanceid varchar(120) not null,
        
            role varchar(40) not null,
        
            healthstatus varchar(40) not null,
        
            region varchar(80) not null,
        
            availabilityzone varchar(80) not null,
        
            activeconnections int not null,
        
            cpupercent decimal(18,2) not null,
        
            memorypercent decimal(18,2) not null,
        
            lastheartbeatat timestamptz not null,
        
            constraint ux_clusternodeheartbeats_nodeinstance unique(nodename, instanceid)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.failoverevents') is null THEN
        create table if not exists dbo.failoverevents
        
        (
        
            id uuid not null constraint pk_failoverevents primary key,
        
            eventreference varchar(120) not null,
        
            fromnode varchar(120) not null,
        
            tonode varchar(120) not null,
        
            reason varchar(1000) not null,
        
            successful boolean not null,
        
            performedby varchar(120) not null,
        
            createdat timestamptz not null,
        
            constraint ux_failoverevents_reference unique(eventreference)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.disasterrecoveryplans') is null THEN
        create table if not exists dbo.disasterrecoveryplans
        
        (
        
            id uuid not null constraint pk_disasterrecoveryplans primary key,
        
            plancode varchar(80) not null,
        
            name varchar(200) not null,
        
            primaryregion varchar(80) not null,
        
            recoveryregion varchar(80) not null,
        
            rposeconds int not null,
        
            rtoseconds int not null,
        
            runbooklocation varchar(500) not null,
        
            status varchar(40) not null,
        
            createdat timestamptz not null,
        
            constraint ux_disasterrecoveryplans_code unique(plancode)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.disasterrecoverydrills') is null THEN
        create table if not exists dbo.disasterrecoverydrills
        
        (
        
            id uuid not null constraint pk_disasterrecoverydrills primary key,
        
            planid uuid not null,
        
            drillreference varchar(120) not null,
        
            status varchar(40) not null,
        
            startedat timestamptz not null,
        
            completedat timestamptz null,
        
            actualrposeconds int not null,
        
            actualrtoseconds int not null,
        
            findings varchar(2000) not null,
        
            remediationactions varchar(2000) not null,
        
            constraint ux_disasterrecoverydrills_reference unique(drillreference)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.regulatoryreports') is null THEN
        create table if not exists dbo.regulatoryreports
        
        (
        
            id uuid not null constraint pk_regulatoryreports primary key,
        
            reportreference varchar(120) not null,
        
            reporttype varchar(80) not null,
        
            periodstart date not null,
        
            periodend date not null,
        
            regulatorcode varchar(40) not null,
        
            status varchar(40) not null,
        
            generatedby varchar(120) not null,
        
            linecount int not null,
        
            outputlocation varchar(500) not null,
        
            checksum varchar(128) not null,
        
            createdat timestamptz not null,
        
            submittedat timestamptz null,
        
            constraint ux_regulatoryreports_reference unique(reportreference)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.regulatoryreportlines') is null THEN
        create table if not exists dbo.regulatoryreportlines
        
        (
        
            id uuid not null constraint pk_regulatoryreportlines primary key,
        
            reportid uuid not null,
        
            linetype varchar(80) not null,
        
            reference varchar(120) not null,
        
            amount decimal(18,2) not null,
        
            count int not null,
        
            currencycode varchar(8) not null,
        
            narrative varchar(1000) not null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='cryptokeyprofiles' and indexname='ix_cryptokeyprofiles_purposestatus') THEN
        create index if not exists ix_cryptokeyprofiles_purposestatus on dbo.cryptokeyprofiles(purpose, status, rotationdueat);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='amlwatchlistentries' and indexname='ix_amlwatchlist_active') THEN
        create index if not exists ix_amlwatchlist_active on dbo.amlwatchlistentries(isactive, listtype, entityname);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='amlscreeningrecords' and indexname='ix_amlscreening_entity') THEN
        create index if not exists ix_amlscreening_entity on dbo.amlscreeningrecords(entityreference, createdat desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='threedsauthenticationrecords' and indexname='ix_threeds_dstransaction') THEN
        create index if not exists ix_threeds_dstransaction on dbo.threedsauthenticationrecords(directoryservertransactionid);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='threedsauthenticationrecords' and indexname='ix_threeds_panhashdate') THEN
        create index if not exists ix_threeds_panhashdate on dbo.threedsauthenticationrecords(panhash, createdat desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='fraudmonitoringevents' and indexname='ix_fraudevents_panhashdate') THEN
        create index if not exists ix_fraudevents_panhashdate on dbo.fraudmonitoringevents(panhash, createdat desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='fraudalerts' and indexname='ix_fraudalerts_status') THEN
        create index if not exists ix_fraudalerts_status on dbo.fraudalerts(status, severity, createdat desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='siemsecurityevents' and indexname='ix_siemsecurityevents_pending') THEN
        create index if not exists ix_siemsecurityevents_pending on dbo.siemsecurityevents(deliverystatus, createdat);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='datawarehouseexportjobs' and indexname='ix_datawarehouseexportjobs_due') THEN
        create index if not exists ix_datawarehouseexportjobs_due on dbo.datawarehouseexportjobs(status, createdat);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='clusternodeheartbeats' and indexname='ix_clusterheartbeats_status') THEN
        create index if not exists ix_clusterheartbeats_status on dbo.clusternodeheartbeats(healthstatus, lastheartbeatat desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='regulatoryreports' and indexname='ix_regulatoryreports_period') THEN
        create index if not exists ix_regulatoryreports_period on dbo.regulatoryreports(reporttype, periodstart, periodend, status);
    END IF;
END $$;

-- ===== 006_tokenization_terminal_keys_institutions.sql =====

-- Phase 5 migration: card-on-file tokenization (CoFT), dynamic terminal session keys, and

-- multi-institutional configuration.

-- Apply after 001_production_schema.sql through 005_enterprise_production_phase4.sql.

DO $$
BEGIN
    IF to_regclass('dbo.cardtokens') is null THEN
        create table if not exists dbo.cardtokens
        
        (
        
            id uuid not null constraint pk_cardtokens primary key,
        
            token varchar(19) not null,
        
            maskedpan varchar(32) not null,
        
            pantoken text not null,
        
            panhash varchar(128) not null,
        
            expirymonth int not null,
        
            expiryyear int not null,
        
            merchantid varchar(64) not null,
        
            sourcenodeid varchar(64) not null,
        
            status varchar(16) not null,
        
            createdat timestamptz not null,
        
            lastusedat timestamptz null,
        
            constraint ux_cardtokens_token unique(token)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='cardtokens' and indexname='ix_cardtokens_panhash_merchant') THEN
        create index if not exists ix_cardtokens_panhash_merchant on dbo.cardtokens(panhash, merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.terminalkeyprofiles') is null THEN
        create table if not exists dbo.terminalkeyprofiles
        
        (
        
            id uuid not null constraint pk_terminalkeyprofiles primary key,
        
            terminalid varchar(16) not null,
        
            sourcenodeid varchar(64) not null,
        
            keyprofile varchar(128) not null,
        
            keyserialnumber varchar(20) not null constraint df_terminalkeyprofiles_ksn default(''),
        
            keycheckvalue varchar(16) not null constraint df_terminalkeyprofiles_kcv default(''),
        
            isactive boolean not null constraint df_terminalkeyprofiles_active default(true),
        
            createdat timestamptz not null,
        
            lastrotatedat timestamptz null,
        
            constraint ux_terminalkeyprofiles_terminalid unique(terminalid)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.institutions') is null THEN
        create table if not exists dbo.institutions
        
        (
        
            id uuid not null constraint pk_institutions primary key,
        
            code varchar(32) not null,
        
            name varchar(200) not null,
        
            type varchar(16) not null,
        
            countrycode varchar(2) not null constraint df_institutions_country default(''),
        
            defaultcurrencycode varchar(3) not null constraint df_institutions_currency default(''),
        
            isactive boolean not null constraint df_institutions_active default(true),
        
            constraint ux_institutions_code unique(code)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='sourcenodes' and column_name='institutioncode') THEN
        alter table dbo.sourcenodes add institutioncode varchar(32) not null constraint df_sourcenodes_institutioncode default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='sinknodes' and column_name='institutioncode') THEN
        alter table dbo.sinknodes add institutioncode varchar(32) not null constraint df_sinknodes_institutioncode default('');
    END IF;
END $$;

-- ===== 007_card_fee_rules.sql =====

-- Phase 6 migration: card-lifecycle fee/waiver configuration (issuance, replacement, upgrade,

-- RePIN, annual maintenance, add-on card), each scoped BIN-wise, account-scheme-wise, or

-- card-wise.

-- Apply after 001_production_schema.sql through 006_tokenization_terminal_keys_institutions.sql.

DO $$
BEGIN
    IF to_regclass('dbo.cardfeerules') is null THEN
        create table if not exists dbo.cardfeerules
        
        (
        
            id uuid not null constraint pk_cardfeerules primary key,
        
            feetype varchar(32) not null,
        
            scopetype varchar(16) not null,
        
            scopevalue varchar(64) not null,
        
            iswaiver boolean not null constraint df_cardfeerules_iswaiver default(false),
        
            feeid uuid null,
        
            isactive boolean not null constraint df_cardfeerules_active default(true),
        
            description varchar(400) not null constraint df_cardfeerules_description default(''),
        
            createdat timestamptz not null,
        
            constraint fk_cardfeerules_fees foreign key (feeid) references dbo.fees(id)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='cardfeerules' and indexname='ix_cardfeerules_lookup') THEN
        create index if not exists ix_cardfeerules_lookup on dbo.cardfeerules(feetype, scopetype, scopevalue, isactive);
    END IF;
END $$;

-- ===== 008_ledger_and_crypto_hardening.sql =====

-- ============================================================

-- Migration 008 — Financial Ledger & Cryptographic Hardening

-- Fixes for CD-01 (double-entry ledger), CD-02 (concurrency),

-- and CD-04 (CVV storage).

-- Apply after 001 through 007.

-- ============================================================

-- ---------------------------------------------------------------

-- CD-01 FIX: Chart of Accounts master table

-- GlJournalLines references account codes as free text — adding

-- the accounts master for referential integrity and reporting.

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.glaccounts') is null THEN
        create table if not exists dbo.glaccounts
        
        (
        
            id           uuid not null constraint pk_glaccounts primary key default gen_random_uuid(),
        
            accountcode  varchar(64)     not null,
        
            name         varchar(200)    not null,
        
            accounttype  varchar(16)     not null,  -- Asset | Liability | Income | Expense | Clearing | Suspense
        
            currencycode varchar(3)      not null constraint df_glaccounts_currency default (''),
        
            isactive     boolean              not null constraint df_glaccounts_active default (true),
        
            createdat    timestamptz   not null constraint df_glaccounts_created default (clock_timestamp()),
        
            constraint ux_glaccounts_code unique (accountcode)
        
        );
    END IF;
END $$;

-- Seed the five standard accounts used by FinancialOperationsService.

-- These match the constants: GlSettlementClearing, GlNostroFunding,

-- GlCardholderLiability, GlFeeIncome, GlAdjustmentExpense.

DO $$
BEGIN
    IF not exists (select 1 from dbo.glaccounts where accountcode = '1000-SETTLEMENT-CLEARING') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)
        
        values ('1000-SETTLEMENT-CLEARING', 'Settlement Clearing Account', 'Clearing', '');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from dbo.glaccounts where accountcode = '1100-NOSTRO-FUNDING') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)
        
        values ('1100-NOSTRO-FUNDING', 'Nostro / Funding Receivable', 'Asset', '');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from dbo.glaccounts where accountcode = '2100-CARDHOLDER-LIABILITY') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)
        
        values ('2100-CARDHOLDER-LIABILITY', 'Cardholder E-numeric(19,4) Liability', 'Liability', '');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from dbo.glaccounts where accountcode = '4000-FEE-INCOME') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)
        
        values ('4000-FEE-INCOME', 'Transaction Fee Income', 'Income', '');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from dbo.glaccounts where accountcode = '5000-ADJUSTMENT-EXPENSE') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)
        
        values ('5000-ADJUSTMENT-EXPENSE', 'Financial Adjustment Expense', 'Expense', '');
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Verify WalletAccounts already has the rowversion column

-- (added in migration 002). Guard is idempotent.

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='walletaccounts' and column_name='rowversion') THEN

            alter table dbo.walletaccounts add rowversion bytea not null;

    END IF;
END $$;

-- ---------------------------------------------------------------

-- CD-04 FIX: Cvv2Token — encrypted CVV2 value on PrepaidCards.

-- Populated at card issuance via HSM GenerateCvv + AES-GCM protect.

-- NULL allowed for cards issued before this migration.

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='cvv2token') THEN

            alter table dbo.prepaidcards

            add cvv2token text null constraint df_prepaidcards_cvv2token default ('');

    END IF;
END $$;

-- ===== 009_eft_orchestration.sql =====

-- ============================================================

-- Migration 009 — EFT Orchestration & Interbank Transfer Processing

-- Implements A1 from the Enterprise Gap Analysis.

-- Apply after 001 through 008.

-- ============================================================

-- ---------------------------------------------------------------

-- Transaction Lifecycle State Machine

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.transactionlifecyclestates') is null THEN
        create table if not exists dbo.transactionlifecyclestates
        
        (
        
            id                   uuid not null constraint pk_transactionlifecyclestates primary key default gen_random_uuid(),
        
            correlationid        varchar(64)     not null,
        
            stan                 varchar(12)     not null constraint df_tls_stan default (''),
        
            sourcenodeid         varchar(64)     not null constraint df_tls_sourcenode default (''),
        
            previousstate        varchar(32)     not null,
        
            newstate             varchar(32)     not null,
        
            reason               varchar(500)    not null constraint df_tls_reason default (''),
        
            latencyfromreceivedms bigint          not null constraint df_tls_latency default (0),
        
            occurredat           timestamptz   not null constraint df_tls_at default (clock_timestamp())
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='transactionlifecyclestates' and indexname='ix_tls_correlationid') THEN
        create index if not exists ix_tls_correlationid on dbo.transactionlifecyclestates(correlationid, occurredat);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='transactionlifecyclestates' and indexname='ix_tls_newstate_occurredat') THEN
        create index if not exists ix_tls_newstate_occurredat on dbo.transactionlifecyclestates(newstate, occurredat);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Extend TransactionLog with LifecycleState

-- ---------------------------------------------------------------

/*DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='transactionlog' and column_name='lifecyclestate') THEN
            alter table dbo.transactionlog add lifecyclestate varchar(32) not null constraint df_txlog_lifecyclestate default ('Received');
    END IF;
END $$;
*/

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'transactionlogs'
          AND column_name = 'lifecyclestate'
    ) THEN

        ALTER TABLE dbo.transactionlogs
            ADD COLUMN lifecyclestate varchar(32)
            NOT NULL
            DEFAULT 'Received';

    END IF;
END $$;

-- ---------------------------------------------------------------

-- Extend Routes with FallbackSinkNodeId for network failover

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='fallbacksinknodeid') THEN
            alter table dbo.routes add fallbacksinknodeid uuid null;
    END IF;
END $$;

-- ---------------------------------------------------------------

-- EFT Transfers (NEFT / RTGS / IMPS / ACH)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.efttransfers') is null THEN
        create table if not exists dbo.efttransfers
        
        (
        
            id                         uuid not null constraint pk_efttransfers primary key default gen_random_uuid(),
        
            railtype                   varchar(32)     not null,
        
            status                     varchar(32)     not null,
        
            correlationid              varchar(64)     not null,
        
            senderaccountnumber        varchar(32)     not null,
        
            senderifsccode             varchar(11)     not null constraint df_eft_senderifsc default (''),
        
            senderbankname             varchar(200)    not null constraint df_eft_senderbank default (''),
        
            beneficiaryaccountnumber   varchar(32)     not null,
        
            beneficiaryifsccode        varchar(11)     not null constraint df_eft_beneifsc default (''),
        
            beneficiarybankname        varchar(200)    not null constraint df_eft_benebank default (''),
        
            beneficiaryname            varchar(200)    not null constraint df_eft_benename default (''),
        
            amount                     decimal(18,4)    not null,
        
            currencycode               varchar(3)      not null constraint df_eft_currency default ('356'),
        
            narration                  varchar(500)    not null constraint df_eft_narration default (''),
        
            customerreference          varchar(64)     not null constraint df_eft_custref default (''),
        
            railtransactionref         varchar(64)     not null constraint df_eft_railref default (''),
        
            batchsequencenumber        varchar(32)     not null constraint df_eft_batchseq default (''),
        
            settlementcycleid          varchar(64)     not null constraint df_eft_cycleid default (''),
        
            originatingcorrelationid   varchar(64)     not null constraint df_eft_origcorr default (''),
        
            rejectionreason            varchar(500)    not null constraint df_eft_rejreason default (''),
        
            createdat                  timestamptz   not null constraint df_eft_created default (clock_timestamp()),
        
            submittedat                timestamptz   null,
        
            settledat                  timestamptz   null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='efttransfers' and indexname='ix_efttransfers_correlationid') THEN
        create index if not exists ix_efttransfers_correlationid on dbo.efttransfers(correlationid);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='efttransfers' and indexname='ix_efttransfers_status_rail') THEN
        create index if not exists ix_efttransfers_status_rail on dbo.efttransfers(status, railtype, createdat);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Clearing Batches

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.clearingbatches') is null THEN
        create table if not exists dbo.clearingbatches
        
        (
        
            id                   uuid not null constraint pk_clearingbatches primary key default gen_random_uuid(),
        
            batchreference       varchar(64)     not null,
        
            fileformat           varchar(32)     not null,
        
            status               varchar(32)     not null,
        
            businessdate         date             not null,
        
            settlementprofile    varchar(64)     not null constraint df_cb_profile default (''),
        
            currencycode         varchar(3)      not null constraint df_cb_currency default (''),
        
            institutioncode      varchar(32)     not null constraint df_cb_institution default (''),
        
            recordcount          int              not null constraint df_cb_count default (0),
        
            totaldebitamount     decimal(18,4)    not null constraint df_cb_debit default (0),
        
            totalcreditamount    decimal(18,4)    not null constraint df_cb_credit default (0),
        
            netsettlementamount  decimal(18,4)    not null constraint df_cb_net default (0),
        
            outputfilepath       varchar(1000)   not null constraint df_cb_filepath default (''),
        
            networkackreference  varchar(64)     not null constraint df_cb_ackref default (''),
        
            createdat            timestamptz   not null constraint df_cb_created default (clock_timestamp()),
        
            generatedat          timestamptz   null,
        
            transmittedat        timestamptz   null,
        
            acknowledgedat       timestamptz   null,
        
            constraint ux_clearingbatches_ref unique (batchreference)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='clearingbatches' and indexname='ix_clearingbatches_date_profile') THEN
        create index if not exists ix_clearingbatches_date_profile on dbo.clearingbatches(businessdate, settlementprofile);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Clearing Records (one per transaction per batch)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.clearingrecords') is null THEN
        create table if not exists dbo.clearingrecords
        
        (
        
            id                   uuid not null constraint pk_clearingrecords primary key default gen_random_uuid(),
        
            clearingbatchid      uuid not null,
        
            correlationid        varchar(64)     not null,
        
            stan                 varchar(12)     not null constraint df_cr_stan default (''),
        
            rrn                  varchar(12)     not null constraint df_cr_rrn default (''),
        
            maskedpan            varchar(32)     not null constraint df_cr_pan default (''),
        
            panhash              varchar(128)    not null constraint df_cr_panhash default (''),
        
            mti                  varchar(4)      not null constraint df_cr_mti default (''),
        
            processingcode       varchar(6)      not null constraint df_cr_proccode default (''),
        
            transactionamount    decimal(18,4)    not null constraint df_cr_amount default (0),
        
            feeamount            decimal(18,4)    not null constraint df_cr_fee default (0),
        
            currencycode         varchar(3)      not null constraint df_cr_currency default (''),
        
            sourcenodeid         varchar(64)     not null constraint df_cr_sourcenode default (''),
        
            sinknodeid           varchar(64)     not null constraint df_cr_sinknode default (''),
        
            authorizationcode    varchar(16)     not null constraint df_cr_authcode default (''),
        
            transactionat        timestamptz   not null constraint df_cr_txat default (clock_timestamp()),
        
            isincluded           boolean              not null constraint df_cr_included default (true),
        
            exclusionreason      varchar(500)    not null constraint df_cr_exclreason default (''),
        
            constraint fk_clearingrecords_batch foreign key (clearingbatchid) references dbo.clearingbatches(id)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='clearingrecords' and indexname='ix_clearingrecords_batch') THEN
        create index if not exists ix_clearingrecords_batch on dbo.clearingrecords(clearingbatchid);
    END IF;
END $$;

-- ===== 010_card_lifecycle.sql =====

-- ============================================================

-- Migration 010 — Customer Onboarding & Card Lifecycle

-- Implements A2 from the Enterprise Gap Analysis.

-- Apply after 001 through 009.

-- ============================================================

-- ---------------------------------------------------------------

-- PrepaidCards — new lifecycle columns

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='pintoken') THEN
            alter table dbo.prepaidcards add pintoken text null constraint df_cards_pintoken default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='blockreason') THEN
            alter table dbo.prepaidcards add blockreason varchar(64) not null constraint df_cards_blockreason default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='blockedat') THEN
            alter table dbo.prepaidcards add blockedat timestamptz null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='replacedbycardid') THEN
            alter table dbo.prepaidcards add replacedbycardid uuid null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='updatedat') THEN
            alter table dbo.prepaidcards add updatedat timestamptz null;
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Customers — new profile fields

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='updatedat') THEN
            alter table dbo.customers add updatedat timestamptz null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='dateofbirth') THEN
            alter table dbo.customers add dateofbirth varchar(10) not null constraint df_customers_dob default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='addressline1') THEN
            alter table dbo.customers add addressline1 varchar(200) not null constraint df_customers_addr1 default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='city') THEN
            alter table dbo.customers add city varchar(100) not null constraint df_customers_city default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='stateorregion') THEN
            alter table dbo.customers add stateorregion varchar(100) not null constraint df_customers_state default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='countrycode') THEN
            alter table dbo.customers add countrycode varchar(2) not null constraint df_customers_country default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='postalcode') THEN
            alter table dbo.customers add postalcode varchar(16) not null constraint df_customers_postal default ('');
    END IF;
END $$;

-- ---------------------------------------------------------------

-- KYC Documents

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.kycdocuments') is null THEN
        create table if not exists dbo.kycdocuments
        
        (
        
            id                       uuid not null constraint pk_kycdocuments primary key default gen_random_uuid(),
        
            customerid               uuid not null,
        
            customernumber           varchar(64)     not null,
        
            documenttype             varchar(32)     not null,
        
            documentnumber           varchar(64)     not null,
        
            issuingauthority         varchar(200)    not null constraint df_kyc_authority default (''),
        
            issuingcountrycode       varchar(2)      not null constraint df_kyc_country default (''),
        
            issuedate                date             null,
        
            expirydate               date             null,
        
            status                   varchar(32)     not null,
        
            documentvaultreference   varchar(500)    not null constraint df_kyc_vault default (''),
        
            providerverificationid   varchar(128)    not null constraint df_kyc_provref default (''),
        
            rejectionreason          varchar(500)    not null constraint df_kyc_rejection default (''),
        
            submittedby              varchar(128)    not null constraint df_kyc_submitby default (''),
        
            reviewedby               varchar(128)    not null constraint df_kyc_reviewby default (''),
        
            submittedat              timestamptz   not null constraint df_kyc_submitat default (clock_timestamp()),
        
            reviewedat               timestamptz   null
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='kycdocuments' and indexname='ix_kycdocuments_customer') THEN
        create index if not exists ix_kycdocuments_customer on dbo.kycdocuments(customerid, submittedat desc);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Authorization Holds (pre-auth / ISO 0100 / 0220 / 0420)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.authorizationholds') is null THEN
        create table if not exists dbo.authorizationholds
        
        (
        
            id                    uuid not null constraint pk_authorizationholds primary key default gen_random_uuid(),
        
            walletaccountid       uuid not null,
        
            cardid                uuid null,
        
            correlationid         varchar(64)     not null,
        
            stan                  varchar(12)     not null constraint df_ah_stan default (''),
        
            rrn                   varchar(12)     not null,
        
            authorizationcode     varchar(16)     not null constraint df_ah_authcode default (''),
        
            holdamount            decimal(18,4)    not null,
        
            currencycode          varchar(3)      not null,
        
            merchantid            varchar(64)     not null constraint df_ah_merchid default (''),
        
            merchantname          varchar(200)    not null constraint df_ah_merchname default (''),
        
            terminalid            varchar(16)     not null constraint df_ah_terminal default (''),
        
            status                varchar(16)     not null,
        
            placedat              timestamptz   not null constraint df_ah_placedat default (clock_timestamp()),
        
            expiresat             timestamptz   not null,
        
            releasedat            timestamptz   null,
        
            capturedamount        decimal(18,4)    not null constraint df_ah_capturedamt default (0),
        
            capturecorrelationid  varchar(64)     not null constraint df_ah_capturecorr default ('')
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='authorizationholds' and indexname='ix_authholds_wallet_status') THEN
        create index if not exists ix_authholds_wallet_status on dbo.authorizationholds(walletaccountid, status, expiresat);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='authorizationholds' and indexname='ix_authholds_rrn') THEN
        create index if not exists ix_authholds_rrn on dbo.authorizationholds(rrn, walletaccountid);
    END IF;
END $$;

-- ===== 011_monitoring_alerting.sql =====

-- ============================================================

-- Migration 011 — Monitoring, Alerting & SIEM Hardening

-- Implements A3 from the Enterprise Gap Analysis.

-- Apply after 001 through 010.

-- ============================================================

-- ---------------------------------------------------------------

-- Alert Rules (configurable threshold definitions)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.alertrules') is null THEN
        create table if not exists dbo.alertrules
        
        (
        
            id                  uuid not null constraint pk_alertrules primary key default gen_random_uuid(),
        
            name                varchar(200)    not null,
        
            ruletype            varchar(64)     not null,
        
            severity            varchar(16)     not null,
        
            thresholdvalue      float            not null,
        
            evaluationwindowms  bigint           not null,
        
            minimumsamples      int              not null constraint df_ar_minsamples default (1),
        
            suppressionwindowms bigint           not null constraint df_ar_suppwindow default (0),
        
            nodeidfilter        varchar(64)     not null constraint df_ar_nodefilter default (''),
        
            isactive            boolean              not null constraint df_ar_active default (true),
        
            createdat           timestamptz   not null constraint df_ar_created default (clock_timestamp())
        
        );
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Alert Events (fired instances)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.alertevents') is null THEN
        create table if not exists dbo.alertevents
        
        (
        
            id                  uuid not null constraint pk_alertevents primary key default gen_random_uuid(),
        
            ruleid              uuid not null,
        
            rulename            varchar(200)    not null,
        
            ruletype            varchar(64)     not null,
        
            severity            varchar(16)     not null,
        
            status              varchar(16)     not null,
        
            title               varchar(500)    not null,
        
            detail              text    not null,
        
            nodeid              varchar(64)     not null constraint df_ae_nodeid default (''),
        
            observedvalue       float            not null,
        
            thresholdvalue      float            not null,
        
            firedat             timestamptz   not null constraint df_ae_firedat default (clock_timestamp()),
        
            acknowledgedat      timestamptz   null,
        
            resolvedat          timestamptz   null,
        
            acknowledgedby      varchar(128)    not null constraint df_ae_ackby default (''),
        
            webhookresponsecode int              not null constraint df_ae_webhookcode default (0),
        
            forwardedtosiem     boolean              not null constraint df_ae_forwardedsiem default (false)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='alertevents' and indexname='ix_alertevents_status_firedat') THEN
        create index if not exists ix_alertevents_status_firedat on dbo.alertevents(status, firedat desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='alertevents' and indexname='ix_alertevents_ruleid_firedat') THEN
        create index if not exists ix_alertevents_ruleid_firedat on dbo.alertevents(ruleid, firedat desc);
    END IF;
END $$;

-- ===== 012_payment_switch_enhancements.sql =====

-- ============================================================

-- Migration 012 — Payment Switch B1 Enhancements

-- Pre-auth tracking, stand-in profiles, distributed idempotency

-- Apply after 001 through 011.

-- ============================================================

-- ---------------------------------------------------------------

-- Pre-Authorization Records (ISO 0100 / 0220 / 0420)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.preauthrecords') is null THEN
        create table if not exists dbo.preauthrecords
        
        (
        
            id                    uuid not null constraint pk_preauthrecords primary key default gen_random_uuid(),
        
            sourcenodeid          varchar(64)     not null,
        
            stan                  varchar(12)     not null,
        
            rrn                   varchar(12)     not null constraint df_par_rrn default (''),
        
            authorizationcode     varchar(16)     not null constraint df_par_authcode default (''),
        
            maskedpan             varchar(32)     not null constraint df_par_maskedpan default (''),
        
            panhash               varchar(128)    not null constraint df_par_panhash default (''),
        
            authorizedamount      decimal(18,4)    not null,
        
            currencycode          varchar(3)      not null,
        
            sinknodeid            varchar(64)     not null constraint df_par_sinknode default (''),
        
            originalcorrelationid varchar(64)     not null,
        
            originalmessagesnapshot text  not null constraint df_par_snapshot default (''),
        
            status                varchar(16)     not null constraint df_par_status default ('Initiated'),
        
            createdat             timestamptz   not null constraint df_par_created default (clock_timestamp()),
        
            expiresat             timestamptz   not null,
        
            completedat           timestamptz   null,
        
            completedamount       decimal(18,4)    not null constraint df_par_completedamt default (0),
        
            completioncorrelationid varchar(64)   not null constraint df_par_completioncorr default ('')
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='preauthrecords' and indexname='ix_preauthrecords_rrn_source') THEN
        create index if not exists ix_preauthrecords_rrn_source on dbo.preauthrecords(rrn, sourcenodeid, status);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='preauthrecords' and indexname='ix_preauthrecords_expiresat') THEN
        create index if not exists ix_preauthrecords_expiresat on dbo.preauthrecords(expiresat) where status = 'Approved';
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Stand-in Processing Profiles

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.standinprofiles') is null THEN
        create table if not exists dbo.standinprofiles
        
        (
        
            id                    uuid not null constraint pk_standinprofiles primary key default gen_random_uuid(),
        
            profilecode           varchar(64)     not null,
        
            binprefix             varchar(8)      not null constraint df_sip_bin default (''),
        
            floorlimitamount      decimal(18,4)    not null,
        
            currencycode          varchar(3)      not null,
        
            velocitycountlimit    int              not null constraint df_sip_velcount default (3),
        
            velocitywindowseconds bigint           not null constraint df_sip_velwindow default (86400),
        
            eligibletransactiontypes varchar(500) not null constraint df_sip_txntypes default ('00'),
        
            isactive              boolean              not null constraint df_sip_active default (true),
        
            createdat             timestamptz   not null constraint df_sip_created default (clock_timestamp()),
        
            constraint ux_standinprofiles_bincode unique (binprefix, profilecode)
        
        );
    END IF;
END $$;

-- Default global stand-in profile (empty BIN prefix = catch-all)

DO $$
BEGIN
    IF not exists (select 1 from dbo.standinprofiles where binprefix = '' and profilecode = 'GLOBAL-DEFAULT') THEN
        insert into dbo.standinprofiles (profilecode, binprefix, floorlimitamount, currencycode, velocitycountlimit, velocitywindowseconds, eligibletransactiontypes, isactive)
        
        values ('GLOBAL-DEFAULT', '', 10000.00, '566', 3, 86400, '00', false); -- disabled by default, operators enable per policy
    END IF;
END $$;

-- ---------------------------------------------------------------

-- Distributed Idempotency Keys

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.idempotencykeys') is null THEN
        create table if not exists dbo.idempotencykeys
        
        (
        
            key         varchar(256)   not null constraint pk_idempotencykeys primary key,
        
            correlationid varchar(64)    not null,
        
            claimedat     timestamptz  not null constraint df_ik_claimedat default (clock_timestamp()),
        
            expiresat     timestamptz  not null
        
        );
    END IF;
END $$;

-- TTL-based cleanup: rows with ExpiresAt in the past can be purged by a maintenance job

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='idempotencykeys' and indexname='ix_idempotencykeys_expiresat') THEN
        create index if not exists ix_idempotencykeys_expiresat on dbo.idempotencykeys(expiresat);
    END IF;
END $$;

-- ===== 013_clearing_settlement_schema.sql =====

-- ============================================================

-- Migration 013 — Clearing & Settlement Engine Schema Completion

-- Fixes the clearing pipeline: adds IsCleared / ClearingBatchId /

-- SettlementProfile to dbo.TransactionLogs so the clearing engine

-- can identify uncleared transactions and mark them once batched.

-- Also creates dbo.NetSettlementPositions for the outbound

-- settlement engine net position calculation.

-- Apply after 001 through 012.

-- ============================================================

-- ---------------------------------------------------------------

-- dbo.TransactionLogs — clearing tracking columns

-- ---------------------------------------------------------------

-- SettlementProfile: copied from the sink node at transaction time.

-- Used by the clearing engine to group transactions per network

-- (VISA_NG, MASTERCARD_NG, VERVE_NIBSS, DEFAULT, etc.)

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='transactionlogs' and column_name='settlementprofile') THEN
            alter table dbo.transactionlogs
        
                add settlementprofile varchar(64) not null constraint df_tl_settlementprofile default ('');
    END IF;
END $$;

-- IsCleared: flipped to 1 when the clearing engine includes the

-- transaction in a ClearingBatch. Prevents double-clearing.

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='transactionlogs' and column_name='iscleared') THEN
            alter table dbo.transactionlogs
        
                add iscleared boolean not null constraint df_tl_iscleared default (false);
    END IF;
END $$;

-- ClearingBatchId: FK-style link to dbo.ClearingBatches once cleared.

-- NULL until the transaction is included in a batch.

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='transactionlogs' and column_name='clearingbatchid') THEN
            alter table dbo.transactionlogs
        
                add clearingbatchid uuid null;
    END IF;
END $$;

-- Index to make GetUnclearedTransactionsAsync efficient:

-- filters on IsCleared=false, approved ResponseCode, Mti, and business date

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='transactionlogs' and indexname='ix_tl_iscleared_profile_date') THEN
            create index if not exists ix_tl_iscleared_profile_date
        
                on dbo.transactionlogs (iscleared, settlementprofile, responsecode, mti, createdat)
        
                where iscleared=false;
    END IF;
END $$;

-- ---------------------------------------------------------------

-- dbo.NetSettlementPositions — outbound settlement generation

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.netsettlementpositions') is null THEN
        create table if not exists dbo.netsettlementpositions
        
        (
        
            id                       uuid not null constraint pk_netsettlementpositions primary key default gen_random_uuid(),
        
            institutioncode          varchar(64)     not null,
        
            settlementprofile        varchar(64)     not null,
        
            businessdate             date             not null,
        
            currencycode             varchar(3)      not null,
        
            grosspurchaseamount      decimal(18,4)    not null constraint df_nsp_purchase default (0),
        
            grossrefundamount        decimal(18,4)    not null constraint df_nsp_refund default (0),
        
            grossfeeamount           decimal(18,4)    not null constraint df_nsp_fee default (0),
        
            netsettlementamount      decimal(18,4)    not null constraint df_nsp_net default (0),
        
            direction                varchar(16)     not null,
        
            status                   varchar(32)     not null constraint df_nsp_status default ('Calculated'),
        
            transactioncount         int              not null constraint df_nsp_count default (0),
        
            nostrogljournalid        uuid null,
        
            settlementinstructionfile varchar(1000)  not null constraint df_nsp_file default (''),
        
            nostroreference          varchar(64)     not null constraint df_nsp_nostroref default (''),
        
            calculatedat             timestamptz   not null constraint df_nsp_calculated default (clock_timestamp()),
        
            glpostedat               timestamptz   null,
        
            instructiongeneratedat   timestamptz   null,
        
            constraint ux_nsp_profile_date_currency unique (settlementprofile, businessdate, currencycode)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='netsettlementpositions' and indexname='ix_nsp_date_status') THEN
            create index if not exists ix_nsp_date_status on dbo.netsettlementpositions(businessdate, status);
    END IF;
END $$;

-- ===== 014_b2_eft_chargeback_dispute_reconciliation.sql =====

-- ============================================================

-- Migration 014 — B2: EFT Rails, Chargeback, Dispute, Reconciliation

-- Apply after 001 through 013.

-- ============================================================

-- Extend EftTransfers for return / MMID / mandate fields

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='isreturn') THEN
            alter table dbo.efttransfers add isreturn boolean not null constraint df_eft_isreturn default(false);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='returnreasoncode') THEN
            alter table dbo.efttransfers add returnreasoncode varchar(8) not null constraint df_eft_returncode default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='originaltransferid') THEN
            alter table dbo.efttransfers add originaltransferid uuid null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='mmidnumber') THEN
            alter table dbo.efttransfers add mmidnumber varchar(7) not null constraint df_eft_mmid default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='mobilenumber') THEN
            alter table dbo.efttransfers add mobilenumber varchar(10) not null constraint df_eft_mobile default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='npcitransactionid') THEN
            alter table dbo.efttransfers add npcitransactionid varchar(64) not null constraint df_eft_npci default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='directdebitmandateid') THEN
            alter table dbo.efttransfers add directdebitmandateid uuid null;
    END IF;
END $$;

-- NEFT Batches

DO $$
BEGIN
    IF to_regclass('dbo.neftbatches') is null THEN
        create table if not exists dbo.neftbatches(
        
            id uuid not null constraint pk_neftbatches primary key default gen_random_uuid(),
        
            batchreference varchar(64) not null, cycleid varchar(32) not null,
        
            memberid varchar(4) not null constraint df_nb_member default(''),
        
            settlementdate date not null, sessionnumber int not null constraint df_nb_session default(0),
        
            recordcount int not null constraint df_nb_count default(0),
        
            totalamount decimal(18,4) not null constraint df_nb_total default(0),
        
            currencycode varchar(3) not null constraint df_nb_ccy default('356'),
        
            status varchar(32) not null constraint df_nb_status default('Draft'),
        
            filecontent text not null constraint df_nb_content default(''),
        
            outputfilepath varchar(500) not null constraint df_nb_path default(''),
        
            npciackreference varchar(64) not null constraint df_nb_ack default(''),
        
            createdat timestamptz not null constraint df_nb_created default(clock_timestamp()),
        
            submittedat timestamptz null, settledat timestamptz null);
    END IF;
END $$;

-- SWIFT Messages

DO $$
BEGIN
    IF to_regclass('dbo.swiftmessages') is null THEN
        create table if not exists dbo.swiftmessages(
        
            id uuid not null constraint pk_swiftmessages primary key default gen_random_uuid(),
        
            messagetype varchar(8) not null, status varchar(16) not null,
        
            efttransferid uuid not null,
        
            senderbic varchar(11) not null constraint df_sm_sender default(''),
        
            receiverbic varchar(11) not null constraint df_sm_rcvr default(''),
        
            transactionreference varchar(16) not null constraint df_sm_txnref default(''),
        
            valuedate varchar(6) not null constraint df_sm_valdate default(''),
        
            currencycode varchar(3) not null constraint df_sm_ccy default(''),
        
            amount decimal(18,4) not null constraint df_sm_amt default(0),
        
            rawmessagecontent text not null constraint df_sm_raw default(''),
        
            ackreference varchar(64) not null constraint df_sm_ackref default(''),
        
            createdat timestamptz not null constraint df_sm_created default(clock_timestamp()),
        
            sentat timestamptz null, acknowledgedat timestamptz null);
    END IF;
END $$;

-- ACH Files

DO $$
BEGIN
    IF to_regclass('dbo.achfiles') is null THEN
        create table if not exists dbo.achfiles(
        
            id uuid not null constraint pk_achfiles primary key default gen_random_uuid(),
        
            filereference varchar(64) not null, filetype varchar(16) not null,
        
            entrytype varchar(4) not null constraint df_af_entry default('CCD'),
        
            status varchar(16) not null constraint df_af_status default('Draft'),
        
            originatingdfi varchar(9) not null constraint df_af_dfi default(''),
        
            originatingcompanyid varchar(10) not null constraint df_af_coid default(''),
        
            originatingcompanyname varchar(16) not null constraint df_af_coname default(''),
        
            effectivedate date not null,
        
            recordcount int not null constraint df_af_count default(0),
        
            totaldebitamount decimal(18,4) not null constraint df_af_debit default(0),
        
            totalcreditamount decimal(18,4) not null constraint df_af_credit default(0),
        
            filecontent text not null constraint df_af_content default(''),
        
            outputfilepath varchar(500) not null constraint df_af_path default(''),
        
            createdat timestamptz not null constraint df_af_created default(clock_timestamp()),
        
            submittedat timestamptz null, settledat timestamptz null);
    END IF;
END $$;

-- Direct Debit Mandates

DO $$
BEGIN
    IF to_regclass('dbo.directdebitmandates') is null THEN
        create table if not exists dbo.directdebitmandates(
        
            id uuid not null constraint pk_directdebitmandates primary key default gen_random_uuid(),
        
            mandatereference varchar(30) not null, customernumber varchar(64) not null,
        
            customerid uuid not null,
        
            debtoraccountnumber varchar(20) not null, debtorifsccode varchar(11) not null,
        
            debtorbankname varchar(100) not null constraint df_ddm_debtorbank default(''),
        
            creditoraccountnumber varchar(20) not null, creditorifsccode varchar(11) not null,
        
            creditorname varchar(100) not null constraint df_ddm_credname default(''),
        
            maximumamount decimal(18,4) not null, currencycode varchar(3) not null constraint df_ddm_ccy default('356'),
        
            frequency varchar(16) not null, status varchar(16) not null,
        
            startdate date not null, enddate date null,
        
            createdat timestamptz not null constraint df_ddm_created default(clock_timestamp()),
        
            activatedat timestamptz null, cancelledat timestamptz null,
        
            cancellationreason varchar(500) not null constraint df_ddm_cancelreason default(''),
        
            lastchargeddate date null, successfuldebitcount int not null constraint df_ddm_debitcount default(0));
    END IF;
END $$;

-- Chargeback Reason Codes

DO $$
BEGIN
    IF to_regclass('dbo.chargebackreasoncodes') is null THEN
        create table if not exists dbo.chargebackreasoncodes(
        
            id uuid not null constraint pk_chargebackreasoncodes primary key default gen_random_uuid(),
        
            network varchar(16) not null, code varchar(16) not null,
        
            category varchar(64) not null constraint df_crc_cat default(''),
        
            description varchar(200) not null constraint df_crc_desc default(''),
        
            initialchargebackdays int not null constraint df_crc_initial default(120),
        
            representmentdays int not null constraint df_crc_repr default(45),
        
            prearbitrationdays int not null constraint df_crc_prearb default(45),
        
            arbitrationdays int not null constraint df_crc_arb default(10),
        
            representmentallowed boolean not null constraint df_crc_reprallowed default(true),
        
            isactive boolean not null constraint df_crc_active default(true),
        
            constraint ux_chargebackreasoncodes unique(network, code));
    END IF;
END $$;

-- Chargeback Cases

DO $$
BEGIN
    IF to_regclass('dbo.chargebackcases') is null THEN
        create table if not exists dbo.chargebackcases(
        
            id uuid not null constraint pk_chargebackcases primary key default gen_random_uuid(),
        
            casereference varchar(64) not null, network varchar(16) not null,
        
            stage varchar(32) not null, outcome varchar(32) not null constraint df_cc_outcome default('Pending'),
        
            originaltransactioncorrelationid varchar(64) not null constraint df_cc_orgcorr default(''),
        
            rrn varchar(12) not null constraint df_cc_rrn default(''),
        
            stan varchar(12) not null constraint df_cc_stan default(''),
        
            maskedpan varchar(32) not null constraint df_cc_pan default(''),
        
            panhash varchar(128) not null constraint df_cc_panhash default(''),
        
            transactionamount decimal(18,4) not null, chargebackamount decimal(18,4) not null,
        
            currencycode varchar(3) not null, transactiondate date not null,
        
            reasoncode varchar(16) not null constraint df_cc_code default(''),
        
            reasondescription varchar(200) not null constraint df_cc_desc default(''),
        
            networkcaseid varchar(64) not null constraint df_cc_netid default(''),
        
            merchantid varchar(64) not null constraint df_cc_merch default(''),
        
            chargebackreceiveddate date not null,
        
            representmentdeadline date not null,
        
            representmentsubmitteddate date null,
        
            prearbitrationdeadline date null, prearbitrationreceiveddate date null,
        
            arbitrationdeadline date null, arbitrationsubmitteddate date null, resolveddate date null,
        
            issuerevidencesummary text not null constraint df_cc_issev default(''),
        
            acquirerevidencesummary text not null constraint df_cc_acqev default(''),
        
            resolutionnotes text not null constraint df_cc_resnotes default(''),
        
            createdat timestamptz not null constraint df_cc_created default(clock_timestamp()),
        
            updatedat timestamptz null, lastupdatedby varchar(128) not null constraint df_cc_updatedby default(''));
    END IF;
END $$;

create index if not exists ix_chargebackcases_stage on dbo.chargebackcases(stage, network);

-- Customer Disputes

DO $$
BEGIN
    IF to_regclass('dbo.customerdisputes') is null THEN
        create table if not exists dbo.customerdisputes(
        
            id uuid not null constraint pk_customerdisputes primary key default gen_random_uuid(),
        
            disputereference varchar(32) not null, customernumber varchar(64) not null,
        
            customerid uuid not null, disputetype varchar(32) not null,
        
            status varchar(32) not null, rrn varchar(12) not null constraint df_cd_rrn default(''),
        
            stan varchar(12) not null constraint df_cd_stan default(''),
        
            maskedpan varchar(32) not null constraint df_cd_pan default(''),
        
            disputedamount decimal(18,4) not null, currencycode varchar(3) not null,
        
            transactiondate date not null, merchantname varchar(200) not null constraint df_cd_merch default(''),
        
            customerstatement text not null constraint df_cd_stmt default(''),
        
            internalnotes text not null constraint df_cd_notes default(''),
        
            channel varchar(32) not null constraint df_cd_channel default(''),
        
            linkedchargebackid uuid null,
        
            awardedamount decimal(18,4) null, resolutionnotes text not null constraint df_cd_resnotes default(''),
        
            receivedat timestamptz not null constraint df_cd_received default(clock_timestamp()),
        
            evidencedeadline timestamptz null, escalatedat timestamptz null,
        
            resolvedat timestamptz null, updatedat timestamptz null);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.disputeevidence') is null THEN
        create table if not exists dbo.disputeevidence(
        
            id uuid not null constraint pk_disputeevidence primary key default gen_random_uuid(),
        
            disputeid uuid not null, evidencetype varchar(32) not null,
        
            description varchar(500) not null constraint df_de_desc default(''),
        
            documentvaultreference varchar(500) not null constraint df_de_vault default(''),
        
            submittedby varchar(128) not null constraint df_de_by default(''),
        
            submittedbyrole varchar(64) not null constraint df_de_role default(''),
        
            submittedat timestamptz not null constraint df_de_at default(clock_timestamp()));
    END IF;
END $$;

create index if not exists ix_disputeevidence_dispute on dbo.disputeevidence(disputeid);

-- Reconciliation Runs

DO $$
BEGIN
    IF to_regclass('dbo.reconciliationruns') is null THEN
        create table if not exists dbo.reconciliationruns(
        
            id uuid not null constraint pk_reconciliationruns primary key default gen_random_uuid(),
        
            businessdate date not null, status varchar(32) not null,
        
            transactionlogcount int not null constraint df_rr_txcount default(0),
        
            ledgerentrycount int not null constraint df_rr_ledcount default(0),
        
            gljournallinecount int not null constraint df_rr_glcount default(0),
        
            clearingrecordcount int not null constraint df_rr_clcount default(0),
        
            matchedcount int not null constraint df_rr_matched default(0),
        
            breakcount int not null constraint df_rr_breaks default(0),
        
            totalswitchamount decimal(18,4) not null constraint df_rr_swamt default(0),
        
            totalledgeramount decimal(18,4) not null constraint df_rr_ledamt default(0),
        
            totalglamount decimal(18,4) not null constraint df_rr_glamt default(0),
        
            runtrigger varchar(32) not null constraint df_rr_trigger default('Scheduled'),
        
            triggeredby varchar(128) not null constraint df_rr_by default(''),
        
            startedat timestamptz not null constraint df_rr_started default(clock_timestamp()),
        
            completedat timestamptz null);
    END IF;
END $$;

create index if not exists ix_reconciliationruns_date on dbo.reconciliationruns(businessdate, startedat desc);

-- Reconciliation Breaks

DO $$
BEGIN
    IF to_regclass('dbo.reconciliationbreaks') is null THEN
        create table if not exists dbo.reconciliationbreaks(
        
            id uuid not null constraint pk_reconciliationbreaks primary key default gen_random_uuid(),
        
            reconciliationrunid uuid not null,
        
            breaktype varchar(64) not null, correlationid varchar(64) not null constraint df_rb_corr default(''),
        
            rrn varchar(12) not null constraint df_rb_rrn default(''),
        
            switchamount decimal(18,4) null, ledgeramount decimal(18,4) null,
        
            glamount decimal(18,4) null, clearingamount decimal(18,4) null,
        
            description text not null constraint df_rb_desc default(''),
        
            isresolved boolean not null constraint df_rb_resolved default(false),
        
            resolutionnotes varchar(500) not null constraint df_rb_resnotes default(''),
        
            detectedat timestamptz not null constraint df_rb_detected default(clock_timestamp()),
        
            resolvedat timestamptz null,
        
            constraint fk_reconciliationbreaks_run foreign key(reconciliationrunid) references dbo.reconciliationruns(id));
    END IF;
END $$;

create index if not exists ix_reconciliationbreaks_run on dbo.reconciliationbreaks(reconciliationrunid, isresolved);

-- ===== 015_security_controls_schema.sql =====

-- ============================================================

-- Migration 015 — B3 Security Controls Schema

-- TOTP enrollment, PCI DSS control results, HSM lifecycle,

-- DUKPT key state, and key rotation audit tables.

-- Apply after 001 through 014.

-- ============================================================

-- ---------------------------------------------------------------

-- TOTP / MFA Enrollment (RFC 6238)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.totpenrollments') is null THEN
        create table if not exists dbo.totpenrollments
        
        (
        
            id                    uuid not null constraint pk_totpenrollments primary key default gen_random_uuid(),
        
            userid                varchar(128)    not null,
        
            username              varchar(256)    not null,
        
            encryptedsecret       varchar(2000)   not null,     -- AES-256-GCM encrypted base32 seed
        
            algorithm             varchar(16)     not null constraint df_te_algo default ('HmacSha1'),
        
            digits                int              not null constraint df_te_digits default (6),
        
            periodseconds         int              not null constraint df_te_period default (30),
        
            status                varchar(16)     not null constraint df_te_status default ('NotEnrolled'),
        
            issuername            varchar(128)    not null constraint df_te_issuer default ('BankSwitch'),
        
            encryptedbackupcodes  varchar(4000)   not null constraint df_te_backup default (''),
        
            backupcodesremaining  int              not null constraint df_te_backupcount default (8),
        
            enrolledat            timestamptz   not null constraint df_te_enrolledat default (clock_timestamp()),
        
            verifiedat            timestamptz   null,
        
            lastusedat            timestamptz   null,
        
            lastvalidatedcounter  bigint           null,
        
            constraint ux_totpenrollments_userid unique (userid)
        
        );
    END IF;
END $$;

-- ---------------------------------------------------------------

-- PCI DSS v4.0 Control Results

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.pcicontrolresults') is null THEN
        create table if not exists dbo.pcicontrolresults
        
        (
        
            id                    uuid not null constraint pk_pcicontrolresults primary key default gen_random_uuid(),
        
            requirementcode       varchar(16)     not null,
        
            category              varchar(64)     not null,
        
            title                 varchar(256)    not null,
        
            description           varchar(1000)   not null,
        
            status                varchar(32)     not null,
        
            evidence              varchar(2000)   not null constraint df_pcr_evidence default (''),
        
            remediationguidance   varchar(2000)   not null constraint df_pcr_remediation default (''),
        
            evaluatedat           timestamptz   not null constraint df_pcr_evalat default (clock_timestamp()),
        
            evaluatedby           varchar(64)     not null constraint df_pcr_evalby default ('AutomaticScan')
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='pcicontrolresults' and indexname='ix_pcicontrolresults_code_date') THEN
            create index if not exists ix_pcicontrolresults_code_date on dbo.pcicontrolresults(requirementcode, evaluatedat desc);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- HSM Partition Snapshots

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.hsmpartitionsnapshots') is null THEN
        create table if not exists dbo.hsmpartitionsnapshots
        
        (
        
            id                    uuid not null constraint pk_hsmpartitionsnapshots primary key default gen_random_uuid(),
        
            partitionname         varchar(64)     not null,
        
            hsmserialnumber       varchar(64)     not null constraint df_hps_serial default (''),
        
            status                varchar(16)     not null,
        
            loadedkeycount        int              not null constraint df_hps_keys default (0),
        
            freekeyslots          int              not null constraint df_hps_free default (0),
        
            firmwareversion       varchar(32)     not null constraint df_hps_fw default (''),
        
            tamperstatus          varchar(32)     not null constraint df_hps_tamper default (''),
        
            diagnosticlog         varchar(2000)   not null constraint df_hps_diag default (''),
        
            snapshottakenat       timestamptz   not null constraint df_hps_snapshotat default (clock_timestamp())
        
        );
    END IF;
END $$;

-- ---------------------------------------------------------------

-- HSM Key Load Events (PCI DSS Req 3.6 — dual custodian audit)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.hsmkeyloadevents') is null THEN
        create table if not exists dbo.hsmkeyloadevents
        
        (
        
            id                    uuid not null constraint pk_hsmkeyloadevents primary key default gen_random_uuid(),
        
            keyprofilecode        varchar(64)     not null,
        
            hsmpartitionname      varchar(64)     not null,
        
            eventtype             varchar(32)     not null,
        
            custodian1            varchar(128)    not null,     -- First custodian (dual-control)
        
            custodian2            varchar(128)    not null,     -- Second custodian (dual-control)
        
            purpose               varchar(256)    not null constraint df_hkl_purpose default (''),
        
            keycheckvalue         varchar(16)     not null,
        
            encryptedkeyunderlmk  varchar(2000)   not null constraint df_hkl_enc default (''),
        
            correlationid         varchar(64)     not null,
        
            occurredat            timestamptz   not null constraint df_hkl_at default (clock_timestamp())
        
        );
    END IF;
END $$;

-- Key load events are immutable — no UPDATE allowed (enforced by application layer)

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='hsmkeyloadevents' and indexname='ix_hsmkeyloadevents_profile_date') THEN
            create index if not exists ix_hsmkeyloadevents_profile_date on dbo.hsmkeyloadevents(keyprofilecode, occurredat desc);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- DUKPT Key State (ANSI X9.24-1 terminal key tracking)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.dukptkeystates') is null THEN
        create table if not exists dbo.dukptkeystates
        
        (
        
            id                    uuid not null constraint pk_dukptkeystates primary key default gen_random_uuid(),
        
            terminalid            varchar(64)     not null,
        
            keyserialnumber       varchar(20)     not null,     -- 10-byte KSN as 20 hex chars
        
            basederivationkeyid   varchar(64)     not null,
        
            keytype               varchar(16)     not null constraint df_dks_type default ('Tdes2Key'),
        
            usage                 varchar(32)     not null constraint df_dks_usage default ('PinEncryption'),
        
            transactioncounter    bigint           not null constraint df_dks_counter default (0),
        
            exhaustedshiftcount   int              not null constraint df_dks_shifts default (0),
        
            isexhausted           boolean              not null constraint df_dks_exhausted default (false),
        
            lastkcv               varchar(16)     not null constraint df_dks_kcv default (''),
        
            createdat             timestamptz   not null constraint df_dks_created default (clock_timestamp()),
        
            lastusedat            timestamptz   null,
        
            exhaustedat           timestamptz   null,
        
            constraint ux_dukptkeystates_terminalid unique (terminalid)
        
        );
    END IF;
END $$;

-- ===== 016_financial_processing_b4.sql =====

-- ============================================================

-- Migration 016 — B4 Financial Processing: Immutable Ledger,

-- Chart of Accounts Balances, GL Periods (End-of-Day)

-- Apply after 001 through 015.

-- ============================================================

-- ---------------------------------------------------------------

-- GL Journal Entries — hash chain columns (B4: Immutable Ledger)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='businessdate') THEN
            alter table dbo.gljournalentries add businessdate date not null constraint df_gje_businessdate default (cast(clock_timestamp() as date));
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='chainsequence') THEN
            alter table dbo.gljournalentries add chainsequence bigint not null constraint df_gje_seq default (0);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='previoushash') THEN
            alter table dbo.gljournalentries add previoushash varchar(64) not null constraint df_gje_prevhash default ('GENESIS');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='entryhash') THEN
            alter table dbo.gljournalentries add entryhash varchar(64) not null constraint df_gje_hash default ('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='reversesjournalid') THEN
            alter table dbo.gljournalentries add reversesjournalid uuid null;
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='isvoided') THEN
            alter table dbo.gljournalentries add isvoided boolean not null constraint df_gje_voided default (false);
    END IF;
END $$;

-- Index for chain verification by date and sequence

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='gljournalentries' and indexname='ix_gje_businessdate_seq') THEN
            create unique index if not exists ix_gje_businessdate_seq on dbo.gljournalentries(chainsequence) where chainsequence > 0;
    END IF;
END $$;

-- ---------------------------------------------------------------

-- GL Account Balances (running balances per account per day)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.glaccountbalances') is null THEN
        create table if not exists dbo.glaccountbalances
        
        (
        
            id                uuid not null constraint pk_glaccountbalances primary key default gen_random_uuid(),
        
            accountcode       varchar(64)     not null,
        
            currencycode      varchar(3)      not null,
        
            balancedate       date             not null,
        
            openingbalance    decimal(18,4)    not null constraint df_gab_open default (0),
        
            totaldebits       decimal(18,4)    not null constraint df_gab_dr default (0),
        
            totalcredits      decimal(18,4)    not null constraint df_gab_cr default (0),
        
            closingbalance    decimal(18,4)    not null constraint df_gab_close default (0),
        
            journallinecount  int              not null constraint df_gab_linecount default (0),
        
            lastupdatedat     timestamptz   not null constraint df_gab_updated default (clock_timestamp()),
        
            constraint ux_glaccountbalances unique (accountcode, balancedate)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='glaccountbalances' and indexname='ix_gab_date') THEN
            create index if not exists ix_gab_date on dbo.glaccountbalances(balancedate, accountcode);
    END IF;
END $$;

-- ---------------------------------------------------------------

-- GL Periods (End-of-Day accounting periods)

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF to_regclass('dbo.glperiods') is null THEN
        create table if not exists dbo.glperiods
        
        (
        
            id                    uuid not null constraint pk_glperiods primary key default gen_random_uuid(),
        
            businessdate          date             not null constraint ux_glperiods_date unique,
        
            status                varchar(16)     not null constraint df_gp_status default ('Open'),
        
            currencycode          varchar(3)      not null constraint df_gp_ccy default ('566'),
        
            openingdebittotal     decimal(18,4)    not null constraint df_gp_opendr default (0),
        
            openingcredittotal    decimal(18,4)    not null constraint df_gp_opencr default (0),
        
            closingdebittotal     decimal(18,4)    not null constraint df_gp_closedr default (0),
        
            closingcredittotal    decimal(18,4)    not null constraint df_gp_closecr default (0),
        
            journalcount          int              not null constraint df_gp_journals default (0),
        
            openedby              varchar(128)    not null constraint df_gp_openedby default (''),
        
            closedby              varchar(128)    not null constraint df_gp_closedby default (''),
        
            openedat              timestamptz   not null constraint df_gp_openedat default (clock_timestamp()),
        
            closedat              timestamptz   null,
        
            periodclosehash       varchar(64)     not null constraint df_gp_hash default ('')
        
        );
    END IF;
END $$;

-- ===== 017_performance_optimization.sql =====
-- PostgreSQL equivalent of the SQL Server partition/archive/performance migration.
-- The source defined a SQL Server partition function and scheme but did not bind
-- TransactionLogs to that scheme. PostgreSQL therefore retains the monthly boundary
-- metadata as documentation rather than creating an unrelated extra table/object.
-- Monthly boundaries from the source: 2024-01-01 through 2026-07-01.

create table if not exists dbo.transactionlogsarchive
(
    id uuid not null constraint pk_tla primary key,
    correlationid varchar(64) not null,
    mti varchar(4) not null,
    sourcenodeid varchar(64) not null,
    sinknodeid varchar(64) not null,
    maskedpan varchar(32) not null,
    pantoken varchar(64) not null,
    panhash varchar(128) not null,
    stan varchar(12) not null,
    rrn varchar(12) not null,
    amount numeric(18,4) not null,
    currencycode varchar(3) not null,
    responsecode varchar(4) not null,
    latencymilliseconds bigint not null,
    routeused varchar(64) not null,
    schemeused varchar(64) not null,
    feeapplied varchar(64) not null,
    reversalstate varchar(16) not null,
    macvalidationstatus varchar(32) not null,
    settlementprofile varchar(64) not null,
    iscleared boolean not null,
    clearingbatchid uuid null,
    createdat timestamptz not null,
    archivedat timestamptz not null default (clock_timestamp())
);

drop procedure if exists dbo.usp_archivetransactionlogs(integer, integer, integer, integer);

create or replace procedure dbo.usp_archivetransactionlogs(
    in retentiondays integer default 90,
    in archiveretentiondays integer default 730,
    in batchsize integer default 10000,
    in maxbatches integer default 100
)
language plpgsql
as $$
declare
    cutoff timestamptz := clock_timestamp() - make_interval(days => retentiondays);
    archivecutoff timestamptz := clock_timestamp() - make_interval(days => archiveretentiondays);
    batch integer := 0;
    moved integer := 0;
begin
    while batch < maxbatches loop
        with moved_rows as (
            select id
            from dbo.transactionlogs
            where createdat < cutoff
            limit batchsize
            for update skip locked
        ), inserted as (
            insert into dbo.transactionlogsarchive
            (id, correlationid, mti, sourcenodeid, sinknodeid, maskedpan, pantoken, panhash,
             stan, rrn, amount, currencycode, responsecode, latencymilliseconds, routeused,
             schemeused, feeapplied, reversalstate, macvalidationstatus, settlementprofile,
             iscleared, clearingbatchid, createdat)
            select t.id, t.correlationid, t.mti, t.sourcenodeid, t.sinknodeid, t.maskedpan, t.pantoken, t.panhash,
                   t.stan, t.rrn, t.amount, t.currencycode, t.responsecode, t.latencymilliseconds, t.routeused,
                   t.schemeused, t.feeapplied, t.reversalstate, t.macvalidationstatus, t.settlementprofile,
                   t.iscleared, t.clearingbatchid, t.createdat
            from dbo.transactionlogs t
            join moved_rows m on m.id = t.id
            returning id
        )
        delete from dbo.transactionlogs t
        using inserted i
        where t.id = i.id;

        get diagnostics moved = row_count;
        exit when moved = 0;
        batch := batch + 1;
        perform pg_sleep(0.1);
    end loop;

    loop
        delete from dbo.transactionlogsarchive
        where ctid in (
            select ctid from dbo.transactionlogsarchive
            where createdat < archivecutoff
            limit batchsize
        );
        get diagnostics moved = row_count;
        exit when moved = 0;
        perform pg_sleep(0.1);
    end loop;
end;
$$;

create index if not exists ix_tl_duplicate_check
    on dbo.transactionlogs (sourcenodeid, stan, rrn, createdat)
    include (amount, responsecode)
    with (fillfactor = 90);

CREATE INDEX IF NOT EXISTS ix_tl_clearing_query
    on dbo.transactionlogs (iscleared, settlementprofile, createdat)
    INCLUDE (id, correlationid, amount, currencycode, responsecode, mti)
    WITH (fillfactor = 85)
    WHERE iscleared = false;
	
create index if not exists ix_tl_approved_bydate
    on dbo.transactionlogs (createdat, responsecode, mti)
    include (id, correlationid, sourcenodeid, amount, currencycode, settlementprofile, iscleared)	
    with (fillfactor = 85)
    where responsecode in ('00','08','10','11') and mti in ('0200','0210');

create index if not exists ix_gje_chainseq
    on dbo.gljournalentries (chainsequence, businessdate)
    include (journalnumber, debittotal, credittotal, previoushash, entryhash, postedat)
    with (fillfactor = 90);

-- ===== 018_b7_compliance_schema.sql =====

-- ============================================================

-- Migration 018 — B7 Compliance: AML Reports, Fraud Baselines,

--                 OWASP Results, ISO 27001, Audit Evidence

-- ============================================================

-- Applied by: BankSwitch v25 B7 Compliance Implementation

-- ─── AML Regulatory Reports ─────────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.cashtransactionreports') is null THEN

            create table if not exists dbo.cashtransactionreports (

                id              uuid not null default gen_random_uuid() constraint pk_cashtransactionreports primary key,

                reportnumber    varchar(64)     not null,

                customernumber  varchar(32)     not null,

                customername    varchar(200)    not null,

                aggregateamount decimal(18,4)    not null,

                currencycode    varchar(3)      not null,

                reportdate      date             not null,

                status          varchar(20)     not null default 'Draft',

                fiureference    varchar(64)     not null default '',

                reportjson      text    not null default '{}',

                transactionids  text    not null default '[]',

                generatedat     timestamptz   not null default clock_timestamp(),

                filedat         timestamptz       null,

                generatedby     varchar(100)    not null,

                constraint uq_cashtransactionreports_number unique (reportnumber)

            );

            create index if not exists ix_cashtransactionreports_customer on dbo.cashtransactionreports (customernumber, reportdate desc);

            create index if not exists ix_cashtransactionreports_status   on dbo.cashtransactionreports (status) where status in ('Draft');

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.suspiciousactivityreports') is null THEN

            create table if not exists dbo.suspiciousactivityreports (

                id                            uuid not null default gen_random_uuid() constraint pk_suspiciousactivityreports primary key,

                reportnumber                  varchar(64)     not null,

                customernumber                varchar(32)     not null,

                customername                  varchar(200)    not null,

                suspiciousactivitydescription varchar(2000)   not null,

                patterncategory               varchar(100)    not null,

                totalamountinvolved           decimal(18,4)    not null default 0,

                currencycode                  varchar(3)      not null default '566',

                activitystartdate             date             not null,

                activityenddate               date             not null,

                status                        varchar(20)     not null default 'Draft',

                fiureference                  varchar(64)     not null default '',

                reportjson                    text    not null default '{}',

                generatedat                   timestamptz   not null default clock_timestamp(),

                filedat                       timestamptz       null,

                generatedby                   varchar(100)    not null,

                constraint uq_suspiciousactivityreports_number unique (reportnumber)

            );

            create index if not exists ix_suspiciousactivityreports_customer on dbo.suspiciousactivityreports (customernumber, generatedat desc);

            create index if not exists ix_suspiciousactivityreports_status   on dbo.suspiciousactivityreports (status) where status in ('Draft', 'UnderReview');

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.amlfeedsnapshots') is null THEN

            create table if not exists dbo.amlfeedsnapshots (

                id            uuid not null default gen_random_uuid() constraint pk_amlfeedsnapshots primary key,

                source        varchar(50)     not null,

                feedurl       varchar(500)    not null,

                entriesadded  int              not null default 0,

                entriesremoved int             not null default 0,

                totalentries  int              not null default 0,

                issuccess     boolean              not null default true,

                errormessage  varchar(500)    not null default '',

                fetchedat     timestamptz   not null default clock_timestamp()

            );

            create index if not exists ix_amlfeedsnapshots_source on dbo.amlfeedsnapshots (source, fetchedat desc);

    END IF;
END $$;

-- ─── Fraud — Behavioral Baselines ────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.cardbehavioralbaselines') is null THEN

            create table if not exists dbo.cardbehavioralbaselines (

                id                       uuid not null default gen_random_uuid() constraint pk_cardbehavioralbaselines primary key,

                panhash                  varchar(64)     not null,

                avgtransactionamount     decimal(18,4)    not null default 0,

                stddevtransactionamount  decimal(18,4)    not null default 0,

                avgdailyspend            decimal(18,4)    not null default 0,

                avgdailytransactioncount int              not null default 0,

                mostfrequentmcc          varchar(4)      not null default '',

                mostfrequentcountry      varchar(2)      not null default '',

                typicalactivehours       varchar(100)    not null default '',

                totaltransactionsanalyzed int             not null default 0,

                baselinestartdate        timestamptz   not null default clock_timestamp(),

                lastupdatedat            timestamptz   not null default clock_timestamp(),

                constraint uq_cardbehavioralbaselines_panhash unique (panhash)

            );

    END IF;
END $$;

-- ─── OWASP ASVS Results ──────────────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.owaspcontrolresults') is null THEN

            create table if not exists dbo.owaspcontrolresults (

                id                   uuid not null default gen_random_uuid() constraint pk_owaspcontrolresults primary key,

                requirementid        varchar(20)     not null,

                chapter              varchar(100)    not null,

                title                varchar(300)    not null,

                level                int              not null,

                status               varchar(40)     not null,

                evidence             varchar(1000)   not null default '',

                remediationguidance  varchar(500)    not null default '',

                evaluatedat          timestamptz   not null default clock_timestamp()

            );

            create index if not exists ix_owaspcontrolresults_status      on dbo.owaspcontrolresults (status, evaluatedat desc);

            create index if not exists ix_owaspcontrolresults_requirement on dbo.owaspcontrolresults (requirementid, evaluatedat desc);

    END IF;
END $$;

-- ─── ISO 27001:2022 Risk Register ─────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.iso27001riskentries') is null THEN

            create table if not exists dbo.iso27001riskentries (

                id                  uuid not null default gen_random_uuid() constraint pk_iso27001riskentries primary key,

                riskid              varchar(20)     not null,

                assetname           varchar(200)    not null,

                threatdescription   varchar(500)    not null,

                vulnerability       varchar(500)    not null,

                likelihood          int              not null,

                impact              int              not null,

                treatment           varchar(20)     not null,

                controlmeasures     varchar(1000)   not null default '',

                residualriskscore   int              not null default 0,

                riskowner           varchar(100)    not null,

                reviewdate          date             not null,

                createdat           timestamptz   not null default clock_timestamp(),

                constraint uq_iso27001riskentries_riskid unique (riskid)

            );

            create index if not exists ix_iso27001riskentries_score on dbo.iso27001riskentries (likelihood, impact desc);

    END IF;
END $$;

-- ─── ISO 27001 Statement of Applicability ─────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.iso27001controlevaluations') is null THEN

            create table if not exists dbo.iso27001controlevaluations (

                id                       uuid not null default gen_random_uuid() constraint pk_iso27001controlevaluations primary key,

                controlid                varchar(20)     not null,

                controlname              varchar(200)    not null,

                domain                   varchar(100)    not null,

                isapplicable             boolean              not null default true,

                exclusionjustification   varchar(500)    not null default '',

                status                   varchar(40)     not null default 'NotImplemented',

                implementationevidence   varchar(1000)   not null default '',

                nextreviewdate           date             not null,

                constraint uq_iso27001controlevaluations_controlid unique (controlid)

            );

            create index if not exists ix_iso27001controlevaluations_status on dbo.iso27001controlevaluations (status, isapplicable);

    END IF;
END $$;

-- ─── Audit Evidence Packages ─────────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.auditevidencepackages') is null THEN

            create table if not exists dbo.auditevidencepackages (

                id            uuid not null default gen_random_uuid() constraint pk_auditevidencepackages primary key,

                packageid     varchar(100)    not null,

                title         varchar(500)    not null,

                periodfrom    date             not null,

                periodto      date             not null,

                artifactsjson text    not null default '[]',

                manifesthash  varchar(64)     not null,

                generatedby   varchar(100)    not null,

                generatedat   timestamptz   not null default clock_timestamp(),

                constraint uq_auditevidencepackages_packageid unique (packageid)

            );

            create index if not exists ix_auditevidencepackages_period on dbo.auditevidencepackages (periodfrom, periodto desc);

    END IF;
END $$;

-- ===== 019_advanced_routing_criteria.sql =====

/*DO $$
BEGIN
    IF exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='routes' AND indexname='ux_routes_binprefix_active') THEN
            drop index ux_routes_binprefix_active on dbo.routes;
    END IF;
END $$;*/

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'dbo'
          AND tablename = 'routes'
          AND indexname = 'ux_routes_binprefix_active'
    ) THEN

        DROP INDEX dbo.ux_routes_binprefix_active;

    END IF;
END $$;

/* v26 - Advanced Tier-1 routing criteria for EFT Switch
   Adds routing by country, MCC, currency, device, interchange, card range, institution,
   product, network and account number while keeping legacy BIN routing compatible.
*/

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='priority') THEN
            alter table dbo.routes add priority int not null constraint df_routes_priority default(0);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='countrycodes') THEN
            alter table dbo.routes add countrycodes text not null constraint df_routes_countrycodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='merchantcategorycodes') THEN
            alter table dbo.routes add merchantcategorycodes text not null constraint df_routes_mcc default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='currencycodes') THEN
            alter table dbo.routes add currencycodes text not null constraint df_routes_currencycodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='devicecodes') THEN
            alter table dbo.routes add devicecodes text not null constraint df_routes_devicecodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='interchangecodes') THEN
            alter table dbo.routes add interchangecodes text not null constraint df_routes_interchangecodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='cardrangeprefixes') THEN
            alter table dbo.routes add cardrangeprefixes text not null constraint df_routes_cardrangeprefixes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='institutioncodes') THEN
            alter table dbo.routes add institutioncodes text not null constraint df_routes_institutioncodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='productcodes') THEN
            alter table dbo.routes add productcodes text not null constraint df_routes_productcodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='networkcodes') THEN
            alter table dbo.routes add networkcodes text not null constraint df_routes_networkcodes default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='accountranges') THEN
            alter table dbo.routes add accountranges text not null constraint df_routes_accountranges default('');
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='routes' and indexname='ix_routes_advancedlookup') THEN
            create index if not exists ix_routes_advancedlookup on dbo.routes(isactive, priority desc, binprefix);
    END IF;
END $$;

-- ===== 020_debit_card_production_lifecycle.sql =====

-- V27: Enterprise debit card production lifecycle

-- Covers full production order workflow, embossing files, PIN mailer files,

-- personalization bureau integration, instant branch card stock, virtual debit card

-- audit trail, and hotlist propagation to card networks.

create table if not exists dbo.debitcardproductionorders (

    id uuid not null primary key,

    ordernumber varchar(64) not null unique,

    productiontype varchar(40) not null,

    status varchar(40) not null,

    customerid uuid not null,

    customernumber varchar(64) not null,

    productid uuid not null,

    productcode varchar(64) not null,

    cardid uuid null,

    oldcardid uuid null,

    branchstockitemid uuid null,

    maskedpan varchar(32) not null,

    embossname varchar(64) not null,

    branchcode varchar(32) not null,

    deliveryaddress varchar(512) not null,

    bureaucode varchar(64) not null,

    correlationid varchar(64) not null,

    notes varchar(1024) not null default '',

    createdat timestamptz not null,

    updatedat timestamptz null

);

create index if not exists ix_debitcardproductionorders_status on dbo.debitcardproductionorders(status);

create index if not exists ix_debitcardproductionorders_customernumber on dbo.debitcardproductionorders(customernumber);

create index if not exists ix_debitcardproductionorders_cardid on dbo.debitcardproductionorders(cardid);

create table if not exists dbo.debitcardbranchstockitems (

    id uuid not null primary key,

    branchcode varchar(32) not null,

    productcode varchar(64) not null,

    stockreference varchar(64) not null unique,

    maskedpan varchar(32) not null,

    pantoken text not null,

    panhash varchar(128) not null,

    status varchar(40) not null,

    assignedcustomerid uuid null,

    assignedcardid uuid null,

    assignedby varchar(128) not null default '',

    createdat timestamptz not null,

    assignedat timestamptz null

);

create index if not exists ix_debitcardbranchstock_branchproductstatus on dbo.debitcardbranchstockitems(branchcode, productcode, status);

create table if not exists dbo.debitcardbureaufiles (

    id uuid not null primary key,

    filereference varchar(128) not null unique,

    filetype varchar(40) not null,

    status varchar(40) not null,

    bureaucode varchar(64) not null,

    filename varchar(260) not null,

    contenthash varchar(128) not null,

    encryptedpayloadreference text not null,

    productionorderids text not null,

    recordcount int not null,

    generatedat timestamptz not null,

    sentat timestamptz null,

    acknowledgedat timestamptz null,

    ackreference varchar(128) not null default '',

    rejectionreason varchar(1024) not null default ''

);

create index if not exists ix_debitcardbureaufiles_typestatus on dbo.debitcardbureaufiles(filetype, status);

create table if not exists dbo.hotlistpropagationevents (

    id uuid not null primary key,

    cardid uuid not null,

    maskedpan varchar(32) not null,

    panhash varchar(128) not null,

    network varchar(40) not null,

    reason varchar(40) not null,

    status varchar(40) not null,

    attemptcount int not null,

    networkreference varchar(128) not null default '',

    errormessage varchar(1024) not null default '',

    createdat timestamptz not null,

    lastattemptat timestamptz null,

    acknowledgedat timestamptz null

);

create index if not exists ix_hotlistpropagationevents_cardnetwork on dbo.hotlistpropagationevents(cardid, network);

create index if not exists ix_hotlistpropagationevents_status on dbo.hotlistpropagationevents(status);

-- ===== 021_network_settlement_clearing_gl.sql =====

-- v28 Network Settlement, Clearing and GL certification schema

-- Adds Visa/Mastercard/RuPay/NPCI settlement evidence, interchange fee rule engine,

-- and RBI/NPCI audit controls.

DO $$
BEGIN
    IF to_regclass('dbo.interchangefeerule') is null THEN

            create table if not exists dbo.interchangefeerule (

                id uuid not null constraint pk_interchangefeerule primary key,

                rulecode varchar(64) not null,

                network varchar(32) not null,

                productcode varchar(32) not null default '*',

                channelcode varchar(32) not null default '*',

                merchantcategorycode varchar(16) not null default '*',

                countrycode varchar(8) not null default '*',

                currencycode varchar(8) not null default '*',

                transactiontypecode varchar(32) not null default '*',

                flatfee decimal(18,4) not null default 0,

                percentfee decimal(9,4) not null default 0,

                minimumfee decimal(18,4) not null default 0,

                maximumfee decimal(18,4) not null default 0,

                direction varchar(32) not null,

                effectivefrom date not null,

                effectiveto date null,

                isactive boolean not null default true,

                priority int not null default 0,

                createdat timestamptz not null default clock_timestamp()

            );

            create unique index if not exists ux_interchangefeerule_rulecode on dbo.interchangefeerule(rulecode);

            create index if not exists ix_interchangefeerule_lookup on dbo.interchangefeerule(network, effectivefrom, effectiveto, productcode, channelcode, merchantcategorycode, countrycode, currencycode, transactiontypecode, isactive);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.networksettlementrun') is null THEN

            create table if not exists dbo.networksettlementrun (

                id uuid not null constraint pk_networksettlementrun primary key,

                clearingbatchid uuid not null,

                network varchar(32) not null,

                settlementcycle varchar(96) not null,

                filename varchar(260) not null,

                filehashsha256 varchar(64) not null,

                filesizebytes bigint not null,

                certificationstatus varchar(32) not null,

                validationreport text not null default '',

                transmissionreference varchar(128) not null default '',

                createdat timestamptz not null default clock_timestamp(),

                submittedat timestamptz null,

                acceptedat timestamptz null

            );

            create unique index if not exists ux_networksettlementrun_batch on dbo.networksettlementrun(clearingbatchid);

            create index if not exists ix_networksettlementrun_networkcycle on dbo.networksettlementrun(network, settlementcycle, certificationstatus);

    END IF;
END $$;

/*DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='clearingrecord' and column_name='feeamount') THEN

            alter table dbo.clearingrecord add feeamount decimal(18,4) not null constraint df_clearingrecord_feeamount default 0;

    END IF;
END $$;*/

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'clearingrecords'
          AND column_name = 'feeamount'
    ) THEN

        ALTER TABLE dbo.clearingrecords
            ADD COLUMN feeamount decimal(18,4)
            NOT NULL
            DEFAULT 0;

    END IF;
END $$;

/*DO $$
BEGIN
    IF not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='clearingbatch' and column_name='institutioncode') THEN

            alter table dbo.clearingbatch add institutioncode varchar(32) not null constraint df_clearingbatch_institutioncode default '';

    END IF;
END $$;*/

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'dbo'
          AND table_name = 'clearingbatch'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'dbo'
          AND table_name = 'clearingbatch'
          AND column_name = 'institutioncode'
    ) THEN

        ALTER TABLE dbo.clearingbatch
            ADD COLUMN institutioncode varchar(32)
            NOT NULL
            DEFAULT '';

    END IF;
END $$;

insert into dbo.interchangefeerule (id, rulecode, network, productcode, channelcode, merchantcategorycode, countrycode, currencycode, transactiontypecode, flatfee, percentfee, minimumfee, maximumfee, direction, effectivefrom, effectiveto, isactive, priority)
values (gen_random_uuid(), 'VISA-DEBIT-DOM-ATM', 'Visa', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4500, 0.0000, 25.0000, 'AcquirerReceives', ('2020-01-01')::date, null, true, 90),

    (gen_random_uuid(), 'VISA-DEBIT-POS', 'Visa', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.3500, 0.0000, 20.0000, 'IssuerReceives', ('2020-01-01')::date, null, true, 80),

    (gen_random_uuid(), 'MC-DEBIT-DOM-ATM', 'Mastercard', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4500, 0.0000, 25.0000, 'AcquirerReceives', ('2020-01-01')::date, null, true, 90),

    (gen_random_uuid(), 'MC-DEBIT-POS', 'Mastercard', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.3500, 0.0000, 20.0000, 'IssuerReceives', ('2020-01-01')::date, null, true, 80),

    (gen_random_uuid(), 'RUPAY-POS', 'Rupay', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.2500, 0.0000, 15.0000, 'IssuerReceives', ('2020-01-01')::date, null, true, 80),

    (gen_random_uuid(), 'NPCI-NFS-ATM', 'NpciNfs', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4000, 0.0000, 20.0000, 'AcquirerReceives', ('2020-01-01')::date, null, true, 80)
on conflict (rulecode) do nothing;

-- ===== 022_advanced_reconciliation_odr_udir.sql =====

-- v29 Advanced Reconciliation, ATM Evidence, C3R and ODR/UDIR

-- SQL Server oriented schema. Provides persistence for network reconciliation files,

-- ATM EJ/CCTV/pinhole evidence, C3R cash reconciliation and RBI ODR / NPCI UDIR cases.

DO $$
BEGIN
    IF to_regclass('dbo.networkreconciliationfile') is null THEN

            create table if not exists dbo.networkreconciliationfile (

                id uuid not null constraint pk_networkreconciliationfile primary key,

                format varchar(64) not null,

                network varchar(32) not null,

                businessdate date not null,

                filename varchar(260) not null,

                sourcechannel varchar(64) not null,

                filehashsha256 char(64) not null,

                recordcount int not null,

                totaldebitamount decimal(18,2) not null,

                totalcreditamount decimal(18,2) not null,

                importedat timestamptz not null,

                importedby varchar(128) not null,

                validationsummary varchar(1000) not null

            );

            create index if not exists ix_networkreconciliationfile_datenetwork on dbo.networkreconciliationfile(businessdate, network, format);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.networkreconciliationrecord') is null THEN

            create table if not exists dbo.networkreconciliationrecord (

                id uuid not null constraint pk_networkreconciliationrecord primary key,

                fileid uuid not null,

                network varchar(32) not null,

                format varchar(64) not null,

                businessdate date not null,

                recordtype varchar(32) not null,

                rrn varchar(32) null,

                stan varchar(16) null,

                arn varchar(64) null,

                networkreference varchar(64) null,

                maskedpan varchar(32) null,

                acquirerid varchar(32) null,

                issuerid varchar(32) null,

                terminalid varchar(32) null,

                merchantid varchar(64) null,

                merchantcategorycode varchar(8) null,

                transactioncode varchar(16) null,

                transactionamount decimal(18,2) not null,

                settlementamount decimal(18,2) not null,

                interchangefee decimal(18,2) not null,

                currencycode varchar(3) not null,

                responsecode varchar(8) null,

                status varchar(32) not null,

                rawline text not null,

                constraint fk_networkreconrecord_file foreign key (fileid) references dbo.networkreconciliationfile(id)

            );

            create index if not exists ix_networkreconrecord_match on dbo.networkreconciliationrecord(businessdate, network, rrn, stan, arn, networkreference);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atmevidenceitem') is null THEN

            create table if not exists dbo.atmevidenceitem (

                id uuid not null constraint pk_atmevidenceitem primary key,

                rrn varchar(32) null,

                stan varchar(16) null,

                terminalid varchar(32) not null,

                businessdate date not null,

                evidencetype varchar(16) not null,

                filename varchar(260) not null,

                storageuri varchar(1000) not null,

                hashsha256 char(64) not null,

                extractedtext text null,

                capturedat timestamptz not null,

                capturedby varchar(128) not null

            );

            create index if not exists ix_atmevidenceitem_lookup on dbo.atmevidenceitem(businessdate, terminalid, rrn, stan, evidencetype);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.c3ratmreconciliationrun') is null THEN

            create table if not exists dbo.c3ratmreconciliationrun (

                id uuid not null constraint pk_c3ratmreconciliationrun primary key,

                runreference varchar(80) not null,

                terminalid varchar(32) not null,

                businessdate date not null,

                openingbalance decimal(18,2) not null,

                loadamount decimal(18,2) not null,

                dispensedamount decimal(18,2) not null,

                depositedamount decimal(18,2) not null,

                cashbroughtbackamount decimal(18,2) not null,

                switchexpectedclosingbalance decimal(18,2) not null,

                physicalclosingbalance decimal(18,2) not null,

                shortageamount decimal(18,2) not null,

                excessamount decimal(18,2) not null,

                status varchar(32) not null,

                evidenceidsjson text null,

                createdat timestamptz not null,

                createdby varchar(128) not null,

                approvalnotes varchar(1000) null

            );

            create unique index if not exists ux_c3ratmreconciliationrun_ref on dbo.c3ratmreconciliationrun(runreference);

            create index if not exists ix_c3ratmreconciliationrun_dateterminal on dbo.c3ratmreconciliationrun(businessdate, terminalid, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.odrudircase') is null THEN

            create table if not exists dbo.odrudircase (

                id uuid not null constraint pk_odrudircase primary key,

                network varchar(32) not null,

                localdisputeid uuid null,

                chargebackcaseid uuid null,

                casereference varchar(80) not null,

                externalcasereference varchar(120) null,

                udirtransactionid varchar(120) null,

                rrn varchar(32) null,

                maskedpan varchar(32) null,

                amount decimal(18,2) not null,

                currencycode varchar(3) not null,

                complaintcategory varchar(80) not null,

                status varchar(32) not null,

                lastnetworkresponsecode varchar(16) null,

                lastnetworkresponsemessage varchar(1000) null,

                createdat timestamptz not null,

                submittedat timestamptz null,

                updatedat timestamptz null,

                createdby varchar(128) not null

            );

            create unique index if not exists ux_odrudircase_casereference on dbo.odrudircase(casereference);

            create index if not exists ix_odrudircase_status on dbo.odrudircase(network, status, rrn);

    END IF;
END $$;

-- ===== 023_network_dispute_exchange_odr_udir.sql =====

-- V30 Network Dispute Exchange and external ODR/UDIR integration

-- Bank-grade audit tables for Visa VROL, Mastercard MCOM/File Express,

-- NPCI RuPay/NFS UDIR, RBI ODR and NPCI UDIR exchange files/API payloads.

DO $$
BEGIN
    IF to_regclass('dbo.network_dispute_exchange_files') is null THEN

        create table if not exists dbo.network_dispute_exchange_files (

            id uuid primary key,

            network varchar(32) not null,

            direction varchar(16) not null,

            file_type varchar(40) not null,

            status varchar(24) not null,

            business_date date not null,

            file_name varchar(255) not null,

            content text not null,

            content_sha256 char(64) not null,

            record_count int not null default 0,

            external_batch_reference varchar(128) null,

            network_ack_code varchar(16) null,

            network_ack_message varchar(1024) null,

            transport_reference varchar(256) null,

            created_at timestamptz(7) not null,

            transmitted_at timestamptz(7) null,

            acknowledged_at timestamptz(7) null,

            created_by varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_dispute_exchange_files' and indexname='ix_network_dispute_exchange_files_date_network') THEN
            create index if not exists ix_network_dispute_exchange_files_date_network
        
            on dbo.network_dispute_exchange_files (business_date, network, status);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_dispute_exchange_records') is null THEN

        create table if not exists dbo.network_dispute_exchange_records (

            id uuid primary key,

            file_id uuid not null references dbo.network_dispute_exchange_files(id),

            network varchar(32) not null,

            file_type varchar(40) not null,

            local_chargeback_case_id uuid null,

            local_dispute_id uuid null,

            local_case_reference varchar(128) null,

            network_case_id varchar(128) null,

            udir_transaction_id varchar(128) null,

            rrn varchar(32) null,

            stan varchar(16) null,

            masked_pan varchar(32) null,

            reason_code varchar(32) null,

            amount decimal(18,2) not null default 0,

            currency_code varchar(3) null,

            action_code varchar(40) null,

            raw_record text not null,

            validation_status varchar(16) not null,

            validation_error varchar(1024) null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_dispute_exchange_records' and indexname='ix_network_dispute_exchange_records_file') THEN
            create index if not exists ix_network_dispute_exchange_records_file
        
            on dbo.network_dispute_exchange_records (file_id);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_dispute_exchange_records' and indexname='ix_network_dispute_exchange_records_lookup') THEN
            create index if not exists ix_network_dispute_exchange_records_lookup
        
            on dbo.network_dispute_exchange_records (rrn, stan, network_case_id, udir_transaction_id);
    END IF;
END $$;

-- In production, replace the simulated gateways with certified adapters:

-- Visa VROL / Visa Resolve Online, Mastercard MCOM/File Express, NPCI UDIR SFTP/API, RBI ODR API.

-- ===== 024_atm_driving_protocols.sql =====

-- V31: ATM Driving Protocols, Screen Distribution, LOD, Admin Card Cash Workflow,

-- C3R, EJ/CCTV/Pinhole Evidence, Voice Guidance and Multilingual Runtime

create table if not exists dbo.atm_terminal_profile (

    terminal_id           varchar(32) primary key,

    vendor                varchar(40) not null,

    protocol              varchar(40) not null,

    ip_address            varchar(64) not null,

    location_code         varchar(64) not null,

    branch_code           varchar(64) not null,

    region_code           varchar(64) not null,

    country_code          varchar(3) not null,

    currency_code         varchar(3) not null,

    voice_guidance_enabled boolean not null default false,

    default_language      varchar(16) not null,

    capabilities_json     text null,

    created_at_utc        timestamptz not null,

    updated_at_utc        timestamptz not null

);

create table if not exists dbo.atm_vendor_certification_artifact (

    id                    uuid primary key,

    vendor                varchar(40) not null,

    protocol              varchar(40) not null,

    certification_name    varchar(128) not null,

    version               varchar(32) not null,

    test_pack_reference   varchar(256) not null,

    evidence_hash         varchar(64) not null,

    valid_from_utc        timestamptz not null,

    valid_to_utc          timestamptz null,

    status                varchar(40) not null,

    remarks               varchar(1000) null

);

create table if not exists dbo.atm_screen_definition (

    id                    uuid primary key,

    name                  varchar(128) not null,

    version               varchar(32) not null,

    language_code         varchar(16) not null,

    screen_flow_json      text not null,

    receipt_template      text not null,

    voice_prompt_pack_id  varchar(128) null,

    status                varchar(40) not null,

    created_by            varchar(128) not null,

    created_at_utc        timestamptz not null,

    updated_at_utc        timestamptz not null

);

create table if not exists dbo.atm_lod_file_artifact (

    id                    uuid primary key,

    screen_definition_id  uuid not null,

    vendor                varchar(40) not null,

    protocol              varchar(40) not null,

    file_name             varchar(256) not null,

    content_type          varchar(128) not null,

    payload_base64        text not null,

    sha256_hash           varchar(64) not null,

    generated_at_utc      timestamptz not null,

    generated_by          varchar(128) not null

);

create table if not exists dbo.atm_screen_distribution_job (

    id                    uuid primary key,

    screen_definition_id  uuid not null,

    terminal_ids_json     text not null,

    status                varchar(40) not null,

    scheduled_by          varchar(128) not null,

    scheduled_at_utc      timestamptz not null,

    completed_at_utc      timestamptz null,

    terminal_statuses_json text not null,

    correlation_id        varchar(64) not null

);

create table if not exists dbo.atm_admin_cash_operation (

    id                    uuid primary key,

    terminal_id           varchar(32) not null,

    admin_card_masked_pan varchar(32) not null,

    operation_type        varchar(40) not null,

    currency_code         varchar(3) not null,

    cassettes_json        text not null,

    total_amount          decimal(19,4) not null,

    performed_by          varchar(128) not null,

    performed_at_utc      timestamptz not null,

    approval_status       varchar(40) not null,

    correlation_id        varchar(64) not null

);

create table if not exists dbo.atm_c3r_reconciliation_run (

    id                    uuid primary key,

    terminal_id           varchar(32) not null,

    business_date         date not null,

    opening_balance       decimal(19,4) not null,

    load_amount           decimal(19,4) not null,

    dispensed_amount      decimal(19,4) not null,

    deposited_amount      decimal(19,4) not null,

    cash_brought_back_amount decimal(19,4) not null,

    shortage_amount       decimal(19,4) not null,

    excess_amount         decimal(19,4) not null,

    closing_balance       decimal(19,4) not null,

    status                varchar(40) not null,

    created_at_utc        timestamptz not null,

    correlation_id        varchar(64) not null

);

create table if not exists dbo.atm_evidence_artifact (

    id                    uuid primary key,

    terminal_id           varchar(32) not null,

    evidence_type         varchar(40) not null,

    from_utc              timestamptz not null,

    to_utc                timestamptz not null,

    file_name             varchar(256) not null,

    storage_uri           varchar(1024) not null,

    sha256_hash           varchar(64) not null,

    captured_by           varchar(128) not null,

    captured_at_utc       timestamptz not null,

    correlation_id        varchar(64) not null

);

create table if not exists dbo.atm_voice_prompt_pack (

    id                    varchar(128) primary key,

    language_code         varchar(16) not null,

    description           varchar(512) not null,

    prompt_file_uris_json text not null,

    sha256_manifest       varchar(64) not null,

    updated_at_utc        timestamptz not null

);

create index if not exists ix_atm_terminal_profile_vendor_protocol on dbo.atm_terminal_profile(vendor, protocol);

create index if not exists ix_atm_c3r_terminal_date on dbo.atm_c3r_reconciliation_run(terminal_id, business_date);

create index if not exists ix_atm_evidence_terminal_type on dbo.atm_evidence_artifact(terminal_id, evidence_type);

create index if not exists ix_atm_screen_distribution_status on dbo.atm_screen_distribution_job(status, scheduled_at_utc);

-- ===== 025_pos_mpos_ecommerce_terminal_driving.sql =====

-- V32 POS / mPOS / e-Commerce Terminal Driving

-- v44.6 canonicalized: creates the same production SQL Server objects consumed by SqlPosTerminalDrivingRepository.

-- Complements v33 PosAcquiringProduction persistence and removes the SQL-provider dependency on in-memory v32 records.

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') is null THEN

            create table if not exists dbo.posterminalprofiles (

                terminalid varchar(64) not null primary key,

                merchantid varchar(64) not null,

                vendor varchar(32) not null,

                protocol varchar(32) not null,

                serialnumber varchar(128) not null,

                devicemodel varchar(128) not null,

                branchcode varchar(64) not null,

                locationcode varchar(64) not null,

                countrycode varchar(8) not null,

                currencycode varchar(8) not null,

                ismpos boolean not null,

                contactlessenabled boolean not null,

                status varchar(32) not null,

                capabilitiesjson text not null,

                createdat timestamptz not null,

                updatedat timestamptz not null

            );

            create index if not exists ix_posterminalprofiles_merchant on dbo.posterminalprofiles(merchantid, status);

            create index if not exists ix_posterminalprofiles_vendorprotocol on dbo.posterminalprofiles(vendor, protocol, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') is null THEN

            create table if not exists dbo.posmposenrollments (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                devicebindingid varchar(256) not null,

                mobilenumbermasked varchar(64) not null,

                appversion varchar(64) not null,

                osname varchar(64) not null,

                osversion varchar(64) not null,

                status varchar(32) not null,

                enrolledat timestamptz not null,

                updatedat timestamptz not null

            );

            create index if not exists ix_posmposenrollments_terminal on dbo.posmposenrollments(terminalid, status);

            create index if not exists ix_posmposenrollments_merchant on dbo.posmposenrollments(merchantid, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadcertifications') is null THEN

            create table if not exists dbo.poskeydownloadcertifications (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                vendor varchar(32) not null,

                protocol varchar(32) not null,

                scheme varchar(32) not null,

                keyscheme varchar(64) not null,

                certificationpackreference varchar(256) not null,

                evidencehash varchar(128) not null,

                status varchar(32) not null,

                certifiedat timestamptz not null,

                remarks varchar(1000) not null

            );

            create index if not exists ix_poskeydownloadcertifications_terminal on dbo.poskeydownloadcertifications(terminalid, scheme, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadsessions') is null THEN

            create table if not exists dbo.poskeydownloadsessions (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                scheme varchar(32) not null,

                tmkkcv varchar(16) not null,

                tpkkcv varchar(16) not null,

                takkcv varchar(16) not null,

                status varchar(32) not null,

                requestedat timestamptz not null,

                completedat timestamptz null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poskeydownloadsessions_terminal on dbo.poskeydownloadsessions(terminalid, scheme, status, requestedat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') is null THEN

            create table if not exists dbo.poscontactlesstransactionflows (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                mode varchar(32) not null,

                panmasked varchar(32) not null,

                amount decimal(18,2) not null,

                currencycode varchar(8) not null,

                emvcryptogram varchar(512) not null,

                offlineapprovedbyterminal boolean not null,

                onlinehostauthorised boolean not null,

                responsecode varchar(8) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poscontactlesstransactionflows_merchant on dbo.poscontactlesstransactionflows(merchantid, currencycode, createdat);

            create index if not exists ix_poscontactlesstransactionflows_terminal on dbo.poscontactlesstransactionflows(terminalid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') is null THEN

            create table if not exists dbo.postipadjustments (

                id uuid not null primary key,

                originaltransactionid varchar(128) not null,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                originalamount decimal(18,2) not null,

                tipamount decimal(18,2) not null,

                finalamount decimal(18,2) not null,

                currencycode varchar(8) not null,

                approvalcode varchar(64) not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

            create unique index if not exists ux_postipadjustments_original on dbo.postipadjustments(originaltransactionid);

            create index if not exists ix_postipadjustments_merchant on dbo.postipadjustments(merchantid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') is null THEN

            create table if not exists dbo.poscashatposacquiring (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                panmasked varchar(32) not null,

                purchaseamount decimal(18,2) not null,

                cashamount decimal(18,2) not null,

                totalamount decimal(18,2) not null,

                currencycode varchar(8) not null,

                approvalcode varchar(64) not null,

                responsecode varchar(8) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poscashatposacquiring_merchant on dbo.poscashatposacquiring(merchantid, currencycode, createdat);

            create index if not exists ix_poscashatposacquiring_terminal on dbo.poscashatposacquiring(terminalid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementbatches') is null THEN

            create table if not exists dbo.posmerchantsettlementbatches (

                id uuid not null primary key,

                merchantid varchar(64) not null,

                settlementdate date not null,

                currencycode varchar(8) not null,

                transactioncount int not null,

                grossamount decimal(18,2) not null,

                interchangefee decimal(18,2) not null,

                mdrfee decimal(18,2) not null,

                gstamount decimal(18,2) not null,

                netpayable decimal(18,2) not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                filehash varchar(128) not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_posmerchantsettlementbatches_merchant on dbo.posmerchantsettlementbatches(merchantid, settlementdate, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') is null THEN

            create table if not exists dbo.posdevicecommands (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                command varchar(128) not null,

                parametersjson text not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                appliedat timestamptz null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_posdevicecommands_terminal on dbo.posdevicecommands(terminalid, status, createdat);

    END IF;
END $$;

-- ===== 026_pos_acquiring_production_core.sql =====

-- V33 POS Acquiring Production Core

-- v44.6 canonicalized: v32 terminal-driving records are owned by migration 025; this migration adds only v33 acquiring-specific persistence.

DO $$
BEGIN
    IF to_regclass('dbo.posmerchants') is null THEN

            create table if not exists dbo.posmerchants (

                merchantid varchar(64) not null primary key,

                legalname varchar(256) not null,

                displayname varchar(256) not null,

                mcc varchar(8) not null,

                panortaxidmasked varchar(64) not null,

                kycstatus varchar(64) not null,

                settlementaccountnumbermasked varchar(64) not null,

                settlementifsc varchar(32) not null,

                settlementcurrencycode varchar(8) not null,

                settlementcycle varchar(32) not null,

                status varchar(32) not null,

                defaultmdrpercent decimal(9,4) not null,

                defaultmdrflatfee decimal(18,2) not null,

                allowcashatpos boolean not null,

                allowofflinecontactless boolean not null,

                createdat timestamptz not null,

                updatedat timestamptz not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_posmerchants_mcc_status on dbo.posmerchants(mcc, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmdrrules') is null THEN

            create table if not exists dbo.posmdrrules (

                id uuid not null primary key,

                merchantid varchar(64) not null,

                mcc varchar(8) not null,

                scheme varchar(32) not null,

                network varchar(32) not null,

                productcode varchar(64) not null,

                currencycode varchar(8) not null,

                flowtype varchar(64) not null,

                percentfee decimal(9,4) not null,

                flatfee decimal(18,2) not null,

                minimumfee decimal(18,2) not null,

                maximumfee decimal(18,2) not null,

                gstpercent decimal(9,4) not null,

                effectivefrom date not null,

                effectiveto date null,

                isactive boolean not null,

                priority int not null,

                createdat timestamptz not null

            );

            create index if not exists ix_posmdrrules_lookup on dbo.posmdrrules(network, merchantid, mcc, scheme, productcode, currencycode, flowtype, isactive, effectivefrom, effectiveto);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posterminallifecycle') is null THEN

            create table if not exists dbo.posterminallifecycle (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                status varchar(32) not null,

                previousstatus varchar(64) not null,

                reasoncode varchar(64) not null,

                remarks varchar(1000) not null,

                actor varchar(128) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') is null THEN

            create table if not exists dbo.poscommandqueue (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                command varchar(128) not null,

                parametersjson text not null,

                status varchar(32) not null,

                attemptcount int not null,

                maxattempts int not null,

                notbefore timestamptz not null,

                expiresat timestamptz not null,

                createdat timestamptz not null,

                dispatchedat timestamptz null,

                acknowledgedat timestamptz null,

                lasterror varchar(1000) not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poscommandqueue_pending on dbo.poscommandqueue(status, terminalid, notbefore, expiresat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlesstxns') is null THEN

            create table if not exists dbo.posofflinecontactlesstxns (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                transactionid varchar(128) not null,

                panmasked varchar(32) not null,

                amount decimal(18,2) not null,

                currencycode varchar(8) not null,

                emvcryptogram varchar(256) not null,

                terminalapprovedat timestamptz not null,

                capturedeadline timestamptz not null,

                status varchar(32) not null,

                riskdecision varchar(128) not null,

                clearingreference varchar(128) not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_posofflinecontactlesstxns_clearing on dbo.posofflinecontactlesstxns(merchantid, currencycode, terminalapprovedat, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlessbatches') is null THEN

            create table if not exists dbo.posofflinecontactlessbatches (

                id uuid not null primary key,

                merchantid varchar(64) not null,

                businessdate date not null,

                currencycode varchar(8) not null,

                transactioncount int not null,

                grossamount decimal(18,2) not null,

                status varchar(32) not null,

                filehash varchar(128) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeyceremonies') is null THEN

            create table if not exists dbo.poskeyceremonies (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                ceremonytype varchar(64) not null,

                status varchar(32) not null,

                scheme varchar(32) not null,

                keyscheme varchar(64) not null,

                zmkkcv varchar(16) not null,

                tmkkcv varchar(16) not null,

                tpkkcv varchar(16) not null,

                takkcv varchar(16) not null,

                makeruser varchar(128) not null,

                checkeruser varchar(128) not null,

                evidencehash varchar(128) not null,

                createdat timestamptz not null,

                approvedat timestamptz null,

                completedat timestamptz null,

                correlationid varchar(128) not null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posemvcertificationevidence') is null THEN

            create table if not exists dbo.posemvcertificationevidence (

                id uuid not null primary key,

                terminalmodel varchar(128) not null,

                vendor varchar(32) not null,

                protocol varchar(32) not null,

                level varchar(64) not null,

                scheme varchar(32) not null,

                testpackreference varchar(256) not null,

                evidencehash varchar(128) not null,

                status varchar(32) not null,

                certifiedfrom date not null,

                certifiedto date null,

                remarks varchar(1000) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementpostings') is null THEN

            create table if not exists dbo.posmerchantsettlementpostings (

                id uuid not null primary key,

                merchantid varchar(64) not null,

                settlementdate date not null,

                currencycode varchar(8) not null,

                transactioncount int not null,

                grossamount decimal(18,2) not null,

                interchangefee decimal(18,2) not null,

                mdrfee decimal(18,2) not null,

                gstamount decimal(18,2) not null,

                netpayable decimal(18,2) not null,

                status varchar(32) not null,

                gljournalreference varchar(128) not null,

                corebankingexportreference varchar(128) not null,

                filehash varchar(128) not null,

                createdat timestamptz not null,

                postedat timestamptz null,

                correlationid varchar(128) not null

            );

    END IF;
END $$;

-- ===== 027_acquiring_certification_simulator.sql =====

-- V34 Card Network Acquiring Certification Simulator

-- Certification lab tables for Visa/Mastercard/RuPay/NPCI acquiring simulators, test packs,

-- validation results, EMV/contactless checklist and evidence reports.

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_cases') is null THEN

        create table if not exists dbo.acquiring_cert_test_cases (

            id uuid primary key,

            test_case_code varchar(80) not null unique,

            scheme varchar(32) not null,

            category varchar(64) not null,

            flow_kind varchar(64) not null,

            title varchar(250) not null,

            description text not null,

            input_fields_json text not null,

            expected_fields_json text not null,

            expected_response_code varchar(8) not null,

            severity varchar(16) not null,

            is_mandatory boolean not null default true,

            is_active boolean not null default true,

            created_at timestamp(7) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_packs') is null THEN

        create table if not exists dbo.acquiring_cert_packs (

            id uuid primary key,

            pack_code varchar(80) not null unique,

            scheme varchar(32) not null,

            terminal_model varchar(120) not null,

            pos_protocol varchar(80) not null,

            version varchar(40) not null,

            test_case_ids_json text not null,

            status varchar(32) not null,

            created_at timestamp(7) not null,

            updated_at timestamp(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_runs') is null THEN

        create table if not exists dbo.acquiring_cert_runs (

            id uuid primary key,

            pack_id uuid not null,

            pack_code varchar(80) not null,

            scheme varchar(32) not null,

            status varchar(32) not null,

            total_tests int not null,

            passed_tests int not null,

            failed_tests int not null,

            blocked_tests int not null,

            evidence_hash varchar(128) not null,

            started_at timestamp(7) not null,

            completed_at timestamp(7) null,

            actor varchar(120) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') is null THEN

        create table if not exists dbo.acquiring_cert_test_results (

            id uuid primary key,

            run_id uuid not null,

            test_case_id uuid not null,

            test_case_code varchar(80) not null,

            status varchar(32) not null,

            response_code varchar(8) not null,

            findings_json text not null,

            request_hash varchar(128) not null,

            response_hash varchar(128) not null,

            trace text not null,

            executed_at timestamp(7) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_message_validation_results') is null THEN

        create table if not exists dbo.acquiring_message_validation_results (

            id uuid primary key,

            scheme varchar(32) not null,

            flow_kind varchar(64) not null,

            mti varchar(4) not null,

            is_valid boolean not null,

            findings_json text not null,

            message_hash varchar(128) not null,

            validated_at timestamp(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_host_response_validation_results') is null THEN

        create table if not exists dbo.acquiring_host_response_validation_results (

            id uuid primary key,

            scheme varchar(32) not null,

            flow_kind varchar(64) not null,

            is_valid boolean not null,

            findings_json text not null,

            validated_at timestamp(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.emv_contactless_cert_checklist') is null THEN

        create table if not exists dbo.emv_contactless_cert_checklist (

            id uuid primary key,

            scheme varchar(32) not null,

            terminal_model varchar(120) not null,

            kernel_type varchar(80) not null,

            requirement_code varchar(80) not null,

            requirement_text text not null,

            status varchar(32) not null,

            evidence_reference varchar(250) not null,

            remarks text not null,

            updated_at timestamp(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_flow_results') is null THEN

        create table if not exists dbo.acquiring_cert_flow_results (

            id uuid primary key,

            scheme varchar(32) not null,

            flow_kind varchar(64) not null,

            stan varchar(20) not null,

            rrn varchar(40) not null,

            response_code varchar(8) not null,

            network_reference varchar(80) not null,

            settlement_reference varchar(80) not null,

            chargeback_reference varchar(80) not null,

            status varchar(32) not null,

            findings_json text not null,

            executed_at timestamp(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_evidence_reports') is null THEN

        create table if not exists dbo.acquiring_cert_evidence_reports (

            id uuid primary key,

            run_id uuid not null,

            report_format varchar(20) not null,

            report_body text not null,

            report_hash varchar(128) not null,

            generated_at timestamp(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='acquiring_cert_test_cases' and indexname='ix_acquiring_cert_test_cases_scheme') THEN
            create index if not exists ix_acquiring_cert_test_cases_scheme on dbo.acquiring_cert_test_cases (scheme, category, flow_kind);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='acquiring_cert_runs' and indexname='ix_acquiring_cert_runs_pack') THEN
            create index if not exists ix_acquiring_cert_runs_pack on dbo.acquiring_cert_runs (pack_id, status);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='acquiring_cert_test_results' and indexname='ix_acquiring_cert_results_run') THEN
            create index if not exists ix_acquiring_cert_results_run on dbo.acquiring_cert_test_results (run_id, status);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='emv_contactless_cert_checklist' and indexname='ix_emv_contactless_checklist_terminal') THEN
            create index if not exists ix_emv_contactless_checklist_terminal on dbo.emv_contactless_cert_checklist (scheme, terminal_model, requirement_code);
    END IF;
END $$;

-- ===== 028_acquiring_certification_lab_extensions.sql =====

-- V35: Acquiring certification lab extensions

-- Adds schema for scenario designer, masked production replay, fuzz testing,

-- regression suites, endurance profiles, fault injection and plugin registry.

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_scenarios') is null THEN

        create table if not exists dbo.acquiring_cert_scenarios (

            id uuid primary key,

            scenario_code varchar(80) not null unique,

            name varchar(200) not null,

            scheme varchar(30) not null,

            flow_kind varchar(40) not null,

            description text not null default '',

            steps_json text not null,

            is_active boolean not null default true,

            version varchar(40) not null default '1.0',

            created_at timestamptz(7) not null,

            updated_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_replay_runs') is null THEN

        create table if not exists dbo.acquiring_cert_replay_runs (

            id uuid primary key,

            source varchar(40) not null,

            scheme varchar(30) not null,

            source_name varchar(200) not null,

            total_messages integer not null,

            replayed_messages integer not null,

            passed_messages integer not null,

            failed_messages integer not null,

            masked_samples_json text not null,

            evidence_hash char(64) not null,

            executed_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_fuzz_runs') is null THEN

        create table if not exists dbo.acquiring_cert_fuzz_runs (

            id uuid primary key,

            scheme varchar(30) not null,

            flow_kind varchar(40) not null,

            case_count integer not null,

            passed_cases integer not null,

            failed_cases integer not null,

            critical_findings integer not null,

            findings_json text not null,

            evidence_hash char(64) not null,

            executed_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_regression_runs') is null THEN

        create table if not exists dbo.acquiring_cert_regression_runs (

            id uuid primary key,

            suite_code varchar(80) not null,

            baseline_version varchar(80) not null,

            candidate_version varchar(80) not null,

            total_packs integer not null,

            passed_packs integer not null,

            failed_packs integer not null,

            regressions_json text not null,

            evidence_hash char(64) not null,

            executed_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_endurance_runs') is null THEN

        create table if not exists dbo.acquiring_cert_endurance_runs (

            id uuid primary key,

            scheme varchar(30) not null,

            profile_code varchar(80) not null,

            target_tps integer not null,

            duration_seconds integer not null,

            concurrent_terminals integer not null,

            total_transactions bigint not null,

            average_latency_ms numeric(12,2) not null,

            p95_latency_ms numeric(12,2) not null,

            p99_latency_ms numeric(12,2) not null,

            success_rate numeric(6,2) not null,

            evidence_hash char(64) not null,

            executed_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_fault_injection_runs') is null THEN

        create table if not exists dbo.acquiring_cert_fault_injection_runs (

            id uuid primary key,

            scheme varchar(30) not null,

            fault_kind varchar(40) not null,

            duration_seconds integer not null,

            failure_percentage numeric(6,2) not null,

            impacted_messages integer not null,

            recovered_messages integer not null,

            recovery_validated boolean not null,

            evidence_hash char(64) not null,

            executed_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_plugins') is null THEN

        create table if not exists dbo.acquiring_cert_plugins (

            id uuid primary key,

            plugin_code varchar(80) not null unique,

            name varchar(200) not null,

            plugin_kind varchar(50) not null,

            scheme varchar(30) null,

            version varchar(40) not null,

            entry_point varchar(500) not null,

            capabilities_json text not null,

            is_enabled boolean not null default true,

            registered_at timestamptz(7) not null,

            correlation_id varchar(80) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='acquiring_cert_scenarios' and indexname='ix_acquiring_cert_scenarios_scheme') THEN
            create index if not exists ix_acquiring_cert_scenarios_scheme on dbo.acquiring_cert_scenarios (scheme, flow_kind);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='acquiring_cert_replay_runs' and indexname='ix_acquiring_cert_replay_scheme') THEN
            create index if not exists ix_acquiring_cert_replay_scheme on dbo.acquiring_cert_replay_runs (scheme, executed_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='acquiring_cert_plugins' and indexname='ix_acquiring_cert_plugins_kind') THEN
            create index if not exists ix_acquiring_cert_plugins_kind on dbo.acquiring_cert_plugins (plugin_kind, is_enabled);
    END IF;
END $$;

-- ===== 029_issuer_certification_full_lab.sql =====

-- v36: Issuer Certification Simulator & Host Validation Lab

-- Canonical SQL Server DDL for issuer-side certification test packs, runs, evidence and validation.

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_test_cases') is null THEN

            create table if not exists dbo.issuer_cert_test_cases (

                id uuid not null constraint pk_issuer_cert_test_cases primary key,

                test_case_code varchar(80) not null,

                scheme varchar(32) not null,

                category varchar(64) not null,

                flow_kind varchar(64) not null,

                title varchar(250) not null,

                description text not null,

                request_fields_json text not null,

                expected_host_fields_json text not null,

                expected_response_code varchar(8) not null,

                requires_hsm boolean not null constraint df_issuer_cert_test_cases_hsm default (false),

                requires_cbs boolean not null constraint df_issuer_cert_test_cases_cbs default (false),

                is_mandatory boolean not null constraint df_issuer_cert_test_cases_mandatory default (true),

                created_at timestamptz(7) not null,

                correlation_id varchar(128) not null,

                constraint ck_issuer_cert_test_cases_request_json check ((request_fields_json::jsonb is not null)),

                constraint ck_issuer_cert_test_cases_expected_json check ((expected_host_fields_json::jsonb is not null))

            );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_test_cases' and indexname='ux_issuer_cert_test_cases_code_scheme') THEN
            create unique index if not exists ux_issuer_cert_test_cases_code_scheme on dbo.issuer_cert_test_cases(test_case_code, scheme);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_test_cases' and indexname='ix_issuer_cert_test_cases_scheme_category') THEN
            create index if not exists ix_issuer_cert_test_cases_scheme_category on dbo.issuer_cert_test_cases(scheme, category, flow_kind);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_packs') is null THEN

            create table if not exists dbo.issuer_cert_packs (

                id uuid not null constraint pk_issuer_cert_packs primary key,

                pack_code varchar(80) not null,

                scheme varchar(32) not null,

                host_profile varchar(128) not null,

                card_product varchar(128) not null,

                version varchar(40) not null,

                test_case_ids_json text not null,

                status varchar(32) not null,

                created_at timestamptz(7) not null,

                updated_at timestamptz(7) not null,

                correlation_id varchar(128) not null,

                constraint ck_issuer_cert_packs_test_case_ids_json check ((test_case_ids_json::jsonb is not null))

            );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_packs' and indexname='ux_issuer_cert_packs_code_scheme') THEN
            create unique index if not exists ux_issuer_cert_packs_code_scheme on dbo.issuer_cert_packs(pack_code, scheme);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_runs') is null THEN

            create table if not exists dbo.issuer_cert_runs (

                id uuid not null constraint pk_issuer_cert_runs primary key,

                pack_id uuid not null,

                pack_code varchar(80) not null,

                scheme varchar(32) not null,

                status varchar(32) not null,

                total_tests int not null,

                passed_tests int not null,

                failed_tests int not null,

                blocked_tests int not null,

                evidence_hash char(64) not null,

                started_at timestamptz(7) not null,

                completed_at timestamptz(7) null,

                actor varchar(128) not null,

                correlation_id varchar(128) not null,

                constraint ck_issuer_cert_runs_counts check (total_tests >= 0 and passed_tests >= 0 and failed_tests >= 0 and blocked_tests >= 0)

            );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_runs' and indexname='ix_issuer_cert_runs_pack_started') THEN
            create index if not exists ix_issuer_cert_runs_pack_started on dbo.issuer_cert_runs(pack_id, started_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_run_results') is null THEN

            create table if not exists dbo.issuer_cert_run_results (

                id uuid not null constraint pk_issuer_cert_run_results primary key,

                run_id uuid not null,

                test_case_id uuid not null,

                test_case_code varchar(80) not null,

                status varchar(32) not null,

                response_code varchar(8) not null,

                findings_json text not null,

                request_hash char(64) not null,

                response_hash char(64) not null,

                trace text not null,

                executed_at timestamptz(7) not null,

                constraint ck_issuer_cert_run_results_findings_json check ((findings_json::jsonb is not null))

            );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_run_results' and indexname='ix_issuer_cert_run_results_run') THEN
            create index if not exists ix_issuer_cert_run_results_run on dbo.issuer_cert_run_results(run_id, executed_at);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_evidence_reports') is null THEN

            create table if not exists dbo.issuer_cert_evidence_reports (

                id uuid not null constraint pk_issuer_cert_evidence_reports primary key,

                run_id uuid not null,

                report_format varchar(20) not null,

                report_body text not null,

                report_hash char(64) not null,

                generated_at timestamptz(7) not null,

                correlation_id varchar(128) not null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_evidence_reports' and indexname='ix_issuer_cert_evidence_reports_run') THEN
            create index if not exists ix_issuer_cert_evidence_reports_run on dbo.issuer_cert_evidence_reports(run_id, generated_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_validation_evidence') is null THEN

            create table if not exists dbo.issuer_cert_validation_evidence (

                id uuid not null constraint pk_issuer_cert_validation_evidence primary key,

                scheme varchar(32) not null,

                flow_kind varchar(64) not null,

                validation_type varchar(64) not null,

                is_valid boolean not null,

                findings_json text not null,

                evidence_hash char(64) not null,

                validated_at timestamptz(7) not null,

                correlation_id varchar(128) not null,

                constraint ck_issuer_cert_validation_findings_json check ((findings_json::jsonb is not null))

            );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='issuer_cert_validation_evidence' and indexname='ix_issuer_cert_validation_evidence_scheme_type') THEN
            create index if not exists ix_issuer_cert_validation_evidence_scheme_type on dbo.issuer_cert_validation_evidence(scheme, validation_type, validated_at desc);
    END IF;
END $$;

-- ===== 030_real_hsm_key_management_production_core.sql =====

-- V37 Real HSM & Key Management Production Core

-- Adds production-grade HSM inventory, key lifecycle, key ceremony, TR-31/TR-34, DUKPT/UKPT, and audit evidence tables.

DO $$
BEGIN
    IF to_regclass('dbo.hsm_connector_profiles') is null THEN

            create table if not exists dbo.hsm_connector_profiles (

                connector_id uuid not null primary key,

                name varchar(100) not null,

                vendor varchar(40) not null,

                endpoint varchar(512) not null,

                is_production boolean not null default false,

                is_active boolean not null default true,

                created_at timestamptz not null,

                last_health_check_at timestamptz null,

                last_health_status varchar(40) not null default 'UNKNOWN',

                settings_json text null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hsm_key_inventory') is null THEN

            create table if not exists dbo.hsm_key_inventory (

                key_id uuid not null primary key,

                key_alias varchar(120) not null unique,

                key_type varchar(40) not null,

                key_usage varchar(60) not null,

                status varchar(40) not null,

                key_block_format varchar(40) not null,

                key_check_value varchar(16) not null,

                parent_key_alias varchar(120) null,

                version varchar(20) not null,

                network varchar(40) null,

                institution_id varchar(40) null,

                created_by varchar(120) not null,

                created_at timestamptz not null,

                activated_at timestamptz null,

                retired_at timestamptz null,

                expires_at timestamptz null,

                custodian_a varchar(120) null,

                custodian_b varchar(120) null,

                audit_hash char(64) not null

            );

            create index if not exists ix_hsm_key_inventory_type_status on dbo.hsm_key_inventory(key_type, status);

            create index if not exists ix_hsm_key_inventory_network on dbo.hsm_key_inventory(network, institution_id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hsm_key_ceremonies') is null THEN

            create table if not exists dbo.hsm_key_ceremonies (

                ceremony_id uuid not null primary key,

                ceremony_type varchar(80) not null,

                status varchar(40) not null,

                target_key_alias varchar(120) not null,

                maker varchar(120) not null,

                checker varchar(120) null,

                executed_by varchar(120) null,

                created_at timestamptz not null,

                approved_at timestamptz null,

                executed_at timestamptz null,

                steps_json text not null,

                evidence_hashes_json text not null,

                notes text null

            );

            create index if not exists ix_hsm_key_ceremonies_status on dbo.hsm_key_ceremonies(status, created_at);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hsm_tr34_remote_key_load_sessions') is null THEN

            create table if not exists dbo.hsm_tr34_remote_key_load_sessions (

                session_id uuid not null primary key,

                terminal_id varchar(40) not null,

                key_alias varchar(120) not null,

                status varchar(50) not null,

                challenge varchar(128) not null,

                envelope_ref varchar(256) not null,

                audit_hash char(64) not null,

                created_at timestamptz not null,

                completed_at timestamptz null

            );

            create index if not exists ix_hsm_tr34_terminal_status on dbo.hsm_tr34_remote_key_load_sessions(terminal_id, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hsm_dukpt_device_state') is null THEN

            create table if not exists dbo.hsm_dukpt_device_state (

                terminal_id varchar(40) not null primary key,

                bdk_alias varchar(120) not null,

                ksn varchar(40) not null,

                counter bigint not null,

                current_kcv varchar(16) not null,

                last_derived_at timestamptz not null,

                status varchar(40) not null

            );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hsm_tamper_proof_audit_log') is null THEN

            create table if not exists dbo.hsm_tamper_proof_audit_log (

                audit_id uuid not null primary key,

                created_at timestamptz not null,

                severity varchar(20) not null,

                actor varchar(120) not null,

                action varchar(120) not null,

                target varchar(160) not null,

                correlation_id varchar(80) not null,

                before_hash char(64) null,

                after_hash char(64) null,

                details text null,

                chain_hash char(64) not null

            );

            create index if not exists ix_hsm_audit_target_time on dbo.hsm_tamper_proof_audit_log(target, created_at desc);

            create index if not exists ix_hsm_audit_action_time on dbo.hsm_tamper_proof_audit_log(action, created_at desc);

    END IF;
END $$;

-- ===== 031_real_card_network_host_integration_core.sql =====

-- V38 Real Card Network Host Integration Core

-- Visa Base I/II, Mastercard MIP/IPM/File Express, RuPay/NPCI host integration boundaries.

DO $$
BEGIN
    IF to_regclass('dbo.network_host_profiles') is null THEN

        create table if not exists dbo.network_host_profiles (

            host_id uuid primary key,

            host_code varchar(64) not null unique,

            name varchar(200) not null,

            scheme varchar(40) not null,

            role varchar(20) not null,

            transport varchar(40) not null,

            endpoint varchar(500) not null,

            institution_id varchar(64) not null,

            bin_range varchar(128) not null,

            currency_code varchar(3) not null,

            time_zone varchar(64) not null,

            is_production boolean not null default false,

            status varchar(40) not null,

            created_at timestamptz(7) not null,

            last_sign_on_at timestamptz(7) null,

            last_echo_at timestamptz(7) null,

            settings_json text not null default '{}'

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') is null THEN

        create table if not exists dbo.iso8583_network_profiles (

            profile_id uuid primary key,

            profile_code varchar(128) not null unique,

            scheme varchar(40) not null,

            flow varchar(40) not null,

            mti varchar(4) not null,

            mandatory_fields_json text not null,

            field_mappings_json text not null,

            response_code_map_json text not null,

            status varchar(30) not null,

            version varchar(40) not null,

            created_at timestamptz(7) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_message_journal') is null THEN

        create table if not exists dbo.network_message_journal (

            message_id uuid primary key,

            host_id uuid not null references dbo.network_host_profiles(host_id),

            scheme varchar(40) not null,

            flow varchar(40) not null,

            mti varchar(4) not null,

            stan varchar(12) not null,

            rrn varchar(24) not null,

            pan_masked varchar(32) not null,

            amount decimal(18,2) not null,

            currency_code varchar(3) not null,

            fields_json text not null,

            raw_message text not null,

            correlation_id varchar(128) not null,

            created_at timestamptz(7) not null,

            direction varchar(8) not null,

            message_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_saf_replay_queue') is null THEN

        create table if not exists dbo.network_saf_replay_queue (

            replay_id uuid primary key,

            host_id uuid not null references dbo.network_host_profiles(host_id),

            scheme varchar(40) not null,

            flow varchar(40) not null,

            original_reference varchar(128) not null,

            status varchar(30) not null,

            attempt_count int not null default 0,

            created_at timestamptz(7) not null,

            last_attempt_at timestamptz(7) null,

            last_response_code varchar(16) null,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_settlement_calendars') is null THEN

        create table if not exists dbo.network_settlement_calendars (

            calendar_id uuid primary key,

            scheme varchar(40) not null,

            institution_id varchar(64) not null,

            currency_code varchar(3) not null,

            business_date varchar(10) not null,

            cutover_time_local varchar(16) not null,

            status varchar(30) not null,

            created_at timestamptz(7) not null,

            cutover_at timestamptz(7) null,

            notes text null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_host_profiles' and indexname='ix_network_host_profiles_scheme_status') THEN
            create index if not exists ix_network_host_profiles_scheme_status on dbo.network_host_profiles (scheme, status);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='iso8583_network_profiles' and indexname='ix_iso8583_network_profiles_scheme_flow') THEN
            create index if not exists ix_iso8583_network_profiles_scheme_flow on dbo.iso8583_network_profiles (scheme, flow, mti);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_message_journal' and indexname='ix_network_message_journal_host_created') THEN
            create index if not exists ix_network_message_journal_host_created on dbo.network_message_journal (host_id, created_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_saf_replay_queue' and indexname='ix_network_saf_replay_queue_host_status') THEN
            create index if not exists ix_network_saf_replay_queue_host_status on dbo.network_saf_replay_queue (host_id, status);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='network_settlement_calendars' and indexname='ix_network_settlement_calendars_scheme_date') THEN
            create index if not exists ix_network_settlement_calendars_scheme_date on dbo.network_settlement_calendars (scheme, business_date);
    END IF;
END $$;

-- ===== 032_core_banking_enterprise_integration_core.sql =====

-- V39 Core Banking & Enterprise Integration Production Core

-- Adds CBS/Finacle, ESB/API Manager, Payment Hub/IPH, ACS/3DS, FRM, DWH/BI and notification integration records.

DO $$
BEGIN
    IF to_regclass('dbo.enterprise_connector_profiles') is null THEN

        create table if not exists dbo.enterprise_connector_profiles (

            connector_id uuid primary key,

            connector_code varchar(64) not null unique,

            system_code varchar(64) not null,

            transport_code varchar(32) not null,

            endpoint varchar(512) not null,

            institution_id varchar(64) not null,

            environment varchar(32) not null,

            is_production boolean not null default false,

            status varchar(32) not null,

            settings_json text null,

            created_at timestamptz(7) not null default current_timestamp

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='enterprise_connector_profiles' and indexname='ix_enterprise_connector_system_status') THEN
            create index if not exists ix_enterprise_connector_system_status on dbo.enterprise_connector_profiles (system_code, status);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.cbs_posting_audit') is null THEN

        create table if not exists dbo.cbs_posting_audit (

            posting_id uuid primary key default gen_random_uuid(),

            transaction_reference varchar(128) not null,

            cbs_reference varchar(128) not null,

            account_number varchar(64) null,

            posting_type varchar(32) null,

            amount decimal(18,2) null,

            currency_code char(3) null,

            response_code varchar(8) not null,

            audit_hash char(64) not null,

            posted_at timestamptz(7) not null default current_timestamp

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.card_account_linkage_sync') is null THEN

        create table if not exists dbo.card_account_linkage_sync (

            linkage_id uuid primary key,

            customer_id varchar(64) not null,

            card_masked varchar(32) not null,

            account_number varchar(64) not null,

            product_code varchar(64) not null,

            is_primary boolean not null,

            status varchar(32) not null,

            audit_hash char(64) not null,

            created_at timestamptz(7) not null default current_timestamp

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.enterprise_external_events') is null THEN

        create table if not exists dbo.enterprise_external_events (

            event_id uuid primary key default gen_random_uuid(),

            event_type varchar(64) not null,

            external_reference varchar(128) null,

            status varchar(32) not null,

            payload_hash char(64) not null,

            created_at timestamptz(7) not null default current_timestamp

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.dwh_bi_feeds') is null THEN

        create table if not exists dbo.dwh_bi_feeds (

            feed_id uuid primary key,

            feed_type varchar(64) not null,

            business_date date not null,

            output_format varchar(16) not null,

            status varchar(32) not null,

            file_name varchar(255) not null,

            sha256_hash char(64) not null,

            created_at timestamptz(7) not null default current_timestamp

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.enterprise_notifications') is null THEN

        create table if not exists dbo.enterprise_notifications (

            notification_id uuid primary key,

            channel varchar(32) not null,

            recipient_masked varchar(128) not null,

            template_code varchar(64) null,

            status varchar(32) not null,

            provider_reference varchar(128) null,

            audit_hash char(64) not null,

            created_at timestamptz(7) not null default current_timestamp

        );

    END IF;
END $$;

-- ===== 033_operations_command_center_sla_automation.sql =====

-- V40 Operations Command Center & SLA Automation Core

-- Provides DB-backed structures for 24x7 operations dashboard, incidents,

-- SLA breach detection, escalation, technical decline analytics, RCA, DR drill evidence,

-- capacity/performance telemetry and regulatory uptime reporting.

DO $$
BEGIN
    IF to_regclass('dbo.ops_health_snapshots') is null THEN

        create table if not exists dbo.ops_health_snapshots (

            snapshot_id uuid primary key,

            component_type varchar(40) not null,

            component_code varchar(80) not null,

            status varchar(30) not null,

            status_message varchar(500),

            availability_percent numeric(7,4) not null,

            current_tps integer not null default 0,

            technical_declines integer not null default 0,

            captured_at timestamptz(7) not null,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='ops_health_snapshots' and indexname='ix_ops_health_component') THEN
            create index if not exists ix_ops_health_component on dbo.ops_health_snapshots (component_type, component_code, captured_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_incidents') is null THEN

        create table if not exists dbo.ops_incidents (

            incident_id uuid primary key,

            incident_number varchar(60) not null unique,

            severity varchar(30) not null,

            status varchar(30) not null,

            component_type varchar(40) not null,

            component_code varchar(80) not null,

            title varchar(200) not null,

            description text,

            current_level varchar(30) not null,

            assigned_to varchar(120),

            opened_at timestamptz(7) not null,

            acknowledged_at timestamptz(7) null,

            resolved_at timestamptz(7) null,

            closed_at timestamptz(7) null,

            root_cause text,

            corrective_action text,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='ops_incidents' and indexname='ix_ops_incidents_status') THEN
            create index if not exists ix_ops_incidents_status on dbo.ops_incidents (status, severity, opened_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_sla_policies') is null THEN

        create table if not exists dbo.ops_sla_policies (

            policy_id uuid primary key,

            policy_code varchar(80) not null unique,

            target_type varchar(50) not null,

            component_type varchar(40) null,

            threshold_value numeric(18,6) not null,

            unit varchar(30) not null,

            warning_minutes integer not null,

            breach_minutes integer not null,

            escalate_to varchar(30) not null,

            enabled boolean not null default true,

            created_at timestamptz(7) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_sla_evaluations') is null THEN

        create table if not exists dbo.ops_sla_evaluations (

            evaluation_id uuid primary key,

            policy_id uuid not null references dbo.ops_sla_policies(policy_id),

            policy_code varchar(80) not null,

            status varchar(30) not null,

            observed_value numeric(18,6) not null,

            threshold_value numeric(18,6) not null,

            unit varchar(30) not null,

            message varchar(500) not null,

            incident_id uuid null references dbo.ops_incidents(incident_id),

            evaluated_at timestamptz(7) not null,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='ops_sla_evaluations' and indexname='ix_ops_sla_eval_status') THEN
            create index if not exists ix_ops_sla_eval_status on dbo.ops_sla_evaluations (status, evaluated_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_escalation_rules') is null THEN

        create table if not exists dbo.ops_escalation_rules (

            rule_id uuid primary key,

            severity varchar(30) not null,

            from_level varchar(30) not null,

            to_level varchar(30) not null,

            escalate_after_minutes integer not null,

            notify_group varchar(120) not null,

            enabled boolean not null default true

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_technical_declines') is null THEN

        create table if not exists dbo.ops_technical_declines (

            decline_id uuid primary key,

            transaction_reference varchar(80) not null,

            category varchar(40) not null,

            component_code varchar(80) not null,

            response_code varchar(20) not null,

            reason varchar(500) not null,

            channel varchar(40) not null,

            amount numeric(18,2) not null,

            currency_code varchar(3) not null,

            occurred_at timestamptz(7) not null,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='ops_technical_declines' and indexname='ix_ops_declines_time') THEN
            create index if not exists ix_ops_declines_time on dbo.ops_technical_declines (occurred_at desc, category, channel);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_rca_cases') is null THEN

        create table if not exists dbo.ops_rca_cases (

            rca_id uuid primary key,

            incident_id uuid not null references dbo.ops_incidents(incident_id),

            interim_report text,

            final_report text,

            root_cause text not null,

            corrective_action text not null,

            preventive_action text not null,

            prepared_by varchar(120) not null,

            due_at timestamptz(7) not null,

            submitted_at timestamptz(7) null,

            status varchar(30) not null,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_dr_drills') is null THEN

        create table if not exists dbo.ops_dr_drills (

            drill_id uuid primary key,

            drill_code varchar(80) not null unique,

            status varchar(30) not null,

            planned_at timestamptz(7) not null,

            started_at timestamptz(7) null,

            completed_at timestamptz(7) null,

            observed_rpo_minutes integer not null default 0,

            observed_rto_minutes integer not null default 0,

            evidence_file varchar(500),

            report text,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_capacity_metrics') is null THEN

        create table if not exists dbo.ops_capacity_metrics (

            metric_id uuid primary key,

            metric_type varchar(40) not null,

            component_code varchar(80) not null,

            value numeric(18,6) not null,

            unit varchar(30) not null,

            captured_at timestamptz(7) not null,

            audit_hash varchar(128) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='ops_capacity_metrics' and indexname='ix_ops_capacity_component') THEN
            create index if not exists ix_ops_capacity_component on dbo.ops_capacity_metrics (component_code, metric_type, captured_at desc);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ops_regulatory_uptime_reports') is null THEN

        create table if not exists dbo.ops_regulatory_uptime_reports (

            report_id uuid primary key,

            from_date date not null,

            to_date date not null,

            switch_availability numeric(7,4) not null,

            atm_availability numeric(7,4) not null,

            pos_availability numeric(7,4) not null,

            technical_declines integer not null,

            incidents integer not null,

            sla_breaches integer not null,

            file_name varchar(300) not null,

            sha256_hash varchar(128) not null,

            generated_at timestamptz(7) not null

        );

    END IF;
END $$;

-- ===== 034_regulatory_compliance_audit_evidence_pack.sql =====

-- V41 Regulatory Compliance, Audit & Evidence Pack Core

-- Adds tables for RBI DPSC/PCI/ISO/NPCI/Visa/Mastercard evidence packs,

-- audit observations, VAPT/AppSec findings, secure SDLC evidence,

-- maker-checker evidence, access reviews, retention policies and generated packs.

create table if not exists dbo.compliance_controls (

    control_id uuid not null primary key,

    framework varchar(40) not null,

    control_code varchar(80) not null,

    control_title varchar(256) not null,

    description text null,

    owner_role varchar(80) not null,

    status varchar(40) not null,

    effective_from date not null,

    effective_to date null,

    updated_at timestamptz not null,

    updated_by varchar(128) not null,

    audit_hash char(64) not null,

    constraint uq_compliance_controls unique(framework, control_code)

);

create table if not exists dbo.compliance_evidence_items (

    evidence_id uuid not null primary key,

    control_id uuid null,

    framework varchar(40) not null,

    control_code varchar(80) not null,

    evidence_type varchar(40) not null,

    title varchar(256) not null,

    description text null,

    file_name varchar(512) null,

    storage_uri varchar(1024) null,

    sha256_hash char(64) not null,

    collected_by varchar(128) not null,

    collected_at timestamptz not null,

    valid_until date null,

    audit_hash char(64) not null,

    constraint fk_compliance_evidence_control foreign key(control_id) references dbo.compliance_controls(control_id)

);

create index if not exists ix_compliance_evidence_framework_control on dbo.compliance_evidence_items(framework, control_code);

create table if not exists dbo.audit_observations (

    observation_id uuid not null primary key,

    observation_number varchar(64) not null unique,

    source varchar(40) not null,

    severity varchar(40) not null,

    status varchar(40) not null,

    framework varchar(40) not null,

    control_code varchar(80) not null,

    title varchar(256) not null,

    details text not null,

    remediation_plan text null,

    assigned_to varchar(128) null,

    due_date date not null,

    created_at timestamptz not null,

    closed_at timestamptz null,

    audit_hash char(64) not null

);

create index if not exists ix_audit_observations_status_due on dbo.audit_observations(status, due_date);

create table if not exists dbo.security_findings (

    finding_id uuid not null primary key,

    finding_number varchar(64) not null unique,

    source varchar(40) not null,

    severity varchar(40) not null,

    status varchar(40) not null,

    component varchar(160) not null,

    cwe_or_owasp varchar(80) null,

    title varchar(256) not null,

    description text not null,

    remediation text null,

    target_date date not null,

    created_at timestamptz not null,

    closed_at timestamptz null,

    audit_hash char(64) not null

);

create index if not exists ix_security_findings_status_target on dbo.security_findings(status, target_date);

create table if not exists dbo.secure_sdlc_artifacts (

    artifact_id uuid not null primary key,

    release_version varchar(80) not null,

    artifact_type varchar(80) not null,

    title varchar(256) not null,

    repository_ref varchar(512) null,

    build_number varchar(120) null,

    commit_hash varchar(128) null,

    evidence_hash char(64) not null,

    approved_by varchar(128) null,

    created_at timestamptz not null,

    audit_hash char(64) not null

);

create index if not exists ix_secure_sdlc_release on dbo.secure_sdlc_artifacts(release_version, artifact_type);

create table if not exists dbo.maker_checker_evidence (

    evidence_id uuid not null primary key,

    change_reference varchar(128) not null,

    module varchar(120) not null,

    maker varchar(128) not null,

    checker varchar(128) not null,

    decision varchar(40) not null,

    change_summary text not null,

    maker_at timestamptz not null,

    checker_at timestamptz not null,

    audit_hash char(64) not null

);

create index if not exists ix_maker_checker_module on dbo.maker_checker_evidence(module, checker_at desc);

create table if not exists dbo.access_review_campaigns (

    campaign_id uuid not null primary key,

    campaign_code varchar(80) not null unique,

    scope varchar(512) not null,

    status varchar(40) not null,

    review_period_start date not null,

    review_period_end date not null,

    owner varchar(128) not null,

    users_reviewed int not null default 0,

    exceptions_found int not null default 0,

    created_at timestamptz not null,

    closed_at timestamptz null,

    audit_hash char(64) not null

);

create table if not exists dbo.data_retention_policies (

    policy_id uuid not null primary key,

    data_set varchar(160) not null unique,

    retention_days int not null,

    default_action varchar(40) not null,

    legal_hold boolean not null default false,

    owner_role varchar(80) not null,

    created_at timestamptz not null,

    audit_hash char(64) not null

);

create table if not exists dbo.retention_executions (

    execution_id uuid not null primary key,

    policy_id uuid not null,

    data_set varchar(160) not null,

    action varchar(40) not null,

    records_evaluated int not null,

    records_actioned int not null,

    output_file varchar(512) null,

    sha256_hash char(64) not null,

    executed_at timestamptz not null,

    audit_hash char(64) not null,

    constraint fk_retention_execution_policy foreign key(policy_id) references dbo.data_retention_policies(policy_id)

);

create table if not exists dbo.compliance_packs (

    pack_id uuid not null primary key,

    framework varchar(40) not null,

    from_date date not null,

    to_date date not null,

    file_name varchar(512) not null,

    sha256_hash char(64) not null,

    controls int not null,

    evidence_items int not null,

    open_findings int not null,

    generated_at timestamptz not null,

    audit_hash char(64) not null

);

create index if not exists ix_compliance_packs_framework_date on dbo.compliance_packs(framework, to_date desc);

-- ===== 035_realtime_fraud_risk_aml_production_core.sql =====

-- v42 Real-Time Fraud Risk & AML Production Core

-- Adds DB-backed structures for rules, watchlists, AML screening, risk cases and risk model profiles.

DO $$
BEGIN
    IF to_regclass('dbo.risk_rules') is null THEN

        create table if not exists dbo.risk_rules (

            rule_id uuid primary key,

            rule_code varchar(64) not null unique,

            category varchar(40) not null,

            description text not null,

            expression text not null,

            score int not null check (score between 0 and 100),

            action varchar(40) not null,

            enabled boolean not null default true,

            priority int not null default 100,

            updated_at timestamptz(7) not null,

            updated_by varchar(128) not null,

            audit_hash char(64) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_list_entries') is null THEN

        create table if not exists dbo.risk_list_entries (

            entry_id uuid primary key,

            list_type varchar(40) not null,

            entity_type varchar(40) not null,

            entity_value varchar(256) not null,

            reason text not null,

            source varchar(128) not null,

            effective_from date not null,

            effective_to date null,

            enabled boolean not null default true,

            created_at timestamptz(7) not null,

            created_by varchar(128) not null,

            audit_hash char(64) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='risk_list_entries' and indexname='ix_risk_list_lookup') THEN
            create index if not exists ix_risk_list_lookup on dbo.risk_list_entries (list_type, entity_type, entity_value) where enabled=true;
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_evaluations') is null THEN

        create table if not exists dbo.risk_evaluations (

            evaluation_id uuid primary key,

            correlation_id varchar(128) not null,

            pan_hash varchar(128) not null,

            account_number_hash varchar(128) not null,

            merchant_id varchar(64) not null,

            terminal_id varchar(64) not null,

            channel varchar(32) not null,

            country_code char(2) not null,

            currency_code char(3) not null,

            amount numeric(18,2) not null,

            network varchar(32) not null,

            product_code varchar(64) not null,

            decision varchar(40) not null,

            total_score int not null,

            response_code varchar(8) not null,

            reason text not null,

            hits_json text not null,

            evaluated_at timestamptz(7) not null,

            audit_hash char(64) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='risk_evaluations' and indexname='ix_risk_eval_date_decision') THEN
            create index if not exists ix_risk_eval_date_decision on dbo.risk_evaluations (evaluated_at, decision);
    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='risk_evaluations' and indexname='ix_risk_eval_corr') THEN
            create index if not exists ix_risk_eval_corr on dbo.risk_evaluations (correlation_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.aml_screenings') is null THEN

        create table if not exists dbo.aml_screenings (

            screening_id uuid primary key,

            correlation_id varchar(128) not null,

            entity_type varchar(40) not null,

            entity_name_or_value varchar(256) not null,

            country_code char(2) not null,

            identification_number_hash varchar(128) not null,

            source_system varchar(64) not null,

            status varchar(40) not null,

            match_score int not null,

            matched_list varchar(64) not null,

            matched_value varchar(256) not null,

            disposition text not null,

            screened_at timestamptz(7) not null,

            audit_hash char(64) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='aml_screenings' and indexname='ix_aml_status_date') THEN
            create index if not exists ix_aml_status_date on dbo.aml_screenings (status, screened_at);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_cases') is null THEN

        create table if not exists dbo.risk_cases (

            case_id uuid primary key,

            case_number varchar(40) not null unique,

            correlation_id varchar(128) not null,

            status varchar(40) not null,

            decision varchar(40) not null,

            score int not null,

            title varchar(256) not null,

            details text not null,

            assigned_to varchar(128) not null,

            created_at timestamptz(7) not null,

            closed_at timestamptz(7) null,

            audit_hash char(64) not null

        );

    END IF;
END $$;

DO $$
BEGIN
    IF not exists (select 1 from pg_indexes where schemaname='dbo' and tablename='risk_cases' and indexname='ix_risk_case_status') THEN
            create index if not exists ix_risk_case_status on dbo.risk_cases (status, created_at);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_model_profiles') is null THEN

        create table if not exists dbo.risk_model_profiles (

            model_id uuid primary key,

            model_code varchar(64) not null unique,

            model_name varchar(128) not null,

            status varchar(40) not null,

            version varchar(40) not null,

            feature_set_json text not null,

            review_threshold int not null,

            decline_threshold int not null,

            updated_at timestamptz(7) not null,

            updated_by varchar(128) not null,

            audit_hash char(64) not null

        );

    END IF;
END $$;

-- ===== 036_persistent_pos_terminal_driving_repository.sql =====

-- V43 Persistent POS Terminal Driving Repository

-- SQL Server migration for v32 POS/mPOS/eCommerce terminal-driving records.

-- Complements v33 PosAcquiringProduction persistence and removes the SQL-provider dependency on in-memory v32 records.

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') is null THEN

            create table if not exists dbo.posterminalprofiles (

                terminalid varchar(64) not null primary key,

                merchantid varchar(64) not null,

                vendor varchar(32) not null,

                protocol varchar(32) not null,

                serialnumber varchar(128) not null,

                devicemodel varchar(128) not null,

                branchcode varchar(64) not null,

                locationcode varchar(64) not null,

                countrycode varchar(8) not null,

                currencycode varchar(8) not null,

                ismpos boolean not null,

                contactlessenabled boolean not null,

                status varchar(32) not null,

                capabilitiesjson text not null,

                createdat timestamptz not null,

                updatedat timestamptz not null

            );

            create index if not exists ix_posterminalprofiles_merchant on dbo.posterminalprofiles(merchantid, status);

            create index if not exists ix_posterminalprofiles_vendorprotocol on dbo.posterminalprofiles(vendor, protocol, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') is null THEN

            create table if not exists dbo.posmposenrollments (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                devicebindingid varchar(256) not null,

                mobilenumbermasked varchar(64) not null,

                appversion varchar(64) not null,

                osname varchar(64) not null,

                osversion varchar(64) not null,

                status varchar(32) not null,

                enrolledat timestamptz not null,

                updatedat timestamptz not null

            );

            create index if not exists ix_posmposenrollments_terminal on dbo.posmposenrollments(terminalid, status);

            create index if not exists ix_posmposenrollments_merchant on dbo.posmposenrollments(merchantid, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadcertifications') is null THEN

            create table if not exists dbo.poskeydownloadcertifications (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                vendor varchar(32) not null,

                protocol varchar(32) not null,

                scheme varchar(32) not null,

                keyscheme varchar(64) not null,

                certificationpackreference varchar(256) not null,

                evidencehash varchar(128) not null,

                status varchar(32) not null,

                certifiedat timestamptz not null,

                remarks varchar(1000) not null

            );

            create index if not exists ix_poskeydownloadcertifications_terminal on dbo.poskeydownloadcertifications(terminalid, scheme, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadsessions') is null THEN

            create table if not exists dbo.poskeydownloadsessions (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                scheme varchar(32) not null,

                tmkkcv varchar(16) not null,

                tpkkcv varchar(16) not null,

                takkcv varchar(16) not null,

                status varchar(32) not null,

                requestedat timestamptz not null,

                completedat timestamptz null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poskeydownloadsessions_terminal on dbo.poskeydownloadsessions(terminalid, scheme, status, requestedat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') is null THEN

            create table if not exists dbo.poscontactlesstransactionflows (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                mode varchar(32) not null,

                panmasked varchar(32) not null,

                amount decimal(18,2) not null,

                currencycode varchar(8) not null,

                emvcryptogram varchar(512) not null,

                offlineapprovedbyterminal boolean not null,

                onlinehostauthorised boolean not null,

                responsecode varchar(8) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poscontactlesstransactionflows_merchant on dbo.poscontactlesstransactionflows(merchantid, currencycode, createdat);

            create index if not exists ix_poscontactlesstransactionflows_terminal on dbo.poscontactlesstransactionflows(terminalid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') is null THEN

            create table if not exists dbo.postipadjustments (

                id uuid not null primary key,

                originaltransactionid varchar(128) not null,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                originalamount decimal(18,2) not null,

                tipamount decimal(18,2) not null,

                finalamount decimal(18,2) not null,

                currencycode varchar(8) not null,

                approvalcode varchar(64) not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

            create unique index if not exists ux_postipadjustments_original on dbo.postipadjustments(originaltransactionid);

            create index if not exists ix_postipadjustments_merchant on dbo.postipadjustments(merchantid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') is null THEN

            create table if not exists dbo.poscashatposacquiring (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                merchantid varchar(64) not null,

                panmasked varchar(32) not null,

                purchaseamount decimal(18,2) not null,

                cashamount decimal(18,2) not null,

                totalamount decimal(18,2) not null,

                currencycode varchar(8) not null,

                approvalcode varchar(64) not null,

                responsecode varchar(8) not null,

                createdat timestamptz not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_poscashatposacquiring_merchant on dbo.poscashatposacquiring(merchantid, currencycode, createdat);

            create index if not exists ix_poscashatposacquiring_terminal on dbo.poscashatposacquiring(terminalid, createdat);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementbatches') is null THEN

            create table if not exists dbo.posmerchantsettlementbatches (

                id uuid not null primary key,

                merchantid varchar(64) not null,

                settlementdate date not null,

                currencycode varchar(8) not null,

                transactioncount int not null,

                grossamount decimal(18,2) not null,

                interchangefee decimal(18,2) not null,

                mdrfee decimal(18,2) not null,

                gstamount decimal(18,2) not null,

                netpayable decimal(18,2) not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                filehash varchar(128) not null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_posmerchantsettlementbatches_merchant on dbo.posmerchantsettlementbatches(merchantid, settlementdate, status);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') is null THEN

            create table if not exists dbo.posdevicecommands (

                id uuid not null primary key,

                terminalid varchar(64) not null,

                command varchar(128) not null,

                parametersjson text not null,

                status varchar(32) not null,

                createdat timestamptz not null,

                appliedat timestamptz null,

                correlationid varchar(128) not null

            );

            create index if not exists ix_posdevicecommands_terminal on dbo.posdevicecommands(terminalid, status, createdat);

    END IF;
END $$;

-- ===== 037_repository_persistence_data_integrity_hardening.sql =====
-- PostgreSQL equivalent of the v44 persistence hardening migration.

do $$
begin
    if to_regclass('dbo.kycdocuments') is null then
        raise exception 'dbo.kycdocuments is missing. Apply migration 010_card_lifecycle.sql before v44.';
    end if;
end $$;

alter table dbo.kycdocuments add column if not exists payloadjson text;
alter table dbo.kycdocuments add column if not exists createdat timestamptz not null default clock_timestamp();
alter table dbo.kycdocuments add column if not exists updatedat timestamptz not null default clock_timestamp();
alter table dbo.kycdocuments add column if not exists rowversion bytea;

update dbo.kycdocuments
set payloadjson = json_build_object(
    'Id', id, 'CustomerId', customerid, 'CustomerNumber', customernumber,
    'DocumentType', documenttype, 'DocumentNumber', documentnumber,
    'IssuingAuthority', issuingauthority, 'IssuingCountryCode', issuingcountrycode,
    'IssueDate', issuedate, 'ExpiryDate', expirydate, 'Status', status,
    'DocumentVaultReference', documentvaultreference, 'ProviderVerificationId', providerverificationid,
    'RejectionReason', rejectionreason, 'SubmittedBy', submittedby, 'ReviewedBy', reviewedby,
    'SubmittedAt', submittedat, 'ReviewedAt', reviewedat
)::text
where payloadjson is null;

alter table dbo.kycdocuments alter column payloadjson set not null;

do $$
begin
    if not exists (
        select 1 from pg_constraint c
        join pg_class t on t.oid = c.conrelid
        join pg_namespace n on n.oid = t.relnamespace
        where c.conname = 'ck_kycdocuments_payloadjson'
          and n.nspname = 'dbo' and t.relname = 'kycdocuments'
    ) then
        alter table dbo.kycdocuments
            add constraint ck_kycdocuments_payloadjson check (payloadjson::jsonb is not null);
    end if;
end $$;

create index if not exists ix_kycdocuments_customernumber_status
    on dbo.kycdocuments (customernumber, status, submittedat desc);
create index if not exists ix_kycdocuments_documentnumber
    on dbo.kycdocuments (documentnumber);

do $$
begin
    if to_regclass('dbo.authorizationholds') is null then
        raise exception 'dbo.authorizationholds is missing. Apply migration 010_card_lifecycle.sql before v44.';
    end if;
end $$;

alter table dbo.authorizationholds add column if not exists payloadjson text;
alter table dbo.authorizationholds add column if not exists createdat timestamptz not null default clock_timestamp();
alter table dbo.authorizationholds add column if not exists updatedat timestamptz not null default clock_timestamp();
alter table dbo.authorizationholds add column if not exists rowversion bytea;

update dbo.authorizationholds
set payloadjson = json_build_object(
    'Id', id, 'WalletAccountId', walletaccountid, 'CardId', cardid,
    'CorrelationId', correlationid, 'Stan', stan, 'Rrn', rrn,
    'AuthorizationCode', authorizationcode, 'HoldAmount', holdamount,
    'CurrencyCode', currencycode, 'MerchantId', merchantid, 'MerchantName', merchantname,
    'TerminalId', terminalid, 'Status', status, 'PlacedAt', placedat,
    'ExpiresAt', expiresat, 'ReleasedAt', releasedat, 'CapturedAmount', capturedamount,
    'CaptureCorrelationId', capturecorrelationid
)::text
where payloadjson is null;

alter table dbo.authorizationholds alter column payloadjson set not null;

do $$
begin
    if not exists (
        select 1 from pg_constraint c
        join pg_class t on t.oid = c.conrelid
        join pg_namespace n on n.oid = t.relnamespace
        where c.conname = 'ck_authorizationholds_payloadjson'
          and n.nspname = 'dbo' and t.relname = 'authorizationholds'
    ) then
        alter table dbo.authorizationholds
            add constraint ck_authorizationholds_payloadjson check (payloadjson::jsonb is not null);
    end if;
end $$;

create index if not exists ix_authorizationholds_expiry
    on dbo.authorizationholds (status, expiresat)
    include (walletaccountid, rrn);

-- Dynamic SQL in the SQL Server source created three fixed certification stores.
-- They are materialized explicitly here; no extra tables are introduced.
create table if not exists dbo.acquiringcertificationstore
(
    id uuid not null constraint pk_acquiringcertificationstore primary key,
    recordtype varchar(64) not null,
    scheme varchar(64) null,
    parentid uuid null,
    secondarykey varchar(160) null,
    status varchar(64) null,
    occurredat timestamptz(7) not null,
    payloadjson text not null,
    createdat timestamptz(7) not null constraint df_acquiringcertificationstore_createdat default clock_timestamp(),
    updatedat timestamptz(7) not null constraint df_acquiringcertificationstore_updatedat default clock_timestamp(),
    rowversion bytea not null,
    constraint ck_acquiringcertificationstore_payloadjson check (payloadjson::jsonb is not null)
);
create index if not exists ix_acquiringcertificationstore_type_scheme_time on dbo.acquiringcertificationstore(recordtype, scheme, occurredat desc);
create index if not exists ix_acquiringcertificationstore_type_parent_time on dbo.acquiringcertificationstore(recordtype, parentid, occurredat desc);
create index if not exists ix_acquiringcertificationstore_type_key on dbo.acquiringcertificationstore(recordtype, secondarykey) include(status, occurredat);

create table if not exists dbo.acquiringcertificationlabstore
(
    id uuid not null constraint pk_acquiringcertificationlabstore primary key,
    recordtype varchar(64) not null,
    scheme varchar(64) null,
    parentid uuid null,
    secondarykey varchar(160) null,
    status varchar(64) null,
    occurredat timestamptz(7) not null,
    payloadjson text not null,
    createdat timestamptz(7) not null constraint df_acquiringcertificationlabstore_createdat default clock_timestamp(),
    updatedat timestamptz(7) not null constraint df_acquiringcertificationlabstore_updatedat default clock_timestamp(),
    rowversion bytea not null,
    constraint ck_acquiringcertificationlabstore_payloadjson check (payloadjson::jsonb is not null)
);
create index if not exists ix_acquiringcertificationlabstore_type_scheme_time on dbo.acquiringcertificationlabstore(recordtype, scheme, occurredat desc);
create index if not exists ix_acquiringcertificationlabstore_type_parent_time on dbo.acquiringcertificationlabstore(recordtype, parentid, occurredat desc);
create index if not exists ix_acquiringcertificationlabstore_type_key on dbo.acquiringcertificationlabstore(recordtype, secondarykey) include(status, occurredat);

create table if not exists dbo.issuercertificationstore
(
    id uuid not null constraint pk_issuercertificationstore primary key,
    recordtype varchar(64) not null,
    scheme varchar(64) null,
    parentid uuid null,
    secondarykey varchar(160) null,
    status varchar(64) null,
    occurredat timestamptz(7) not null,
    payloadjson text not null,
    createdat timestamptz(7) not null constraint df_issuercertificationstore_createdat default clock_timestamp(),
    updatedat timestamptz(7) not null constraint df_issuercertificationstore_updatedat default clock_timestamp(),
    rowversion bytea not null,
    constraint ck_issuercertificationstore_payloadjson check (payloadjson::jsonb is not null)
);
create index if not exists ix_issuercertificationstore_type_scheme_time on dbo.issuercertificationstore(recordtype, scheme, occurredat desc);
create index if not exists ix_issuercertificationstore_type_parent_time on dbo.issuercertificationstore(recordtype, parentid, occurredat desc);
create index if not exists ix_issuercertificationstore_type_key on dbo.issuercertificationstore(recordtype, secondarykey) include(status, occurredat);

create index if not exists ix_posmerchants_status_mcc
    on dbo.posmerchants(status, mcc) include(settlementcurrencycode, settlementcycle);
create index if not exists ix_poscommandqueue_dispatch
    on dbo.poscommandqueue(status, notbefore, expiresat, attemptcount) include(terminalid, command, maxattempts);
create index if not exists ix_posmerchantsettlementpostings_merchantdate
    on dbo.posmerchantsettlementpostings(merchantid, settlementdate desc, status);

-- ===== 038_enterprise_configuration_control_plane.sql =====

/* BankSwitch v44.1 — Enterprise Configuration Control Plane

   Persistent, versioned, maker-checker governed configuration management.

*/


begin;

DO $$
BEGIN
    IF to_regclass('dbo.configurationdomains') is null THEN
        create table if not exists dbo.configurationdomains(
        
            code varchar(64) not null constraint pk_configurationdomains primary key,
        
            name varchar(128) not null,
        
            description varchar(512) not null constraint df_configdomains_description default(''),
        
            displayorder int not null constraint df_configdomains_order default(0),
        
            enabled boolean not null constraint df_configdomains_enabled default(true),
        
            createdat timestamptz not null constraint df_configdomains_created default(clock_timestamp())
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.configurationdefinitions') is null THEN
        create table if not exists dbo.configurationdefinitions(
        
            id uuid not null constraint pk_configurationdefinitions primary key,
        
            domaincode varchar(64) not null,
        
            key varchar(160) not null,
        
            displayname varchar(160) not null,
        
            description varchar(1024) not null constraint df_configdefinitions_description default(''),
        
            valuetype varchar(32) not null,
        
            defaultvalue text null,
        
            allowedvaluesjson text null,
        
            minimumvalue decimal(28,8) null,
        
            maximumvalue decimal(28,8) null,
        
            sensitivity varchar(32) not null,
        
            reloadpolicy varchar(32) not null,
        
            requiresapproval boolean not null,
        
            issecret boolean not null constraint df_configdefinitions_issecret default(false),
        
            issensitive boolean not null constraint df_configdefinitions_issensitive default(false),
        
            productionlocked boolean not null constraint df_configdefinitions_prodlocked default(false),
        
            validationpattern varchar(1000) null,
        
            validationexpression varchar(2000) null,
        
            displayorder int not null constraint df_configdefinitions_order default(0),
        
            enabled boolean not null constraint df_configdefinitions_enabled default(true),
        
            createdat timestamptz not null constraint df_configdefinitions_created default(clock_timestamp()),
        
            constraint fk_configurationdefinitions_domain foreign key(domaincode) references dbo.configurationdomains(code),
        
            constraint uq_configurationdefinitions unique(domaincode,key),
        
            constraint ck_configurationdefinitions_allowedjson check(allowedvaluesjson is null or (allowedvaluesjson::jsonb is not null))
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.configurationvalues') is null THEN
        create table if not exists dbo.configurationvalues(
        
            id uuid not null constraint pk_configurationvalues primary key,
        
            definitionid uuid not null,
        
            environment varchar(32) not null,
        
            institutionscope varchar(128) not null,
        
            value text not null,
        
            version bigint not null,
        
            effectivefrom timestamptz not null,
        
            effectiveto timestamptz null,
        
            updatedby varchar(160) not null,
        
            updatedat timestamptz not null,
        
            rowversion bytea not null,
        
            constraint fk_configurationvalues_definition foreign key(definitionid) references dbo.configurationdefinitions(id)
        
        );
    END IF;
END $$;

create unique index if not exists ux_configurationvalues_active on dbo.configurationvalues(definitionid,environment,institutionscope) where effectiveto is null;

create index if not exists ix_configurationvalues_scope on dbo.configurationvalues(environment,institutionscope,version desc);

DO $$
BEGIN
    IF to_regclass('dbo.configurationchangerequests') is null THEN
        create table if not exists dbo.configurationchangerequests(
        
            id uuid not null constraint pk_configurationchangerequests primary key,
        
            correlationid varchar(64) not null,
        
            environment varchar(32) not null,
        
            institutionscope varchar(128) not null,
        
            maker varchar(160) not null,
        
            checker varchar(160) null,
        
            reason varchar(1000) not null,
        
            ticketreference varchar(128) not null,
        
            effectiveat timestamptz null,
        
            state varchar(32) not null,
        
            createdat timestamptz not null,
        
            updatedat timestamptz not null,
        
            submittedat timestamptz null,
        
            approvedat timestamptz null,
        
            appliedat timestamptz null,
        
            rejectionreason varchar(1000) null,
        
            rowversion bytea not null,
        
            constraint uq_configurationchangerequests_correlation unique(correlationid)
        
        );
    END IF;
END $$;

create index if not exists ix_configurationchangerequests_state on dbo.configurationchangerequests(state,createdat desc);

create index if not exists ix_configurationchangerequests_scope on dbo.configurationchangerequests(environment,institutionscope,createdat desc);

DO $$
BEGIN
    IF to_regclass('dbo.configurationchangeitems') is null THEN
        create table if not exists dbo.configurationchangeitems(
        
            id uuid not null constraint pk_configurationchangeitems primary key,
        
            changerequestid uuid not null,
        
            definitionid uuid not null,
        
            domaincode varchar(64) not null,
        
            key varchar(160) not null,
        
            oldvalue text null,
        
            newvalue text not null,
        
            issecretreference boolean not null,
        
            reloadpolicy varchar(32) not null,
        
            constraint fk_configurationchangeitems_request foreign key(changerequestid) references dbo.configurationchangerequests(id) on delete cascade,
        
            constraint fk_configurationchangeitems_definition foreign key(definitionid) references dbo.configurationdefinitions(id)
        
        );
    END IF;
END $$;

create index if not exists ix_configurationchangeitems_request on dbo.configurationchangeitems(changerequestid);

DO $$
BEGIN
    IF to_regclass('dbo.configurationhistory') is null THEN
        create table if not exists dbo.configurationhistory(
        
            id bigint generated by default as identity not null constraint pk_configurationhistory primary key,
        
            version bigint not null,
        
            definitionid uuid not null,
        
            domaincode varchar(64) not null,
        
            key varchar(160) not null,
        
            environment varchar(32) not null,
        
            institutionscope varchar(128) not null,
        
            oldvalue text null,
        
            newvalue text not null,
        
            changedby varchar(160) not null,
        
            changerequestid uuid null,
        
            changedat timestamptz not null,
        
            reason varchar(1000) not null,
        
            hash char(64) not null,
        
            constraint fk_configurationhistory_definition foreign key(definitionid) references dbo.configurationdefinitions(id)
        
        );
    END IF;
END $$;

create unique index if not exists ux_configurationhistory_version on dbo.configurationhistory(version);

create index if not exists ix_configurationhistory_scope on dbo.configurationhistory(environment,institutionscope,changedat desc);

create index if not exists ix_configurationhistory_key on dbo.configurationhistory(domaincode,key,changedat desc);

DO $$
BEGIN
    IF to_regclass('dbo.configurationsnapshots') is null THEN
        create table if not exists dbo.configurationsnapshots(
        
            id uuid not null constraint pk_configurationsnapshots primary key,
        
            name varchar(200) not null,
        
            environment varchar(32) not null,
        
            institutionscope varchar(128) not null,
        
            version bigint not null,
        
            checksum char(64) not null,
        
            createdby varchar(160) not null,
        
            createdat timestamptz not null,
        
            payloadjson text not null,
        
            constraint ck_configurationsnapshots_json check((payloadjson::jsonb is not null))
        
        );
    END IF;
END $$;

create index if not exists ix_configurationsnapshots_scope on dbo.configurationsnapshots(environment,institutionscope,createdat desc);

DO $$
BEGIN
    IF to_regclass('dbo.configurationdeployments') is null THEN
        create table if not exists dbo.configurationdeployments(
        
            id uuid not null constraint pk_configurationdeployments primary key,
        
            changerequestid uuid not null,
        
            success boolean not null,
        
            status varchar(64) not null,
        
            highestreloadpolicy varchar(32) not null,
        
            startedat timestamptz not null,
        
            completedat timestamptz not null,
        
            message varchar(2000) not null,
        
            rolledback boolean not null,
        
            constraint fk_configurationdeployments_request foreign key(changerequestid) references dbo.configurationchangerequests(id)
        
        );
    END IF;
END $$;

create index if not exists ix_configurationdeployments_request on dbo.configurationdeployments(changerequestid,startedat desc);

DO $$
BEGIN
    IF to_regclass('dbo.featureflags') is null THEN
        create table if not exists dbo.featureflags(
        
            id uuid not null constraint pk_featureflags primary key,
        
            key varchar(160) not null,
        
            description varchar(512) not null,
        
            enabled boolean not null,
        
            environment varchar(32) not null,
        
            institutionscope varchar(128) not null,
        
            rolloutpercentage int not null,
        
            effectivefrom timestamptz null,
        
            effectiveto timestamptz null,
        
            updatedby varchar(160) not null,
        
            updatedat timestamptz not null,
        
            rowversion bytea not null,
        
            constraint ck_featureflags_rollout check(rolloutpercentage between 0 and 100),
        
            constraint uq_featureflags unique(key,environment,institutionscope)
        
        );
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.certificateinventory') is null THEN
        create table if not exists dbo.certificateinventory(
        
            id uuid not null constraint pk_certificateinventory primary key,
        
            name varchar(160) not null,
        
            purpose varchar(256) not null,
        
            environment varchar(32) not null,
        
            subject varchar(512) not null,
        
            issuer varchar(512) not null,
        
            thumbprint varchar(128) not null,
        
            validfrom timestamptz not null,
        
            validto timestamptz not null,
        
            secretreference varchar(512) not null,
        
            status varchar(32) not null,
        
            createdat timestamptz not null constraint df_certificateinventory_created default(clock_timestamp())
        
        );
    END IF;
END $$;

create index if not exists ix_certificateinventory_expiry on dbo.certificateinventory(environment,validto);

DO $$
BEGIN
    IF to_regclass('dbo.secretreferences') is null THEN
        create table if not exists dbo.secretreferences(
        
            id uuid not null constraint pk_secretreferences primary key,
        
            name varchar(160) not null,
        
            provider varchar(64) not null,
        
            reference varchar(512) not null,
        
            environment varchar(32) not null,
        
            version varchar(128) not null,
        
            lastrotatedat timestamptz null,
        
            expiresat timestamptz null,
        
            status varchar(32) not null,
        
            createdat timestamptz not null constraint df_secretreferences_created default(clock_timestamp()),
        
            constraint uq_secretreferences unique(name,environment)
        
        );
    END IF;
END $$;

-- 34 enterprise settings domains.
/*
insert into dbo.configurationdomains (code,name,description,displayorder,enabled)
values ('general', 'General / System', 'Institution, environment and business-date settings', true),

('api', 'API & Backend', 'API runtime, timeout and retry settings', true),

('realtime', 'Realtime / SignalR', 'Realtime event and reconnect settings', true),

('database', 'Database & Repositories', 'Repository provider and database performance settings', true),

('transactions', 'Transaction Processing', 'Authorization, reversal, SAF and idempotency settings', true),

('iso8583', 'ISO 8583', 'Message profile and field configuration', true),

('routing', 'Routing', 'Advanced switch routing controls', true),

('network-hosts', 'Network Hosts', 'Visa, Mastercard, RuPay and NPCI host controls', true),

('atm', 'ATM', 'ATM protocol and runtime controls', true),

('pos', 'POS / mPOS', 'POS and terminal-driving controls', true),

('merchant', 'Merchant Acquiring', 'Merchant, MDR and settlement controls', true),

('cards', 'Card Management', 'Card product and lifecycle controls', true),

('hsm', 'HSM & Key Management', 'HSM profile, key policy and rotation controls', true),

('fraud', 'Fraud / Risk', 'Fraud rules and scoring thresholds', true),

('aml', 'AML', 'AML, sanctions and PEP screening controls', true),

('settlement', 'Settlement', 'Network and merchant settlement controls', true),

('gl', 'GL / Accounting', 'GL mappings and financial posting controls', true),

('reconciliation', 'Reconciliation', 'Matching, tolerance and exception controls', true),

('disputes', 'Disputes / Chargeback', 'Dispute SLA and evidence controls', true),

('cbs', 'CBS / Finacle', 'Core banking integration controls', true),

('enterprise', 'Enterprise Integrations', 'ESB, ACS, FRM, DWH and notification controls', true),

('certification', 'Certification Lab', 'Simulator, test and evidence controls', true),

('security', 'Security', 'Identity, MFA, session and TLS controls', true),

('rbac', 'Users & RBAC', 'Role and permission control-plane settings', true),

('maker-checker', 'Maker / Checker', 'Four-eyes governance controls', true),

('audit', 'Audit', 'Audit retention and SIEM forwarding controls', true),

('compliance', 'Compliance', 'PCI, RBI, NPCI and ISO evidence controls', true),

('monitoring', 'Monitoring & SLA', 'Health and SLA threshold controls', true),

('alerts', 'Alerts', 'Alert channel and escalation controls', true),

('observability', 'Logging & Observability', 'Logging, metrics and tracing controls', true),

('dr', 'Disaster Recovery', 'RPO, RTO, failover and DR controls', true),

('retention', 'Data Retention', 'Archival and purge controls', true),

('feature-flags', 'Feature Flags', 'Controlled functional rollout settings', true),

('diagnostics', 'Diagnostics', 'Runtime health and diagnostics controls', true)
on conflict (code) do update set name=excluded.name, description=excluded.description, displayorder=excluded.displayorder, enabled=true;
*/

INSERT INTO dbo.configurationdomains
    (code, name, description, displayorder, enabled)
VALUES
    ('general', 'General / System', 'Institution, environment and business-date settings', 1, true),
    ('api', 'API & Backend', 'API runtime, timeout and retry settings', 2, true),
    ('realtime', 'Realtime / SignalR', 'Realtime event and reconnect settings', 3, true),
    ('database', 'Database & Repositories', 'Repository provider and database performance settings', 4, true),
    ('transactions', 'Transaction Processing', 'Authorization, reversal, SAF and idempotency settings', 5, true),
    ('iso8583', 'ISO 8583', 'Message profile and field configuration', 6, true),
    ('routing', 'Routing', 'Advanced switch routing controls', 7, true),
    ('network-hosts', 'Network Hosts', 'Visa, Mastercard, RuPay and NPCI host controls', 8, true),
    ('atm', 'ATM', 'ATM protocol and runtime controls', 9, true),
    ('pos', 'POS / mPOS', 'POS and terminal-driving controls', 10, true),
    ('merchant', 'Merchant Acquiring', 'Merchant, MDR and settlement controls', 11, true),
    ('cards', 'Card Management', 'Card product and lifecycle controls', 12, true),
    ('hsm', 'HSM & Key Management', 'HSM profile, key policy and rotation controls', 13, true),
    ('fraud', 'Fraud / Risk', 'Fraud rules and scoring thresholds', 14, true),
    ('aml', 'AML', 'AML, sanctions and PEP screening controls', 15, true),
    ('settlement', 'Settlement', 'Network and merchant settlement controls', 16, true),
    ('gl', 'GL / Accounting', 'GL mappings and financial posting controls', 17, true),
    ('reconciliation', 'Reconciliation', 'Matching, tolerance and exception controls', 18, true),
    ('disputes', 'Disputes / Chargeback', 'Dispute SLA and evidence controls', 19, true),
    ('cbs', 'CBS / Finacle', 'Core banking integration controls', 20, true),
    ('enterprise', 'Enterprise Integrations', 'ESB, ACS, FRM, DWH and notification controls', 21, true),
    ('certification', 'Certification Lab', 'Simulator, test and evidence controls', 22, true),
    ('security', 'Security', 'Identity, MFA, session and TLS controls', 23, true),
    ('rbac', 'Users & RBAC', 'Role and permission control-plane settings', 24, true),
    ('maker-checker', 'Maker / Checker', 'Four-eyes governance controls', 25, true),
    ('audit', 'Audit', 'Audit retention and SIEM forwarding controls', 26, true),
    ('compliance', 'Compliance', 'PCI, RBI, NPCI and ISO evidence controls', 27, true),
    ('monitoring', 'Monitoring & SLA', 'Health and SLA threshold controls', 28, true),
    ('alerts', 'Alerts', 'Alert channel and escalation controls', 29, true),
    ('observability', 'Logging & Observability', 'Logging, metrics and tracing controls', 30, true),
    ('dr', 'Disaster Recovery', 'RPO, RTO, failover and DR controls', 31, true),
    ('retention', 'Data Retention', 'Archival and purge controls', 32, true),
    ('feature-flags', 'Feature Flags', 'Controlled functional rollout settings', 33, true),
    ('diagnostics', 'Diagnostics', 'Runtime health and diagnostics controls', 34, true)
ON CONFLICT (code)
DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    displayorder = EXCLUDED.displayorder,
    enabled = true;
-- Core definitions. More domain-specific definitions can be registered without schema changes.

create temp table defs(domaincode varchar(64),key varchar(160),displayname varchar(160),description varchar(1024),valuetype varchar(32),defaultvalue text,allowed text,minval decimal(28,8),maxval decimal(28,8),sensitivity varchar(32),reloadpolicy varchar(32),requiresapproval boolean,issecret boolean,issensitive boolean,productionlocked boolean,displayorder int);

insert into defs values

('general', 'Environment', 'Environment', 'DEV/SIT/UAT/PREPROD/PROD/DR', 'Enum', 'DEV', '["DEV","SIT","UAT","PREPROD","PROD","DR"]', null, null, 'Critical', 'ClusterRestart', true, false, true, true, 10),

('general', 'InstitutionCode', 'Institution Code', 'Authoritative institution identifier', 'String', 'BANK', null, null, null, 'Critical', 'ServiceRestart', true, false, true, true, 20),

('general', 'BaseCurrency', 'Base Currency', 'ISO numeric/alphabetic base currency', 'String', 'INR', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, 30),

('general', 'TimeZone', 'Time Zone', 'Business timezone', 'String', 'Asia/Kolkata', null, null, null, 'Operational', 'HotReload', false, false, false, false, 40),

('api', 'RequestTimeoutSeconds', 'Request Timeout', 'Backend request timeout in seconds', 'Integer', '30', null, 1, 300, 'Operational', 'HotReload', false, false, false, false, 10),

('api', 'RetryCount', 'Retry Count', 'Transient retry count', 'Integer', '3', null, 0, 10, 'Operational', 'HotReload', false, false, false, false, 20),

('realtime', 'SignalREnabled', 'SignalR Enabled', 'Enable realtime operational events', 'Boolean', 'true', null, null, null, 'Operational', 'HotReload', false, false, false, false, 10),

('realtime', 'HeartbeatSeconds', 'Heartbeat Interval', 'Realtime heartbeat interval', 'Integer', '15', null, 5, 300, 'Operational', 'HotReload', false, false, false, false, 20),

('database', 'RepositoryProvider', 'Repository Provider', 'Persistence provider', 'Enum', 'SqlServer', '["SqlServer","InMemory"]', null, null, 'Critical', 'ClusterRestart', true, false, true, true, 10),

('database', 'CommandTimeoutSeconds', 'SQL Command Timeout', 'SQL command timeout', 'Integer', '30', null, 1, 300, 'Sensitive', 'ServiceRestart', true, false, false, false, 20),

('database', 'MaxPoolSize', 'SQL Max Pool Size', 'Maximum ADO.NET connection pool size', 'Integer', '200', null, 10, 2000, 'Sensitive', 'ServiceRestart', true, false, false, false, 30),

('transactions', 'AuthorizationTimeoutSeconds', 'Authorization Timeout', 'Authorization processing timeout', 'Integer', '30', null, 1, 120, 'Critical', 'HotReload', true, false, false, false, 10),

('transactions', 'DuplicateWindowSeconds', 'Duplicate Detection Window', 'Duplicate transaction protection window', 'Integer', '300', null, 1, 86400, 'Critical', 'HotReload', true, false, false, false, 20),

('transactions', 'SafEnabled', 'SAF Enabled', 'Enable store-and-forward', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, 30),

('transactions', 'AutoReversalEnabled', 'Auto Reversal', 'Enable automatic timeout reversal', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, 40),

('iso8583', 'DefaultProfile', 'Default ISO Profile', 'Default network message profile', 'String', 'ISO8583-1987', null, null, null, 'Critical', 'ServiceRestart', true, false, false, false, 10),

('iso8583', 'MacField', 'MAC Field', 'ISO MAC field number', 'Enum', '64', '["64","128"]', null, null, 'Critical', 'ServiceRestart', true, false, false, false, 20),

('routing', 'FallbackEnabled', 'Routing Fallback', 'Enable fallback route selection', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, 10),

('network-hosts', 'TlsEnabled', 'Network TLS', 'Require TLS for network hosts', 'Boolean', 'true', null, null, null, 'Critical', 'ConnectionRestart', true, false, true, true, 10),

('network-hosts', 'EchoIntervalSeconds', 'Echo Interval', 'Network management echo interval', 'Integer', '30', null, 5, 600, 'Sensitive', 'HotReload', true, false, false, false, 20),

('atm', 'HeartbeatSeconds', 'ATM Heartbeat', 'ATM heartbeat interval', 'Integer', '30', null, 5, 600, 'Operational', 'HotReload', false, false, false, false, 10),

('pos', 'OfflineFloorLimit', 'Offline Floor Limit', 'Maximum offline contactless floor limit', 'Decimal', '0', null, 0, 1000000, 'Critical', 'HotReload', true, false, false, false, 10),

('merchant', 'DefaultSettlementCycle', 'Settlement Cycle', 'Default merchant settlement cycle', 'Enum', 'T+1', '["T+0","T+1","T+2"]', null, null, 'Sensitive', 'HotReload', true, false, false, false, 10),

('cards', 'PinRetryLimit', 'PIN Retry Limit', 'Maximum PIN retry attempts', 'Integer', '3', null, 1, 10, 'Critical', 'HotReload', true, false, false, false, 10),

('hsm', 'HsmMode', 'HSM Mode', 'HSM operating mode', 'Enum', 'Http', '["Http","Thales","Atalla","Futurex","Mock","BypassForDevelopmentOnly"]', null, null, 'Critical', 'ServiceRestart', true, false, true, true, 10),

('hsm', 'KeyRotationDays', 'Key Rotation Days', 'Default key rotation cadence', 'Integer', '90', null, 1, 365, 'Critical', 'HotReload', true, false, true, false, 20),

('fraud', 'CriticalScoreThreshold', 'Critical Risk Score', 'Score at which transaction is critical', 'Integer', '90', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, 10),

('aml', 'RescreenHours', 'AML Rescreen Interval', 'Customer AML rescreen interval', 'Integer', '24', null, 1, 720, 'Sensitive', 'HotReload', true, false, false, false, 10),

('settlement', 'CutoffTimeUtc', 'Settlement Cutoff', 'Daily settlement cut-off UTC', 'String', '22:00:00', null, null, null, 'Critical', 'HotReload', true, false, false, false, 10),

('gl', 'BalanceTolerance', 'Balance Tolerance', 'Maximum GL imbalance tolerance', 'Decimal', '0', null, 0, 1000, 'Critical', 'HotReload', true, false, false, false, 10),

('reconciliation', 'AmountTolerance', 'Amount Tolerance', 'Automatic reconciliation amount tolerance', 'Decimal', '0', null, 0, 1000, 'Critical', 'HotReload', true, false, false, false, 10),

('disputes', 'AutoEscalationEnabled', 'Dispute Escalation', 'Enable automatic dispute escalation', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, 10),

('cbs', 'Endpoint', 'CBS Endpoint', 'Primary CBS integration endpoint', 'Uri', 'https://cbs.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, true, false, 10),

('enterprise', 'CircuitBreakerThreshold', 'Circuit Breaker Threshold', 'Enterprise integration failure threshold', 'Integer', '5', null, 1, 100, 'Sensitive', 'HotReload', true, false, false, false, 10),

('certification', 'SimulatorMode', 'Simulator Mode', 'Allow scheme simulator execution', 'Boolean', 'true', null, null, null, 'Sensitive', 'ServiceRestart', true, false, false, true, 10),

('security', 'AuthenticationMode', 'Authentication Mode', 'Administrative identity mode', 'Enum', 'OIDC', '["OIDC","AzureAD","Keycloak","Cookie","Disabled"]', null, null, 'Critical', 'ServiceRestart', true, false, true, true, 10),

('security', 'MfaRequired', 'MFA Required', 'Require MFA for privileged administration', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, true, true, 20),

('security', 'TlsMinimumVersion', 'TLS Minimum Version', 'Minimum TLS version', 'Enum', '1.2', '["1.2","1.3"]', null, null, 'Critical', 'ServiceRestart', true, false, true, true, 30),
/*
('security','AdminClientSecretRef','Admin Client Secret Reference','Vault/HSM reference only; never a secret value','SecretReference',null,null,null,null,'Critical','ServiceRestart',1,1,1,0,40),

('maker-checker','CriticalApprovalRequired','Critical Approval','Force maker-checker for critical settings','Boolean','true',null,null,null,'Critical','HotReload',1,0,1,1,10),

('audit','RetentionDays','Audit Retention','Audit retention days','Integer','2555',null,365,3650,'Critical','HotReload',1,0,0,0,10),

('monitoring','LatencyCriticalMs','Critical Latency','Critical transaction latency threshold','Integer','1000',null,10,60000,'Sensitive','HotReload',1,0,0,0,10),

('alerts','CertificateExpiryDays','Certificate Warning','Certificate-expiry warning threshold','Integer','30',null,1,365,'Sensitive','HotReload',1,0,0,0,10),

('observability','LogLevel','Log Level','Minimum structured log level','Enum','Information','["Debug","Information","Warning","Error","Critical"]',null,null,'Operational','HotReload',0,0,0,0,10),

('dr','RpoMinutes','RPO','Recovery point objective in minutes','Integer','5',null,0,1440,'Critical','HotReload',1,0,0,0,10),

('dr','RtoMinutes','RTO','Recovery time objective in minutes','Integer','30',null,1,1440,'Critical','HotReload',1,0,0,0,20),

('retention','TransactionDays','Transaction Retention','Online transaction retention days','Integer','365',null,30,3650,'Critical','HotReload',1,0,0,0,10),

('diagnostics','ConnectionTestEnabled','Connection Test','Allow privileged live dependency connectivity tests','Boolean','true',null,null,null,'Sensitive','HotReload',1,0,0,0,10);*/

('security','AdminClientSecretRef','Admin Client Secret Reference','Vault/HSM reference only; never a secret value','SecretReference',NULL,NULL,NULL,NULL,'Critical','ServiceRestart',true,true,true,false,40),

('maker-checker','CriticalApprovalRequired','Critical Approval','Force maker-checker for critical settings','Boolean','true',NULL,NULL,NULL,'Critical','HotReload',true,false,true,true,10),

('audit','RetentionDays','Audit Retention','Audit retention days','Integer','2555',NULL,365,3650,'Critical','HotReload',true,false,false,false,10),

('monitoring','LatencyCriticalMs','Critical Latency','Critical transaction latency threshold','Integer','1000',NULL,10,60000,'Sensitive','HotReload',true,false,false,false,10),

('alerts','CertificateExpiryDays','Certificate Warning','Certificate-expiry warning threshold','Integer','30',NULL,1,365,'Sensitive','HotReload',true,false,false,false,10),

('observability','LogLevel','Log Level','Minimum structured log level','Enum','Information','["Debug","Information","Warning","Error","Critical"]',NULL,NULL,'Operational','HotReload',false,false,false,false,10),

('dr','RpoMinutes','RPO','Recovery point objective in minutes','Integer','5',NULL,0,1440,'Critical','HotReload',true,false,false,false,10),

('dr','RtoMinutes','RTO','Recovery time objective in minutes','Integer','30',NULL,1,1440,'Critical','HotReload',true,false,false,false,20),

('retention','TransactionDays','Transaction Retention','Online transaction retention days','Integer','365',NULL,30,3650,'Critical','HotReload',true,false,false,false,10),

('diagnostics','ConnectionTestEnabled','Connection Test','Allow privileged live dependency connectivity tests','Boolean','true',NULL,NULL,NULL,'Sensitive','HotReload',true,false,false,false,10);

insert into dbo.configurationdefinitions (id,domaincode,key,displayname,description,valuetype,defaultvalue,allowedvaluesjson,minimumvalue,maximumvalue,sensitivity,reloadpolicy,requiresapproval,issecret,issensitive,productionlocked,displayorder,enabled)
select gen_random_uuid(),s.domaincode,s.key,s.displayname,s.description,s.valuetype,s.defaultvalue,s.allowed,s.minval,s.maxval,s.sensitivity,s.reloadpolicy,s.requiresapproval,s.issecret,s.issensitive,s.productionlocked,s.displayorder,1 from defs s
on conflict (domaincode, key) do update set displayname=excluded.displayname, description=excluded.description, valuetype=excluded.valuetype, defaultvalue=excluded.defaultvalue, allowedvaluesjson=excluded.allowed, minimumvalue=excluded.minval, maximumvalue=excluded.maxval, sensitivity=excluded.sensitivity, reloadpolicy=excluded.reloadpolicy, requiresapproval=excluded.requiresapproval, issecret=excluded.issecret, issensitive=excluded.issensitive, productionlocked=excluded.productionlocked, displayorder=excluded.displayorder, enabled=true;

commit;

-- ===== 039_enterprise_settings_completeness_runtime_validation.sql =====


begin;

-- v44.3A Enterprise Settings completeness: field-level definitions across all control-plane domains.

create temp table defs(

 domaincode varchar(64), key varchar(160), displayname varchar(160), description varchar(1024),

 valuetype varchar(32), defaultvalue text, allowedvaluesjson text, minimumvalue decimal(28,8), maximumvalue decimal(28,8),

 sensitivity varchar(32), reloadpolicy varchar(32), requiresapproval boolean, issecret boolean, issensitive boolean, productionlocked boolean, validationpattern varchar(512), displayorder int);

insert into defs values

('general', 'InstitutionName', 'Institution Name', 'Display/legal institution name', 'String', 'BankSwitch Institution', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('general', 'CountryCode', 'Country Code', 'ISO 3166-1 alpha-2 country code', 'String', 'IN', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, '^[A-Z]{2}$', 60),

('general', 'BusinessDate', 'Business Date', 'Authoritative banking business date in YYYY-MM-DD', 'String', '2026-01-01', null, null, null, 'Critical', 'HotReload', true, false, false, false, '^\d{4}-\d{2}-\d{2}$', 70),

('general', 'DefaultLanguage', 'Default Language', 'Default operator locale', 'String', 'en-IN', null, null, null, 'Operational', 'HotReload', false, false, false, false, null, 80),

('api', 'ConnectionTimeoutSeconds', 'Connection Timeout', 'Outbound API connection timeout', 'Integer', '10', null, 1, 120, 'Operational', 'HotReload', false, false, false, false, null, 30),

('api', 'RetryBackoffMs', 'Retry Backoff', 'Base retry backoff milliseconds', 'Integer', '250', null, 10, 30000, 'Operational', 'HotReload', false, false, false, false, null, 40),

('api', 'MaxConcurrentRequests', 'Max Concurrent Requests', 'Administrative API concurrency limit', 'Integer', '500', null, 10, 10000, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('api', 'MaxRequestBodyBytes', 'Max Request Body', 'Maximum API request body size bytes', 'Integer', '1048576', null, 1024, 104857600, 'Sensitive', 'HotReload', true, false, false, false, null, 60),

('realtime', 'ReconnectSeconds', 'Reconnect Interval', 'SignalR reconnect interval', 'Integer', '5', null, 1, 300, 'Operational', 'HotReload', false, false, false, false, null, 30),

('realtime', 'MaxReconnectAttempts', 'Max Reconnect Attempts', 'Maximum realtime reconnect attempts', 'Integer', '10', null, 1, 100, 'Operational', 'HotReload', false, false, false, false, null, 40),

('realtime', 'EventBufferSize', 'Event Buffer Size', 'Client realtime event buffer limit', 'Integer', '1000', null, 100, 100000, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('database', 'ConnectionStringRef', 'Connection String Reference', 'Vault reference to SQL connection string', 'SecretReference', 'vault://bankswitch/sql/connection', null, null, null, 'Critical', 'ServiceRestart', true, true, true, false, null, 40),

('database', 'MinPoolSize', 'SQL Min Pool Size', 'Minimum ADO.NET pool size', 'Integer', '10', null, 0, 500, 'Sensitive', 'ServiceRestart', true, false, false, false, null, 50),

('database', 'DeadlockRetryCount', 'Deadlock Retry Count', 'Database deadlock retry count', 'Integer', '3', null, 0, 20, 'Sensitive', 'HotReload', true, false, false, false, null, 60),

('database', 'IsolationLevel', 'Write Isolation Level', 'Default financial write isolation level', 'Enum', 'ReadCommitted', '["ReadCommitted","RepeatableRead","Serializable","Snapshot"]', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 70),

('transactions', 'FinancialTimeoutSeconds', 'Financial Timeout', '0200 transaction timeout', 'Integer', '30', null, 1, 180, 'Critical', 'HotReload', true, false, false, false, null, 50),

('transactions', 'ReversalTimeoutSeconds', 'Reversal Timeout', 'Reversal response timeout', 'Integer', '30', null, 1, 180, 'Critical', 'HotReload', true, false, false, false, null, 60),

('transactions', 'SafRetrySeconds', 'SAF Retry Interval', 'Store-and-forward retry interval', 'Integer', '60', null, 5, 3600, 'Critical', 'HotReload', true, false, false, false, null, 70),

('transactions', 'MaxSafRetries', 'Maximum SAF Retries', 'Maximum SAF delivery attempts', 'Integer', '10', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, null, 80),

('transactions', 'IdempotencyTtlSeconds', 'Idempotency TTL', 'Distributed idempotency retention', 'Integer', '86400', null, 60, 604800, 'Critical', 'HotReload', true, false, false, false, null, 90),

('transactions', 'StipEnabled', 'STIP Enabled', 'Enable stand-in transaction processing', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 100),

('iso8583', 'IsoVersion', 'ISO Version', 'Default ISO 8583 dialect', 'Enum', '1987', '["1987","1993","2003"]', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 30),

('iso8583', 'CharacterEncoding', 'Character Encoding', 'Network message character encoding', 'Enum', 'ASCII', '["ASCII","EBCDIC","UTF-8"]', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 40),

('iso8583', 'BitmapEncoding', 'Bitmap Encoding', 'Bitmap encoding format', 'Enum', 'ASCIIHEX', '["ASCIIHEX","BINARY"]', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 50),

('iso8583', 'TpduEnabled', 'TPDU Enabled', 'Enable TPDU header processing', 'Boolean', 'false', null, null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 60),

('iso8583', 'Field55MaxLength', 'DE55 Max Length', 'Maximum EMV field 55 length', 'Integer', '999', null, 1, 4096, 'Sensitive', 'ServiceRestart', true, false, false, false, null, 70),

('routing', 'Strategy', 'Routing Strategy', 'Route selection strategy', 'Enum', 'Priority', '["Priority","Weighted","LeastCost","HealthAware"]', null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('routing', 'HealthThresholdPercent', 'Health Threshold', 'Minimum destination health percent', 'Integer', '95', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, null, 30),

('routing', 'CacheSeconds', 'Route Cache TTL', 'Route rules cache interval', 'Integer', '30', null, 0, 3600, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('routing', 'SimulationEnabled', 'Route Simulation', 'Allow route simulation in admin', 'Boolean', 'true', null, null, null, 'Operational', 'HotReload', false, false, false, false, null, 50),

('network-hosts', 'VisaPrimaryEndpoint', 'Visa Primary Endpoint', 'Visa authorization endpoint', 'Uri', 'https://visa.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 30),

('network-hosts', 'MastercardPrimaryEndpoint', 'Mastercard Primary Endpoint', 'Mastercard authorization endpoint', 'Uri', 'https://mastercard.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 40),

('network-hosts', 'RupayPrimaryEndpoint', 'RuPay Primary Endpoint', 'RuPay/NPCI authorization endpoint', 'Uri', 'https://rupay.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 50),

('network-hosts', 'ReconnectSeconds', 'Reconnect Interval', 'Network connection reconnect interval', 'Integer', '10', null, 1, 600, 'Sensitive', 'HotReload', true, false, false, false, null, 60),

('network-hosts', 'SignOnIntervalSeconds', 'Sign-On Interval', 'Network sign-on refresh interval', 'Integer', '300', null, 10, 86400, 'Sensitive', 'HotReload', true, false, false, false, null, 70),

('network-hosts', 'CertificateRef', 'Network Certificate Reference', 'Certificate inventory reference for scheme TLS', 'CertificateReference', 'SCHEME-TLS-CERT', null, null, null, 'Critical', 'ConnectionRestart', true, false, true, false, null, 80),

('atm', 'DefaultProtocol', 'Default ATM Protocol', 'Default ATM terminal protocol', 'Enum', 'NDC+', '["NDC","NDC+","DDC","XFS","APTRA"]', null, null, 'Sensitive', 'ConnectionRestart', true, false, false, false, null, 20),

('atm', 'CommandTimeoutSeconds', 'ATM Command Timeout', 'Terminal command timeout', 'Integer', '30', null, 1, 300, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('atm', 'EjUploadMinutes', 'EJ Upload Interval', 'Electronic journal upload interval', 'Integer', '15', null, 1, 1440, 'Operational', 'HotReload', false, false, false, false, null, 40),

('atm', 'ScreenPackageVersion', 'Screen Package Version', 'Active ATM screen package version', 'String', '1.0.0', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('pos', 'HeartbeatSeconds', 'POS Heartbeat', 'POS terminal heartbeat interval', 'Integer', '60', null, 5, 3600, 'Operational', 'HotReload', false, false, false, false, null, 20),

('pos', 'CommandTimeoutSeconds', 'POS Command Timeout', 'POS remote command timeout', 'Integer', '30', null, 1, 300, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('pos', 'ContactlessOfflineEnabled', 'Offline Contactless', 'Enable controlled offline contactless', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('pos', 'TipAdjustmentEnabled', 'Tip Adjustment', 'Enable post-auth tip adjustment', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('pos', 'CashAtPosEnabled', 'Cash\@POS', 'Enable Cash\@POS acquiring', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 60),

('merchant', 'DefaultMdrPercent', 'Default MDR %', 'Default merchant discount rate percent', 'Decimal', '2.0', null, 0, 100, 'Critical', 'HotReload', true, false, false, false, null, 20),

('merchant', 'ReservePercent', 'Reserve %', 'Default merchant reserve percent', 'Decimal', '0', null, 0, 100, 'Critical', 'HotReload', true, false, false, false, null, 30),

('merchant', 'RefundLimit', 'Refund Limit', 'Default merchant refund amount limit', 'Decimal', '100000', null, 0, 100000000, 'Critical', 'HotReload', true, false, false, false, null, 40),

('cards', 'DefaultExpiryMonths', 'Default Card Expiry', 'Default card expiry months', 'Integer', '60', null, 1, 120, 'Sensitive', 'HotReload', true, false, false, false, null, 20),

('cards', 'ContactlessLimit', 'Contactless Limit', 'Default contactless limit', 'Decimal', '5000', null, 0, 100000, 'Critical', 'HotReload', true, false, false, false, null, 30),

('cards', 'InternationalEnabled', 'International Usage', 'Default international card usage', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('cards', 'VirtualCardEnabled', 'Virtual Cards', 'Enable virtual card issuance', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('hsm', 'PrimaryEndpoint', 'Primary HSM Endpoint', 'Primary HSM service/device endpoint', 'String', 'hsm-primary:1500', null, null, null, 'Critical', 'ServiceRestart', true, false, true, false, null, 30),

('hsm', 'SecondaryEndpoint', 'Secondary HSM Endpoint', 'Secondary HSM service/device endpoint', 'String', 'hsm-secondary:1500', null, null, null, 'Critical', 'ServiceRestart', true, false, true, false, null, 40),

('hsm', 'CommandTimeoutSeconds', 'HSM Command Timeout', 'HSM command timeout', 'Integer', '5', null, 1, 60, 'Critical', 'HotReload', true, false, false, false, null, 50),

('hsm', 'Tr31Enabled', 'TR-31 Enabled', 'Require TR-31 key blocks', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 60),

('hsm', 'Tr34Enabled', 'TR-34 Enabled', 'Allow TR-34 remote key loading', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 70),

('fraud', 'HighScoreThreshold', 'High Risk Score', 'High risk score threshold', 'Integer', '75', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, null, 20),

('fraud', 'VelocityWindowMinutes', 'Velocity Window', 'Default velocity window minutes', 'Integer', '10', null, 1, 1440, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('fraud', 'AutoBlockCritical', 'Auto Block Critical', 'Automatically block critical-risk events', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('aml', 'SanctionsEnabled', 'Sanctions Screening', 'Enable sanctions screening', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('aml', 'PepEnabled', 'PEP Screening', 'Enable PEP screening', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 30),

('aml', 'AutoCaseThreshold', 'AML Auto Case Score', 'Score triggering automatic AML case', 'Integer', '80', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, null, 40),

('aml', 'ProviderEndpoint', 'AML Provider Endpoint', 'External screening provider endpoint', 'Uri', 'https://aml.invalid', null, null, null, 'Sensitive', 'ConnectionRestart', true, false, false, false, null, 50),

('settlement', 'AutoSettlementEnabled', 'Auto Settlement', 'Enable automatic settlement posting', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('settlement', 'DefaultCurrency', 'Settlement Currency', 'Default settlement currency', 'String', 'INR', null, null, null, 'Critical', 'HotReload', true, false, false, false, '^[A-Z]{3}$', 30),

('settlement', 'HolidayCalendar', 'Holiday Calendar', 'Settlement holiday calendar identifier', 'String', 'IN-BANKING', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('gl', 'AutoPostingEnabled', 'Automatic GL Posting', 'Automatically post balanced journals', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('gl', 'SuspenseAccount', 'Suspense Account', 'Default GL suspense account code', 'String', 'SUSPENSE', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 30),

('gl', 'PeriodCloseApprovalRequired', 'Period Close Approval', 'Require checker for accounting period close', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, true, null, 40),

('reconciliation', 'AutoMatchEnabled', 'Automatic Matching', 'Enable automatic reconciliation matching', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('reconciliation', 'DateToleranceDays', 'Date Tolerance Days', 'Date tolerance for automatic matching', 'Integer', '1', null, 0, 30, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('reconciliation', 'ExceptionAgeHours', 'Exception Ageing', 'Hours before unmatched item escalation', 'Integer', '24', null, 1, 720, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('disputes', 'ChargebackSlaDays', 'Chargeback SLA', 'Chargeback SLA days', 'Integer', '7', null, 1, 180, 'Critical', 'HotReload', true, false, false, false, null, 20),

('disputes', 'RepresentmentSlaDays', 'Representment SLA', 'Representment SLA days', 'Integer', '7', null, 1, 180, 'Critical', 'HotReload', true, false, false, false, null, 30),

('disputes', 'EvidenceRetentionDays', 'Evidence Retention', 'Dispute evidence retention days', 'Integer', '2555', null, 365, 3650, 'Critical', 'HotReload', true, false, false, false, null, 40),

('cbs', 'SecondaryEndpoint', 'CBS Secondary Endpoint', 'Secondary CBS integration endpoint', 'Uri', 'https://cbs-dr.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 20),

('cbs', 'TimeoutSeconds', 'CBS Timeout', 'Core banking request timeout', 'Integer', '10', null, 1, 120, 'Critical', 'HotReload', true, false, false, false, null, 30),

('cbs', 'RetryCount', 'CBS Retry Count', 'Core banking transient retry count', 'Integer', '2', null, 0, 10, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('cbs', 'CircuitBreakerFailures', 'CBS Circuit Breaker', 'Failure threshold before circuit opens', 'Integer', '5', null, 1, 100, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('enterprise', 'EsbEndpoint', 'ESB Endpoint', 'Enterprise service bus endpoint', 'Uri', 'https://esb.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 20),

('enterprise', 'AcsEndpoint', 'ACS / 3DS Endpoint', 'ACS/3DS integration endpoint', 'Uri', 'https://acs.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 30),

('enterprise', 'FrmEndpoint', 'FRM Endpoint', 'Fraud risk manager endpoint', 'Uri', 'https://frm.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 40),

('enterprise', 'DwhExportEnabled', 'DWH Export', 'Enable warehouse feed generation', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('certification', 'EvidenceRetentionDays', 'Evidence Retention', 'Certification evidence retention days', 'Integer', '2555', null, 365, 3650, 'Critical', 'HotReload', true, false, false, false, null, 20),

('certification', 'FuzzTestingEnabled', 'Fuzz Testing', 'Allow controlled certification fuzz testing', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('certification', 'EnduranceMinutes', 'Endurance Test Duration', 'Default certification endurance duration minutes', 'Integer', '60', null, 1, 10080, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('security', 'SessionTimeoutMinutes', 'Session Timeout', 'Privileged admin session timeout', 'Integer', '15', null, 5, 480, 'Critical', 'HotReload', true, false, false, false, null, 50),

('security', 'LoginAttemptLimit', 'Login Attempt Limit', 'Failed login attempts before lockout', 'Integer', '5', null, 3, 20, 'Critical', 'HotReload', true, false, false, false, null, 60),

('security', 'LockoutMinutes', 'Account Lockout', 'Account lockout duration minutes', 'Integer', '30', null, 1, 1440, 'Critical', 'HotReload', true, false, false, false, null, 70),

('security', 'IpAllowListEnabled', 'Admin IP Allowlist', 'Enable administrative IP allowlisting', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 80),

('rbac', 'PrivilegedRoleReviewDays', 'Privileged Role Review', 'Days between privileged access reviews', 'Integer', '90', null, 1, 365, 'Critical', 'HotReload', true, false, false, false, null, 10),

('rbac', 'DormantUserDays', 'Dormant User Threshold', 'Disable/review users inactive for this many days', 'Integer', '45', null, 1, 365, 'Critical', 'HotReload', true, false, false, false, null, 20),

('rbac', 'ExportPermissionRequired', 'Export Permission', 'Require explicit role permission for data export', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 30),

('maker-checker', 'ApprovalExpiryHours', 'Approval Expiry', 'Hours before pending approval expires', 'Integer', '24', null, 1, 168, 'Critical', 'HotReload', true, false, false, false, null, 20),

('maker-checker', 'EmergencyOverrideEnabled', 'Emergency Override', 'Permit emergency controlled override', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, true, null, 30),

('maker-checker', 'TicketRequired', 'Change Ticket Required', 'Require ticket reference for configuration changes', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('audit', 'SiemForwardingEnabled', 'SIEM Forwarding', 'Forward security/admin audit events to SIEM', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('audit', 'HashChainEnabled', 'Audit Hash Chain', 'Enable tamper-evident audit chaining', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, true, null, 30),

('audit', 'ExportApprovalRequired', 'Audit Export Approval', 'Require checker before audit export', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('compliance', 'PciEvidenceRequired', 'PCI Evidence Required', 'Require PCI evidence completion for release', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, true, null, 10),

('compliance', 'RbiEvidenceRequired', 'RBI Evidence Required', 'Require RBI control evidence for production release', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, true, null, 20),

('compliance', 'EvidenceReviewDays', 'Evidence Review Cycle', 'Maximum compliance evidence review interval days', 'Integer', '90', null, 1, 365, 'Critical', 'HotReload', true, false, false, false, null, 30),

('monitoring', 'TpsWarning', 'TPS Warning', 'TPS warning threshold', 'Integer', '5000', null, 1, 100000, 'Sensitive', 'HotReload', true, false, false, false, null, 20),

('monitoring', 'DeclineRateCriticalPercent', 'Decline Rate Critical', 'Critical decline rate percent', 'Decimal', '20', null, 0, 100, 'Critical', 'HotReload', true, false, false, false, null, 30),

('monitoring', 'QueueDepthCritical', 'Queue Depth Critical', 'Critical processing queue depth', 'Integer', '3000', null, 1, 100000, 'Critical', 'HotReload', true, false, false, false, null, 40),

('monitoring', 'AvailabilitySlaPercent', 'Availability SLA', 'Required platform availability percentage', 'Decimal', '99.95', null, 90, 100, 'Critical', 'HotReload', true, false, false, false, null, 50),

('alerts', 'EmailEnabled', 'Email Alerts', 'Enable email operational alerts', 'Boolean', 'true', null, null, null, 'Operational', 'HotReload', false, false, false, false, null, 20),

('alerts', 'SmsEnabled', 'SMS Alerts', 'Enable SMS critical alerts', 'Boolean', 'false', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('alerts', 'CriticalEscalationMinutes', 'Critical Escalation', 'Minutes before critical alert escalates', 'Integer', '5', null, 1, 120, 'Critical', 'HotReload', true, false, false, false, null, 40),

('observability', 'OpenTelemetryEnabled', 'OpenTelemetry', 'Enable OpenTelemetry tracing/metrics', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 20),

('observability', 'TraceSamplingPercent', 'Trace Sampling %', 'OpenTelemetry trace sample percentage', 'Integer', '10', null, 0, 100, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('observability', 'SensitiveDataMasking', 'Sensitive Data Masking', 'Enforce sensitive value masking in logs', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, true, null, 40),

('dr', 'Mode', 'DR Mode', 'Disaster recovery topology', 'Enum', 'ActivePassive', '["ActivePassive","ActiveActive"]', null, null, 'Critical', 'ClusterRestart', true, false, false, false, null, 30),

('dr', 'AutomaticFailoverEnabled', 'Automatic Failover', 'Enable automatic DR failover', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('dr', 'DrSiteCode', 'DR Site Code', 'Configured disaster recovery site identifier', 'String', 'DR1', null, null, null, 'Critical', 'ClusterRestart', true, false, false, false, null, 50),

('retention', 'AuditDays', 'Audit Retention', 'Audit event retention days', 'Integer', '2555', null, 365, 3650, 'Critical', 'HotReload', true, false, false, false, null, 20),

('retention', 'EjDays', 'EJ Retention', 'ATM electronic journal retention days', 'Integer', '365', null, 30, 3650, 'Critical', 'HotReload', true, false, false, false, null, 30),

('retention', 'DisputeDays', 'Dispute Retention', 'Dispute case/evidence retention days', 'Integer', '2555', null, 365, 3650, 'Critical', 'HotReload', true, false, false, false, null, 40),

('retention', 'CertificationDays', 'Certification Retention', 'Certification evidence retention days', 'Integer', '2555', null, 365, 3650, 'Critical', 'HotReload', true, false, false, false, null, 50),

('feature-flags', 'ProductionPercentageRolloutAllowed', 'Production Percentage Rollout', 'Permit percentage feature rollout in PROD', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 10),

('feature-flags', 'RollbackOnHealthFailure', 'Rollback On Health Failure', 'Automatically rollback feature flag when health degrades', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('diagnostics', 'ProbeTimeoutSeconds', 'Probe Timeout', 'Maximum diagnostic dependency probe duration', 'Integer', '5', null, 1, 60, 'Sensitive', 'HotReload', true, false, false, false, null, 20),

('diagnostics', 'HsmProbeEnabled', 'HSM Probe', 'Enable privileged HSM connectivity probe', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 30),

('diagnostics', 'NetworkProbeEnabled', 'Network Host Probe', 'Enable privileged scheme host connectivity probes', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40);

insert into dbo.configurationdefinitions (id,domaincode,key,displayname,description,valuetype,defaultvalue,allowedvaluesjson,minimumvalue,maximumvalue,sensitivity,reloadpolicy,requiresapproval,issecret,issensitive,productionlocked,validationpattern,displayorder,enabled) select gen_random_uuid(),s.domaincode,s.key,s.displayname,s.description,s.valuetype,s.defaultvalue,s.allowedvaluesjson,s.minimumvalue,s.maximumvalue,s.sensitivity,s.reloadpolicy,s.requiresapproval,s.issecret,s.issensitive,s.productionlocked,s.validationpattern,s.displayorder,1 from defs s on conflict (domaincode, key) do update set displayname=excluded.displayname, description=excluded.description, valuetype=excluded.valuetype, defaultvalue=excluded.defaultvalue, allowedvaluesjson=excluded.allowedvaluesjson, minimumvalue=excluded.minimumvalue, maximumvalue=excluded.maximumvalue, sensitivity=excluded.sensitivity, reloadpolicy=excluded.reloadpolicy, requiresapproval=excluded.requiresapproval, issecret=excluded.issecret, issensitive=excluded.issensitive, productionlocked=excluded.productionlocked, validationpattern=excluded.validationpattern, displayorder=excluded.displayorder, enabled=true;

commit;

-- ===== 040_production_ndc_ndcplus_atm_protocol_engine.sql =====

-- V44.5 Production NDC/NDC+ ATM Protocol Engine

-- SQL Server canonical migration. Stores restart-safe ATM protocol session state,

-- trace hashes, device status, download blocks and electronic journal events.

DO $$
BEGIN
    IF to_regclass('dbo.ndcterminalsessions') is null THEN

        create table if not exists dbo.ndcterminalsessions (

            terminalid varchar(64) not null constraint pk_ndcterminalsessions primary key,

            protocol varchar(16) not null,

            state varchar(32) not null,

            nextsequencenumber int not null constraint df_ndcterminalsessions_seq default 1,

            lastinboundat timestamptz null,

            lastoutboundat timestamptz null,

            lastechoat timestamptz null,

            lastdownloadat timestamptz null,

            lasterror varchar(1024) null,

            correlationid varchar(64) not null,

            updatedat timestamptz not null,

            rowversion bytea not null,

            constraint ck_ndcterminalsessions_protocol check (protocol in ('Ndc','NdcPlus')),

            constraint ck_ndcterminalsessions_sequence check (nextsequencenumber > 0)

        );

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ndcprotocoltraces') is null THEN

        create table if not exists dbo.ndcprotocoltraces (

            id uuid not null constraint pk_ndcprotocoltraces primary key,

            terminalid varchar(64) not null,

            direction varchar(16) not null,

            messageclass varchar(64) not null,

            sequencenumber int not null,

            payloadsha256 char(64) not null,

            lrcvalid boolean not null,

            macvalid boolean not null,

            parsedfieldsjson text not null,

            recordedat timestamptz not null,

            correlationid varchar(64) not null,

            constraint fk_ndcprotocoltraces_session foreign key (terminalid) references dbo.ndcterminalsessions(terminalid),

            constraint ck_ndcprotocoltraces_json check ((parsedfieldsjson::jsonb is not null)),

            constraint ck_ndcprotocoltraces_direction check (direction in ('Inbound','Outbound'))

        );

        create index if not exists ix_ndcprotocoltraces_terminal_time on dbo.ndcprotocoltraces(terminalid, recordedat desc);

        create index if not exists ix_ndcprotocoltraces_correlation on dbo.ndcprotocoltraces(correlationid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ndcdevicestatusevents') is null THEN

        create table if not exists dbo.ndcdevicestatusevents (

            id uuid not null constraint pk_ndcdevicestatusevents primary key,

            terminalid varchar(64) not null,

            device varchar(64) not null,

            state varchar(32) not null,

            statuscode varchar(32) not null,

            detail varchar(1024) not null,

            occurredat timestamptz not null,

            correlationid varchar(64) not null,

            constraint fk_ndcdevicestatusevents_session foreign key (terminalid) references dbo.ndcterminalsessions(terminalid)

        );

        create index if not exists ix_ndcdevicestatus_terminal_time on dbo.ndcdevicestatusevents(terminalid, occurredat desc);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ndcdownloadartifacts') is null THEN

        create table if not exists dbo.ndcdownloadartifacts (

            id uuid not null constraint pk_ndcdownloadartifacts primary key,

            terminalid varchar(64) not null,

            downloadtype varchar(64) not null,

            version varchar(64) not null,

            blocknumber int not null,

            totalblocks int not null,

            payloadsha256 char(64) not null,

            status varchar(32) not null,

            createdat timestamptz not null,

            appliedat timestamptz null,

            correlationid varchar(64) not null,

            constraint fk_ndcdownloadartifacts_session foreign key (terminalid) references dbo.ndcterminalsessions(terminalid),

            constraint ck_ndcdownloadartifacts_block check (blocknumber > 0 and totalblocks >= blocknumber)

        );

        create index if not exists ix_ndcdownload_terminal_version on dbo.ndcdownloadartifacts(terminalid, downloadtype, version, blocknumber);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.ndcelectronicjournalentries') is null THEN

        create table if not exists dbo.ndcelectronicjournalentries (

            id uuid not null constraint pk_ndcelectronicjournalentries primary key,

            terminalid varchar(64) not null,

            eventtype varchar(64) not null,

            rrn varchar(32) not null,

            stan varchar(16) not null,

            maskedpan varchar(32) not null,

            amount decimal(19,4) null,

            currencycode varchar(3) not null,

            text varchar(2000) not null,

            occurredat timestamptz not null,

            correlationid varchar(64) not null,

            constraint fk_ndcej_session foreign key (terminalid) references dbo.ndcterminalsessions(terminalid)

        );

        create index if not exists ix_ndcej_terminal_time on dbo.ndcelectronicjournalentries(terminalid, occurredat desc);

        create index if not exists ix_ndcej_rrn_stan on dbo.ndcelectronicjournalentries(rrn, stan);

    END IF;
END $$;

-- ===== 041_canonical_sql_server_schema_referential_integrity_hardening.sql =====

-- V44.6 Canonical SQL Server Schema & Referential Integrity Hardening

-- Establishes the authoritative SQL Server schema contract, adds missing high-confidence

-- referential constraints and JSON integrity checks, and records production repository mappings.


begin;

DO $$
BEGIN
    IF to_regclass('dbo.repositorytablemappings') is null THEN

            create table if not exists dbo.repositorytablemappings(

                interfacename varchar(160) not null constraint pk_repositorytablemappings primary key,

                implementationname varchar(200) not null,

                primarytable varchar(128) not null,

                environmentscope varchar(32) not null,

                isauthoritative boolean not null constraint df_repositorytablemappings_authoritative default(true),

                introducedversion varchar(32) not null,

                lastvalidatedat timestamptz(7) not null constraint df_repositorytablemappings_validated default(clock_timestamp())

            );

    END IF;
END $$;

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('IPosAcquiringProductionRepository', 'SqlPosAcquiringProductionRepository', 'dbo.PosMerchants', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('IPosTerminalDrivingRepository', 'SqlPosTerminalDrivingRepository', 'dbo.PosTerminalProfiles', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('IKycRepository', 'SqlKycRepository', 'dbo.KycDocuments', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('IAcquiringCertificationRepository', 'SqlAcquiringCertificationRepository', 'dbo.AcquiringCertificationStore', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('IAcquiringCertificationLabRepository', 'SqlAcquiringCertificationLabRepository', 'dbo.AcquiringCertificationLabStore', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('IIssuerCertificationRepository', 'SqlIssuerCertificationRepository', 'dbo.IssuerCertificationStore', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();

insert into dbo.repositorytablemappings (interfacename,implementationname,primarytable,environmentscope,isauthoritative,introducedversion)
values ('INdcProtocolRepository', 'SqlNdcProtocolRepository', 'dbo.NdcTerminalSessions', 'Production', true, 'v44.6')
on conflict (interfacename) do update set implementationname=excluded.implementationname, primarytable=excluded.primarytable, environmentscope=excluded.environmentscope, isauthoritative=true, introducedversion=excluded.introducedversion, lastvalidatedat=clock_timestamp();


-- Consolidate obsolete v32 snake_case POS tables into the canonical production tables.

-- These guards make upgrades safe while fresh v44.6 installations never create the legacy objects.

DO $$
BEGIN
    IF to_regclass('dbo.pos_terminal_profile') is not null THEN

            insert into dbo.posterminalprofiles(terminalid,merchantid,vendor,protocol,serialnumber,devicemodel,branchcode,locationcode,countrycode,currencycode,ismpos,contactlessenabled,status,capabilitiesjson,createdat,updatedat)

            select terminal_id,merchant_id,vendor,protocol,serial_number,device_model,branch_code,location_code,country_code,currency_code,is_mpos,contactless_enabled,status,coalesce(capabilities_json,'{}'),created_at_utc,updated_at_utc

            from dbo.pos_terminal_profile s where not exists(select 1 from dbo.posterminalprofiles t where t.terminalid=s.terminal_id);

            drop table if exists dbo.pos_terminal_profile;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.mpos_enrollment') is not null THEN

            insert into dbo.posmposenrollments(id,terminalid,merchantid,devicebindingid,mobilenumbermasked,appversion,osname,osversion,status,enrolledat,updatedat)

            select id,terminal_id,merchant_id,device_binding_id,mobile_number_masked,app_version,os_name,os_version,status,enrolled_at_utc,updated_at_utc

            from dbo.mpos_enrollment s where not exists(select 1 from dbo.posmposenrollments t where t.id=s.id);

            drop table if exists dbo.mpos_enrollment;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_key_download_certification') is not null THEN

            insert into dbo.poskeydownloadcertifications(id,terminalid,vendor,protocol,scheme,keyscheme,certificationpackreference,evidencehash,status,certifiedat,remarks)

            select id,terminal_id,vendor,protocol,scheme,key_scheme,certification_pack_ref,evidence_hash,status,certified_at_utc,coalesce(remarks,'')

            from dbo.pos_key_download_certification s where not exists(select 1 from dbo.poskeydownloadcertifications t where t.id=s.id);

            drop table if exists dbo.pos_key_download_certification;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_key_download_session') is not null THEN

            insert into dbo.poskeydownloadsessions(id,terminalid,scheme,tmkkcv,tpkkcv,takkcv,status,requestedat,completedat,correlationid)

            select id,terminal_id,scheme,tmk_kcv,tpk_kcv,tak_kcv,status,requested_at_utc,completed_at_utc,correlation_id

            from dbo.pos_key_download_session s where not exists(select 1 from dbo.poskeydownloadsessions t where t.id=s.id);

            drop table if exists dbo.pos_key_download_session;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_contactless_transaction_flow') is not null THEN

            insert into dbo.poscontactlesstransactionflows(id,terminalid,merchantid,mode,panmasked,amount,currencycode,emvcryptogram,offlineapprovedbyterminal,onlinehostauthorised,responsecode,createdat,correlationid)

            select id,terminal_id,merchant_id,mode,pan_masked,amount,currency_code,emv_cryptogram,offline_approved,online_authorised,response_code,created_at_utc,correlation_id

            from dbo.pos_contactless_transaction_flow s where not exists(select 1 from dbo.poscontactlesstransactionflows t where t.id=s.id);

            drop table if exists dbo.pos_contactless_transaction_flow;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_tip_adjustment') is not null THEN

            insert into dbo.postipadjustments(id,originaltransactionid,terminalid,merchantid,originalamount,tipamount,finalamount,currencycode,approvalcode,status,createdat,correlationid)

            select id,original_transaction_id,terminal_id,merchant_id,original_amount,tip_amount,final_amount,currency_code,approval_code,status,created_at_utc,correlation_id

            from dbo.pos_tip_adjustment s where not exists(select 1 from dbo.postipadjustments t where t.id=s.id);

            drop table if exists dbo.pos_tip_adjustment;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_cash_at_pos_acquiring') is not null THEN

            insert into dbo.poscashatposacquiring(id,terminalid,merchantid,panmasked,purchaseamount,cashamount,totalamount,currencycode,approvalcode,responsecode,createdat,correlationid)

            select id,terminal_id,merchant_id,pan_masked,purchase_amount,cash_amount,total_amount,currency_code,approval_code,response_code,created_at_utc,correlation_id

            from dbo.pos_cash_at_pos_acquiring s where not exists(select 1 from dbo.poscashatposacquiring t where t.id=s.id);

            drop table if exists dbo.pos_cash_at_pos_acquiring;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.merchant_settlement_batch') is not null THEN

            insert into dbo.posmerchantsettlementbatches(id,merchantid,settlementdate,currencycode,transactioncount,grossamount,interchangefee,mdrfee,gstamount,netpayable,status,createdat,filehash,correlationid)

            select id,merchant_id,settlement_date,currency_code,transaction_count,gross_amount,interchange_fee,mdr_fee,gst_amount,net_payable,status,created_at_utc,file_hash,correlation_id

            from dbo.merchant_settlement_batch s where not exists(select 1 from dbo.posmerchantsettlementbatches t where t.id=s.id);

            drop table if exists dbo.merchant_settlement_batch;

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_device_command') is not null THEN

            insert into dbo.posdevicecommands(id,terminalid,command,parametersjson,status,createdat,appliedat,correlationid)

            select id,terminal_id,command,coalesce(parameters_json,'{}'),status,created_at_utc,applied_at_utc,correlation_id

            from dbo.pos_device_command s where not exists(select 1 from dbo.posdevicecommands t where t.id=s.id);

            drop table if exists dbo.pos_device_command;

    END IF;
END $$;


-- High-confidence foreign keys. intentionally fails the migration if existing orphans are found.

DO $$
BEGIN
    IF to_regclass('dbo.kycdocuments') is not null and to_regclass('dbo.customers') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_kycdocuments_customers') THEN

            alter table dbo.kycdocuments add constraint fk_kycdocuments_customers foreign key(customerid) references dbo.customers(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.kycdocuments') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='kycdocuments' AND indexname='ix_ri_kycdocuments_customers') THEN
            create index if not exists ix_ri_kycdocuments_customers on dbo.kycdocuments(customerid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.authorizationholds') is not null and to_regclass('dbo.walletaccounts') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_authorizationholds_walletaccounts') THEN

            alter table dbo.authorizationholds add constraint fk_authorizationholds_walletaccounts foreign key(walletaccountid) references dbo.walletaccounts(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.authorizationholds') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='authorizationholds' AND indexname='ix_ri_authorizationholds_walletaccounts') THEN
            create index if not exists ix_ri_authorizationholds_walletaccounts on dbo.authorizationholds(walletaccountid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.authorizationholds') is not null and to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_authorizationholds_prepaidcards') THEN

            alter table dbo.authorizationholds add constraint fk_authorizationholds_prepaidcards foreign key(cardid) references dbo.prepaidcards(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.authorizationholds') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='authorizationholds' AND indexname='ix_ri_authorizationholds_prepaidcards') THEN
            create index if not exists ix_ri_authorizationholds_prepaidcards on dbo.authorizationholds(cardid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.prepaidcards') is not null and to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_prepaidcards_replacedbycard') THEN

            alter table dbo.prepaidcards add constraint fk_prepaidcards_replacedbycard foreign key(replacedbycardid) references dbo.prepaidcards(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='prepaidcards' AND indexname='ix_ri_prepaidcards_replacedbycard') THEN
            create index if not exists ix_ri_prepaidcards_replacedbycard on dbo.prepaidcards(replacedbycardid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.directdebitmandates') is not null and to_regclass('dbo.customers') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_directdebitmandates_customers') THEN

            alter table dbo.directdebitmandates add constraint fk_directdebitmandates_customers foreign key(customerid) references dbo.customers(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.directdebitmandates') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='directdebitmandates' AND indexname='ix_ri_directdebitmandates_customers') THEN
            create index if not exists ix_ri_directdebitmandates_customers on dbo.directdebitmandates(customerid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.customerdisputes') is not null and to_regclass('dbo.customers') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_customerdisputes_customers') THEN

            alter table dbo.customerdisputes add constraint fk_customerdisputes_customers foreign key(customerid) references dbo.customers(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.customerdisputes') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='customerdisputes' AND indexname='ix_ri_customerdisputes_customers') THEN
            create index if not exists ix_ri_customerdisputes_customers on dbo.customerdisputes(customerid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.customerdisputes') is not null and to_regclass('dbo.chargebackcases') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_customerdisputes_chargebackcases') THEN

            alter table dbo.customerdisputes add constraint fk_customerdisputes_chargebackcases foreign key(linkedchargebackid) references dbo.chargebackcases(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.customerdisputes') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='customerdisputes' AND indexname='ix_ri_customerdisputes_chargebackcases') THEN
            create index if not exists ix_ri_customerdisputes_chargebackcases on dbo.customerdisputes(linkedchargebackid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.disputeevidence') is not null and to_regclass('dbo.customerdisputes') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_disputeevidence_customerdisputes') THEN

            alter table dbo.disputeevidence add constraint fk_disputeevidence_customerdisputes foreign key(disputeid) references dbo.customerdisputes(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.disputeevidence') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='disputeevidence' AND indexname='ix_ri_disputeevidence_customerdisputes') THEN
            create index if not exists ix_ri_disputeevidence_customerdisputes on dbo.disputeevidence(disputeid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournallines') is not null and to_regclass('dbo.glaccounts') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_gljournallines_glaccounts') THEN

            alter table dbo.gljournallines add constraint fk_gljournallines_glaccounts foreign key(accountcode) references dbo.glaccounts(accountcode);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournallines') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='gljournallines' AND indexname='ix_ri_gljournallines_glaccounts') THEN
            create index if not exists ix_ri_gljournallines_glaccounts on dbo.gljournallines(accountcode);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.glaccountbalances') is not null and to_regclass('dbo.glaccounts') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_glaccountbalances_glaccounts') THEN

            alter table dbo.glaccountbalances add constraint fk_glaccountbalances_glaccounts foreign key(accountcode) references dbo.glaccounts(accountcode);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.glaccountbalances') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='glaccountbalances' AND indexname='ix_ri_glaccountbalances_glaccounts') THEN
            create index if not exists ix_ri_glaccountbalances_glaccounts on dbo.glaccountbalances(accountcode);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournalentries') is not null and to_regclass('dbo.gljournalentries') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_gljournalentries_reversesjournal') THEN

            alter table dbo.gljournalentries add constraint fk_gljournalentries_reversesjournal foreign key(reversesjournalid) references dbo.gljournalentries(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournalentries') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='gljournalentries' AND indexname='ix_ri_gljournalentries_reversesjournal') THEN
            create index if not exists ix_ri_gljournalentries_reversesjournal on dbo.gljournalentries(reversesjournalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and to_regclass('dbo.customers') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardproductionorders_customers') THEN

            alter table dbo.debitcardproductionorders add constraint fk_debitcardproductionorders_customers foreign key(customerid) references dbo.customers(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardproductionorders' AND indexname='ix_ri_debitcardproductionorders_customers') THEN
            create index if not exists ix_ri_debitcardproductionorders_customers on dbo.debitcardproductionorders(customerid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and to_regclass('dbo.cardproducts') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardproductionorders_cardproducts') THEN

            alter table dbo.debitcardproductionorders add constraint fk_debitcardproductionorders_cardproducts foreign key(productid) references dbo.cardproducts(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardproductionorders' AND indexname='ix_ri_debitcardproductionorders_cardproducts') THEN
            create index if not exists ix_ri_debitcardproductionorders_cardproducts on dbo.debitcardproductionorders(productid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardproductionorders_prepaidcards') THEN

            alter table dbo.debitcardproductionorders add constraint fk_debitcardproductionorders_prepaidcards foreign key(cardid) references dbo.prepaidcards(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardproductionorders' AND indexname='ix_ri_debitcardproductionorders_prepaidcards') THEN
            create index if not exists ix_ri_debitcardproductionorders_prepaidcards on dbo.debitcardproductionorders(cardid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardproductionorders_oldcard') THEN

            alter table dbo.debitcardproductionorders add constraint fk_debitcardproductionorders_oldcard foreign key(oldcardid) references dbo.prepaidcards(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardproductionorders' AND indexname='ix_ri_debitcardproductionorders_oldcard') THEN
            create index if not exists ix_ri_debitcardproductionorders_oldcard on dbo.debitcardproductionorders(oldcardid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and to_regclass('dbo.debitcardbranchstockitems') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardproductionorders_branchstock') THEN

            alter table dbo.debitcardproductionorders add constraint fk_debitcardproductionorders_branchstock foreign key(branchstockitemid) references dbo.debitcardbranchstockitems(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardproductionorders') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardproductionorders' AND indexname='ix_ri_debitcardproductionorders_branchstock') THEN
            create index if not exists ix_ri_debitcardproductionorders_branchstock on dbo.debitcardproductionorders(branchstockitemid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardbranchstockitems') is not null and to_regclass('dbo.customers') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardbranchstock_customers') THEN

            alter table dbo.debitcardbranchstockitems add constraint fk_debitcardbranchstock_customers foreign key(assignedcustomerid) references dbo.customers(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardbranchstockitems') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardbranchstockitems' AND indexname='ix_ri_debitcardbranchstock_customers') THEN
            create index if not exists ix_ri_debitcardbranchstock_customers on dbo.debitcardbranchstockitems(assignedcustomerid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardbranchstockitems') is not null and to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_debitcardbranchstock_prepaidcards') THEN

            alter table dbo.debitcardbranchstockitems add constraint fk_debitcardbranchstock_prepaidcards foreign key(assignedcardid) references dbo.prepaidcards(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.debitcardbranchstockitems') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='debitcardbranchstockitems' AND indexname='ix_ri_debitcardbranchstock_prepaidcards') THEN
            create index if not exists ix_ri_debitcardbranchstock_prepaidcards on dbo.debitcardbranchstockitems(assignedcardid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hotlistpropagationevents') is not null and to_regclass('dbo.prepaidcards') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_hotlistpropagation_prepaidcards') THEN

            alter table dbo.hotlistpropagationevents add constraint fk_hotlistpropagation_prepaidcards foreign key(cardid) references dbo.prepaidcards(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.hotlistpropagationevents') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='hotlistpropagationevents' AND indexname='ix_ri_hotlistpropagation_prepaidcards') THEN
            create index if not exists ix_ri_hotlistpropagation_prepaidcards on dbo.hotlistpropagationevents(cardid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_dispute_exchange_records') is not null and to_regclass('dbo.chargebackcases') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_networkdisputerecords_chargeback') THEN

            alter table dbo.network_dispute_exchange_records add constraint fk_networkdisputerecords_chargeback foreign key(local_chargeback_case_id) references dbo.chargebackcases(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_dispute_exchange_records') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='network_dispute_exchange_records' AND indexname='ix_ri_networkdisputerecords_chargeback') THEN
            create index if not exists ix_ri_networkdisputerecords_chargeback on dbo.network_dispute_exchange_records(local_chargeback_case_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_dispute_exchange_records') is not null and to_regclass('dbo.customerdisputes') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_networkdisputerecords_dispute') THEN

            alter table dbo.network_dispute_exchange_records add constraint fk_networkdisputerecords_dispute foreign key(local_dispute_id) references dbo.customerdisputes(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_dispute_exchange_records') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='network_dispute_exchange_records' AND indexname='ix_ri_networkdisputerecords_dispute') THEN
            create index if not exists ix_ri_networkdisputerecords_dispute on dbo.network_dispute_exchange_records(local_dispute_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_lod_file_artifact') is not null and to_regclass('dbo.atm_screen_definition') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_atmlod_screendefinition') THEN

            alter table dbo.atm_lod_file_artifact add constraint fk_atmlod_screendefinition foreign key(screen_definition_id) references dbo.atm_screen_definition(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_lod_file_artifact') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='atm_lod_file_artifact' AND indexname='ix_ri_atmlod_screendefinition') THEN
            create index if not exists ix_ri_atmlod_screendefinition on dbo.atm_lod_file_artifact(screen_definition_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_distribution_job') is not null and to_regclass('dbo.atm_screen_definition') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_atmscreendistribution_screendefinition') THEN

            alter table dbo.atm_screen_distribution_job add constraint fk_atmscreendistribution_screendefinition foreign key(screen_definition_id) references dbo.atm_screen_definition(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_distribution_job') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='atm_screen_distribution_job' AND indexname='ix_ri_atmscreendistribution_screendefinition') THEN
            create index if not exists ix_ri_atmscreendistribution_screendefinition on dbo.atm_screen_distribution_job(screen_definition_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_admin_cash_operation') is not null and to_regclass('dbo.atm_terminal_profile') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_atmadmincash_terminal') THEN

            alter table dbo.atm_admin_cash_operation add constraint fk_atmadmincash_terminal foreign key(terminal_id) references dbo.atm_terminal_profile(terminal_id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_admin_cash_operation') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='atm_admin_cash_operation' AND indexname='ix_ri_atmadmincash_terminal') THEN
            create index if not exists ix_ri_atmadmincash_terminal on dbo.atm_admin_cash_operation(terminal_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_c3r_reconciliation_run') is not null and to_regclass('dbo.atm_terminal_profile') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_atmc3r_terminal') THEN

            alter table dbo.atm_c3r_reconciliation_run add constraint fk_atmc3r_terminal foreign key(terminal_id) references dbo.atm_terminal_profile(terminal_id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_c3r_reconciliation_run') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='atm_c3r_reconciliation_run' AND indexname='ix_ri_atmc3r_terminal') THEN
            create index if not exists ix_ri_atmc3r_terminal on dbo.atm_c3r_reconciliation_run(terminal_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_evidence_artifact') is not null and to_regclass('dbo.atm_terminal_profile') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_atmevidence_terminal') THEN

            alter table dbo.atm_evidence_artifact add constraint fk_atmevidence_terminal foreign key(terminal_id) references dbo.atm_terminal_profile(terminal_id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_evidence_artifact') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='atm_evidence_artifact' AND indexname='ix_ri_atmevidence_terminal') THEN
            create index if not exists ix_ri_atmevidence_terminal on dbo.atm_evidence_artifact(terminal_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_definition') is not null and to_regclass('dbo.atm_voice_prompt_pack') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_atmscreendefinition_voicepack') THEN

            alter table dbo.atm_screen_definition add constraint fk_atmscreendefinition_voicepack foreign key(voice_prompt_pack_id) references dbo.atm_voice_prompt_pack(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_definition') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='atm_screen_definition' AND indexname='ix_ri_atmscreendefinition_voicepack') THEN
            create index if not exists ix_ri_atmscreendefinition_voicepack on dbo.atm_screen_definition(voice_prompt_pack_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posterminalprofiles_merchants') THEN

            alter table dbo.posterminalprofiles add constraint fk_posterminalprofiles_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posterminalprofiles' AND indexname='ix_ri_posterminalprofiles_merchants') THEN
            create index if not exists ix_ri_posterminalprofiles_merchants on dbo.posterminalprofiles(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posmposenrollments_terminalprofiles') THEN

            alter table dbo.posmposenrollments add constraint fk_posmposenrollments_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posmposenrollments' AND indexname='ix_ri_posmposenrollments_terminalprofiles') THEN
            create index if not exists ix_ri_posmposenrollments_terminalprofiles on dbo.posmposenrollments(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posmposenrollments_merchants') THEN

            alter table dbo.posmposenrollments add constraint fk_posmposenrollments_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posmposenrollments' AND indexname='ix_ri_posmposenrollments_merchants') THEN
            create index if not exists ix_ri_posmposenrollments_merchants on dbo.posmposenrollments(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadcertifications') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poskeydownloadcertifications_terminalprofiles') THEN

            alter table dbo.poskeydownloadcertifications add constraint fk_poskeydownloadcertifications_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadcertifications') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poskeydownloadcertifications' AND indexname='ix_ri_poskeydownloadcertifications_terminalprofiles') THEN
            create index if not exists ix_ri_poskeydownloadcertifications_terminalprofiles on dbo.poskeydownloadcertifications(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadsessions') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poskeydownloadsessions_terminalprofiles') THEN

            alter table dbo.poskeydownloadsessions add constraint fk_poskeydownloadsessions_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadsessions') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poskeydownloadsessions' AND indexname='ix_ri_poskeydownloadsessions_terminalprofiles') THEN
            create index if not exists ix_ri_poskeydownloadsessions_terminalprofiles on dbo.poskeydownloadsessions(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poscontactlesstransactionflows_terminalprofiles') THEN

            alter table dbo.poscontactlesstransactionflows add constraint fk_poscontactlesstransactionflows_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poscontactlesstransactionflows' AND indexname='ix_ri_poscontactlesstransactionflows_terminalprofiles') THEN
            create index if not exists ix_ri_poscontactlesstransactionflows_terminalprofiles on dbo.poscontactlesstransactionflows(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poscontactlesstransactionflows_merchants') THEN

            alter table dbo.poscontactlesstransactionflows add constraint fk_poscontactlesstransactionflows_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poscontactlesstransactionflows' AND indexname='ix_ri_poscontactlesstransactionflows_merchants') THEN
            create index if not exists ix_ri_poscontactlesstransactionflows_merchants on dbo.poscontactlesstransactionflows(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_postipadjustments_terminalprofiles') THEN

            alter table dbo.postipadjustments add constraint fk_postipadjustments_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='postipadjustments' AND indexname='ix_ri_postipadjustments_terminalprofiles') THEN
            create index if not exists ix_ri_postipadjustments_terminalprofiles on dbo.postipadjustments(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_postipadjustments_merchants') THEN

            alter table dbo.postipadjustments add constraint fk_postipadjustments_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='postipadjustments' AND indexname='ix_ri_postipadjustments_merchants') THEN
            create index if not exists ix_ri_postipadjustments_merchants on dbo.postipadjustments(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poscashatposacquiring_terminalprofiles') THEN

            alter table dbo.poscashatposacquiring add constraint fk_poscashatposacquiring_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poscashatposacquiring' AND indexname='ix_ri_poscashatposacquiring_terminalprofiles') THEN
            create index if not exists ix_ri_poscashatposacquiring_terminalprofiles on dbo.poscashatposacquiring(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poscashatposacquiring_merchants') THEN

            alter table dbo.poscashatposacquiring add constraint fk_poscashatposacquiring_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poscashatposacquiring' AND indexname='ix_ri_poscashatposacquiring_merchants') THEN
            create index if not exists ix_ri_poscashatposacquiring_merchants on dbo.poscashatposacquiring(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementbatches') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posmerchantsettlementbatches_merchants') THEN

            alter table dbo.posmerchantsettlementbatches add constraint fk_posmerchantsettlementbatches_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementbatches') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posmerchantsettlementbatches' AND indexname='ix_ri_posmerchantsettlementbatches_merchants') THEN
            create index if not exists ix_ri_posmerchantsettlementbatches_merchants on dbo.posmerchantsettlementbatches(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posdevicecommands_terminalprofiles') THEN

            alter table dbo.posdevicecommands add constraint fk_posdevicecommands_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posdevicecommands' AND indexname='ix_ri_posdevicecommands_terminalprofiles') THEN
            create index if not exists ix_ri_posdevicecommands_terminalprofiles on dbo.posdevicecommands(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poscommandqueue_terminalprofiles') THEN

            alter table dbo.poscommandqueue add constraint fk_poscommandqueue_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poscommandqueue' AND indexname='ix_ri_poscommandqueue_terminalprofiles') THEN
            create index if not exists ix_ri_poscommandqueue_terminalprofiles on dbo.poscommandqueue(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlesstxns') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posofflinecontactlesstxns_terminalprofiles') THEN

            alter table dbo.posofflinecontactlesstxns add constraint fk_posofflinecontactlesstxns_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlesstxns') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posofflinecontactlesstxns' AND indexname='ix_ri_posofflinecontactlesstxns_terminalprofiles') THEN
            create index if not exists ix_ri_posofflinecontactlesstxns_terminalprofiles on dbo.posofflinecontactlesstxns(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlesstxns') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posofflinecontactlesstxns_merchants') THEN

            alter table dbo.posofflinecontactlesstxns add constraint fk_posofflinecontactlesstxns_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlesstxns') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posofflinecontactlesstxns' AND indexname='ix_ri_posofflinecontactlesstxns_merchants') THEN
            create index if not exists ix_ri_posofflinecontactlesstxns_merchants on dbo.posofflinecontactlesstxns(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlessbatches') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posofflinecontactlessbatches_merchants') THEN

            alter table dbo.posofflinecontactlessbatches add constraint fk_posofflinecontactlessbatches_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlessbatches') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posofflinecontactlessbatches' AND indexname='ix_ri_posofflinecontactlessbatches_merchants') THEN
            create index if not exists ix_ri_posofflinecontactlessbatches_merchants on dbo.posofflinecontactlessbatches(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeyceremonies') is not null and to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poskeyceremonies_terminalprofiles') THEN

            alter table dbo.poskeyceremonies add constraint fk_poskeyceremonies_terminalprofiles foreign key(terminalid) references dbo.posterminalprofiles(terminalid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeyceremonies') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poskeyceremonies' AND indexname='ix_ri_poskeyceremonies_terminalprofiles') THEN
            create index if not exists ix_ri_poskeyceremonies_terminalprofiles on dbo.poskeyceremonies(terminalid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeyceremonies') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_poskeyceremonies_merchants') THEN

            alter table dbo.poskeyceremonies add constraint fk_poskeyceremonies_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poskeyceremonies') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='poskeyceremonies' AND indexname='ix_ri_poskeyceremonies_merchants') THEN
            create index if not exists ix_ri_poskeyceremonies_merchants on dbo.poskeyceremonies(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementpostings') is not null and to_regclass('dbo.posmerchants') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_posmerchantsettlementpostings_merchants') THEN

            alter table dbo.posmerchantsettlementpostings add constraint fk_posmerchantsettlementpostings_merchants foreign key(merchantid) references dbo.posmerchants(merchantid);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementpostings') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posmerchantsettlementpostings' AND indexname='ix_ri_posmerchantsettlementpostings_merchants') THEN
            create index if not exists ix_ri_posmerchantsettlementpostings_merchants on dbo.posmerchantsettlementpostings(merchantid);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_runs') is not null and to_regclass('dbo.acquiring_cert_packs') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_acquiringcertruns_packs') THEN

            alter table dbo.acquiring_cert_runs add constraint fk_acquiringcertruns_packs foreign key(pack_id) references dbo.acquiring_cert_packs(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_runs') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='acquiring_cert_runs' AND indexname='ix_ri_acquiringcertruns_packs') THEN
            create index if not exists ix_ri_acquiringcertruns_packs on dbo.acquiring_cert_runs(pack_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') is not null and to_regclass('dbo.acquiring_cert_runs') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_acquiringcertresults_runs') THEN

            alter table dbo.acquiring_cert_test_results add constraint fk_acquiringcertresults_runs foreign key(run_id) references dbo.acquiring_cert_runs(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='acquiring_cert_test_results' AND indexname='ix_ri_acquiringcertresults_runs') THEN
            create index if not exists ix_ri_acquiringcertresults_runs on dbo.acquiring_cert_test_results(run_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') is not null and to_regclass('dbo.acquiring_cert_test_cases') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_acquiringcertresults_testcases') THEN

            alter table dbo.acquiring_cert_test_results add constraint fk_acquiringcertresults_testcases foreign key(test_case_id) references dbo.acquiring_cert_test_cases(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='acquiring_cert_test_results' AND indexname='ix_ri_acquiringcertresults_testcases') THEN
            create index if not exists ix_ri_acquiringcertresults_testcases on dbo.acquiring_cert_test_results(test_case_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_evidence_reports') is not null and to_regclass('dbo.acquiring_cert_runs') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_acquiringcertreports_runs') THEN

            alter table dbo.acquiring_cert_evidence_reports add constraint fk_acquiringcertreports_runs foreign key(run_id) references dbo.acquiring_cert_runs(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_evidence_reports') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='acquiring_cert_evidence_reports' AND indexname='ix_ri_acquiringcertreports_runs') THEN
            create index if not exists ix_ri_acquiringcertreports_runs on dbo.acquiring_cert_evidence_reports(run_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_runs') is not null and to_regclass('dbo.issuer_cert_packs') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_issuercertruns_packs') THEN

            alter table dbo.issuer_cert_runs add constraint fk_issuercertruns_packs foreign key(pack_id) references dbo.issuer_cert_packs(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_runs') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='issuer_cert_runs' AND indexname='ix_ri_issuercertruns_packs') THEN
            create index if not exists ix_ri_issuercertruns_packs on dbo.issuer_cert_runs(pack_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_run_results') is not null and to_regclass('dbo.issuer_cert_runs') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_issuercertresults_runs') THEN

            alter table dbo.issuer_cert_run_results add constraint fk_issuercertresults_runs foreign key(run_id) references dbo.issuer_cert_runs(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_run_results') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='issuer_cert_run_results' AND indexname='ix_ri_issuercertresults_runs') THEN
            create index if not exists ix_ri_issuercertresults_runs on dbo.issuer_cert_run_results(run_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_run_results') is not null and to_regclass('dbo.issuer_cert_test_cases') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_issuercertresults_testcases') THEN

            alter table dbo.issuer_cert_run_results add constraint fk_issuercertresults_testcases foreign key(test_case_id) references dbo.issuer_cert_test_cases(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_run_results') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='issuer_cert_run_results' AND indexname='ix_ri_issuercertresults_testcases') THEN
            create index if not exists ix_ri_issuercertresults_testcases on dbo.issuer_cert_run_results(test_case_id);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_evidence_reports') is not null and to_regclass('dbo.issuer_cert_runs') is not null and not exists (select 1 from pg_constraint c join pg_namespace n on n.oid=c.connamespace where n.nspname='dbo' and c.conname='fk_issuercertreports_runs') THEN

            alter table dbo.issuer_cert_evidence_reports add constraint fk_issuercertreports_runs foreign key(run_id) references dbo.issuer_cert_runs(id);

    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.issuer_cert_evidence_reports') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='issuer_cert_evidence_reports' AND indexname='ix_ri_issuercertreports_runs') THEN
            create index if not exists ix_ri_issuercertreports_runs on dbo.issuer_cert_evidence_reports(run_id);
    END IF;
END $$;


-- JSON integrity checks for operational payload columns.

DO $$
BEGIN
    IF to_regclass('dbo.atm_terminal_profile') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='atm_terminal_profile' AND column_name='capabilities_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='atm_terminal_profile' and c.conname='ck_json_atm_terminal_profile_capabilities_json') THEN
            alter table dbo.atm_terminal_profile add constraint ck_json_atm_terminal_profile_capabilities_json check (capabilities_json is null or (capabilities_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_definition') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='atm_screen_definition' AND column_name='screen_flow_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='atm_screen_definition' and c.conname='ck_json_atm_screen_definition_screen_flow_json') THEN
            alter table dbo.atm_screen_definition add constraint ck_json_atm_screen_definition_screen_flow_json check (screen_flow_json is null or (screen_flow_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_distribution_job') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='atm_screen_distribution_job' AND column_name='terminal_ids_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='atm_screen_distribution_job' and c.conname='ck_json_atm_screen_distribution_job_terminal_ids_json') THEN
            alter table dbo.atm_screen_distribution_job add constraint ck_json_atm_screen_distribution_job_terminal_ids_json check (terminal_ids_json is null or (terminal_ids_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_distribution_job') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='atm_screen_distribution_job' AND column_name='terminal_statuses_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='atm_screen_distribution_job' and c.conname='ck_json_atm_screen_distribution_job_terminal_statuses_json') THEN
            alter table dbo.atm_screen_distribution_job add constraint ck_json_atm_screen_distribution_job_terminal_statuses_json check (terminal_statuses_json is null or (terminal_statuses_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_admin_cash_operation') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='atm_admin_cash_operation' AND column_name='cassettes_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='atm_admin_cash_operation' and c.conname='ck_json_atm_admin_cash_operation_cassettes_json') THEN
            alter table dbo.atm_admin_cash_operation add constraint ck_json_atm_admin_cash_operation_cassettes_json check (cassettes_json is null or (cassettes_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_voice_prompt_pack') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='atm_voice_prompt_pack' AND column_name='prompt_file_uris_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='atm_voice_prompt_pack' and c.conname='ck_json_atm_voice_prompt_pack_prompt_file_uris_json') THEN
            alter table dbo.atm_voice_prompt_pack add constraint ck_json_atm_voice_prompt_pack_prompt_file_uris_json check (prompt_file_uris_json is null or (prompt_file_uris_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='posterminalprofiles' AND column_name='capabilitiesjson') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='posterminalprofiles' and c.conname='ck_json_posterminalprofiles_capabilitiesjson') THEN
            alter table dbo.posterminalprofiles add constraint ck_json_posterminalprofiles_capabilitiesjson check (capabilitiesjson is null or (capabilitiesjson::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='posdevicecommands' AND column_name='parametersjson') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='posdevicecommands' and c.conname='ck_json_posdevicecommands_parametersjson') THEN
            alter table dbo.posdevicecommands add constraint ck_json_posdevicecommands_parametersjson check (parametersjson is null or (parametersjson::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='poscommandqueue' AND column_name='parametersjson') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='poscommandqueue' and c.conname='ck_json_poscommandqueue_parametersjson') THEN
            alter table dbo.poscommandqueue add constraint ck_json_poscommandqueue_parametersjson check (parametersjson is null or (parametersjson::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_cases') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_test_cases' AND column_name='input_fields_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_test_cases' and c.conname='ck_json_acquiring_cert_test_cases_input_fields_json') THEN
            alter table dbo.acquiring_cert_test_cases add constraint ck_json_acquiring_cert_test_cases_input_fields_json check (input_fields_json is null or (input_fields_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_cases') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_test_cases' AND column_name='expected_fields_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_test_cases' and c.conname='ck_json_acquiring_cert_test_cases_expected_fields_json') THEN
            alter table dbo.acquiring_cert_test_cases add constraint ck_json_acquiring_cert_test_cases_expected_fields_json check (expected_fields_json is null or (expected_fields_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_packs') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_packs' AND column_name='test_case_ids_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_packs' and c.conname='ck_json_acquiring_cert_packs_test_case_ids_json') THEN
            alter table dbo.acquiring_cert_packs add constraint ck_json_acquiring_cert_packs_test_case_ids_json check (test_case_ids_json is null or (test_case_ids_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_test_results' AND column_name='findings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_test_results' and c.conname='ck_json_acquiring_cert_test_results_findings_json') THEN
            alter table dbo.acquiring_cert_test_results add constraint ck_json_acquiring_cert_test_results_findings_json check (findings_json is null or (findings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_message_validation_results') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_message_validation_results' AND column_name='findings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_message_validation_results' and c.conname='ck_json_acquiring_message_validation_results_findings_json') THEN
            alter table dbo.acquiring_message_validation_results add constraint ck_json_acquiring_message_validation_results_findings_json check (findings_json is null or (findings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_host_response_validation_results') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_host_response_validation_results' AND column_name='findings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_host_response_validation_results' and c.conname='ck_json_acquiring_host_response_validation_results_findings_json') THEN
            alter table dbo.acquiring_host_response_validation_results add constraint ck_json_acquiring_host_response_validation_results_findings_json check (findings_json is null or (findings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_flow_results') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_flow_results' AND column_name='findings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_flow_results' and c.conname='ck_json_acquiring_cert_flow_results_findings_json') THEN
            alter table dbo.acquiring_cert_flow_results add constraint ck_json_acquiring_cert_flow_results_findings_json check (findings_json is null or (findings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_scenarios') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_scenarios' AND column_name='steps_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_scenarios' and c.conname='ck_json_acquiring_cert_scenarios_steps_json') THEN
            alter table dbo.acquiring_cert_scenarios add constraint ck_json_acquiring_cert_scenarios_steps_json check (steps_json is null or (steps_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_replay_runs') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_replay_runs' AND column_name='masked_samples_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_replay_runs' and c.conname='ck_json_acquiring_cert_replay_runs_masked_samples_json') THEN
            alter table dbo.acquiring_cert_replay_runs add constraint ck_json_acquiring_cert_replay_runs_masked_samples_json check (masked_samples_json is null or (masked_samples_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_fuzz_runs') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_fuzz_runs' AND column_name='findings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_fuzz_runs' and c.conname='ck_json_acquiring_cert_fuzz_runs_findings_json') THEN
            alter table dbo.acquiring_cert_fuzz_runs add constraint ck_json_acquiring_cert_fuzz_runs_findings_json check (findings_json is null or (findings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_regression_runs') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_regression_runs' AND column_name='regressions_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_regression_runs' and c.conname='ck_json_acquiring_cert_regression_runs_regressions_json') THEN
            alter table dbo.acquiring_cert_regression_runs add constraint ck_json_acquiring_cert_regression_runs_regressions_json check (regressions_json is null or (regressions_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_plugins') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='acquiring_cert_plugins' AND column_name='configuration_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='acquiring_cert_plugins' and c.conname='ck_json_acquiring_cert_plugins_configuration_json') THEN
            alter table dbo.acquiring_cert_plugins add constraint ck_json_acquiring_cert_plugins_configuration_json check (configuration_json is null or (configuration_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='iso8583_network_profiles' AND column_name='mandatory_fields_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='iso8583_network_profiles' and c.conname='ck_json_iso8583_network_profiles_mandatory_fields_json') THEN
            alter table dbo.iso8583_network_profiles add constraint ck_json_iso8583_network_profiles_mandatory_fields_json check (mandatory_fields_json is null or (mandatory_fields_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='iso8583_network_profiles' AND column_name='field_mappings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='iso8583_network_profiles' and c.conname='ck_json_iso8583_network_profiles_field_mappings_json') THEN
            alter table dbo.iso8583_network_profiles add constraint ck_json_iso8583_network_profiles_field_mappings_json check (field_mappings_json is null or (field_mappings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='iso8583_network_profiles' AND column_name='response_code_map_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='iso8583_network_profiles' and c.conname='ck_json_iso8583_network_profiles_response_code_map_json') THEN
            alter table dbo.iso8583_network_profiles add constraint ck_json_iso8583_network_profiles_response_code_map_json check (response_code_map_json is null or (response_code_map_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_host_profiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='network_host_profiles' AND column_name='settings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='network_host_profiles' and c.conname='ck_json_network_host_profiles_settings_json') THEN
            alter table dbo.network_host_profiles add constraint ck_json_network_host_profiles_settings_json check (settings_json is null or (settings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_message_journal') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='network_message_journal' AND column_name='fields_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='network_message_journal' and c.conname='ck_json_network_message_journal_fields_json') THEN
            alter table dbo.network_message_journal add constraint ck_json_network_message_journal_fields_json check (fields_json is null or (fields_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.enterprise_connector_profiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='enterprise_connector_profiles' AND column_name='settings_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='enterprise_connector_profiles' and c.conname='ck_json_enterprise_connector_profiles_settings_json') THEN
            alter table dbo.enterprise_connector_profiles add constraint ck_json_enterprise_connector_profiles_settings_json check (settings_json is null or (settings_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_evaluations') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='risk_evaluations' AND column_name='hits_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='risk_evaluations' and c.conname='ck_json_risk_evaluations_hits_json') THEN
            alter table dbo.risk_evaluations add constraint ck_json_risk_evaluations_hits_json check (hits_json is null or (hits_json::jsonb IS NOT NULL));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_model_profiles') is not null and (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='risk_model_profiles' AND column_name='feature_set_json') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='risk_model_profiles' and c.conname='ck_json_risk_model_profiles_feature_set_json') THEN
            alter table dbo.risk_model_profiles add constraint ck_json_risk_model_profiles_feature_set_json check (feature_set_json is null or (feature_set_json::jsonb IS NOT NULL));
    END IF;
END $$;


-- Core uniqueness / range constraints required by production invariants.

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='posterminalprofiles' AND indexname='ux_posterminalprofiles_serialnumber') THEN
            create unique index if not exists ux_posterminalprofiles_serialnumber on dbo.posterminalprofiles(serialnumber);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.network_host_profiles') is not null and not exists (select 1 from pg_indexes where schemaname='dbo' AND tablename='network_host_profiles' AND indexname='ux_network_host_profiles_code') THEN
            create unique index if not exists ux_network_host_profiles_code on dbo.network_host_profiles(host_code);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournalentries') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='gljournalentries' and c.conname='ck_gljournalentries_chainhash') THEN
            alter table dbo.gljournalentries add constraint ck_gljournalentries_chainhash check ((chainsequence=0) or (length(entryhash)=64 and length(previoushash)>=7));
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_model_profiles') is not null and not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid join pg_namespace n on n.oid=t.relnamespace where n.nspname='dbo' and t.relname='risk_model_profiles' and c.conname='ck_risk_model_threshold_order') THEN
            alter table dbo.risk_model_profiles add constraint ck_risk_model_threshold_order check (review_threshold between 0 and 100 and decline_threshold between 0 and 100 and review_threshold <= decline_threshold);
    END IF;
END $$;

commit;
