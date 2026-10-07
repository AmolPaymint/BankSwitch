-- postgresql conversion of bankswitch canonical sql server schema through migration 042
-- converted using the requested naming/type/constraint rules.

create schema if not exists dbo;
create extension if not exists pgcrypto;

begin;

-- bankswitch canonical sql server schema through migration 042

-- generated from authoritative ordered migrations.



-- ============================================================================

-- migration 001_production_schema.sql

-- ============================================================================

/\* bankswitch v21 production baseline schema for sql server.

   execute as dba, then grant app login membership in db_datareader/db_datawriter only for required tables or custom least-privilege role.

*/

create table dbo.sourcenodes (

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

create table dbo.sinknodes (

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

create table dbo.routes (

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

create index ix_routes_advancedlookup on dbo.routes(isactive, priority desc, binprefix);

create table dbo.fees (

    id uuid not null constraint pk_fees primary key,

    name varchar(200) not null,

    flatamount decimal(19,2) not null,

    percentageoftransaction decimal(9,4) not null,

    minimum decimal(19,2) not null,

    maximum decimal(19,2) not null,

    isactive boolean not null,

    createdat timestamptz not null constraint df_fees_createdat default(clock_timestamp())

);

create table dbo.schemes (

    id uuid not null constraint pk_schemes primary key,

    name varchar(200) not null,

    sourcenodeid uuid not null,

    routeid uuid not null constraint fk_schemes_routes references dbo.routes(id),

    isactive boolean not null,

    createdat timestamptz not null constraint df_schemes_createdat default(clock_timestamp())

);

create index ix_schemes_sourceroute on dbo.schemes(sourcenodeid, routeid, isactive);

create table dbo.schemepermissions (

    schemeid uuid not null constraint fk_schemepermissions_schemes references dbo.schemes(id),

    transactiontypecode varchar(2) not null,

    channelcode varchar(2) not null,

    feeid uuid not null constraint fk_schemepermissions_fees references dbo.fees(id),

    constraint pk_schemepermissions primary key (schemeid, transactiontypecode, channelcode)

);

create table dbo.transactionlogs (

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

    businessdate date generated always as ((createdat at time zone 'Asia/Kolkata')::date) stored

);

create index ix_transactionlogs_stan on dbo.transactionlogs(stan);

create index ix_transactionlogs_rrn on dbo.transactionlogs(rrn);

create index ix_transactionlogs_date on dbo.transactionlogs(createdat);

create index ix_transactionlogs_sourcedate on dbo.transactionlogs(sourcenodeid, createdat);

create index ix_transactionlogs_panhash on dbo.transactionlogs(panhash);

create unique index ux_transactionlogs_duplicate on dbo.transactionlogs(sourcenodeid, stan, rrn, amount, businessdate) where mti in ('0100','0200','0220');

create table dbo.reversalworkitems (

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

create unique index ux_reversalworkitems_acceptedoriginal on dbo.reversalworkitems(originaltransactionid) where state = 'Accepted';

create index ix_reversalworkitems_due on dbo.reversalworkitems(state, nextattemptat, attemptcount);

-- development seed data matching the in-memory package. replace before production.

DO $$
DECLARE
    sinkid uuid := gen_random_uuid();
    routeid uuid := gen_random_uuid();
    sourceid uuid := gen_random_uuid();
    feeid uuid := gen_random_uuid();
    schemeid uuid := gen_random_uuid();
BEGIN
    INSERT INTO dbo.sinknodes(id, nodeid, name, host, port, isactive, requiremtls, requireprivatenetwork, allowedcidrs, certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds, permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile)
    VALUES(sinkid, 'SNK-DEV-001', 'Development Sink', '127.0.0.1', 5001, true, false, false, '127.0.0.1', '', 100, 0, 4096, 65, '0200;0420', '01', '539983', 'DEV-SINK', 'DEV-SETTLEMENT');

    INSERT INTO dbo.sourcenodes(id, nodeid, name, isactive, requiremtls, requireprivatenetwork, allowedcidrs, certificatethumbprint, tpslimit, dailyamountlimit, maxmessagebytes, idletimeoutseconds, permittedmtis, permittedchannels, allowedbinranges, keyprofile, settlementprofile)
    VALUES(sourceid, 'SRC-DEV-001', 'Development Source', true, false, false, '127.0.0.1', '', 100, 0, 4096, 65, '0200;0420', '01', '539983', 'DEV-SOURCE', 'DEV-SETTLEMENT');

    INSERT INTO dbo.routes(id, binprefix, sinknodeid, isactive) VALUES(routeid, '539983', sinkid, true);
    INSERT INTO dbo.fees(id, name, flatamount, percentageoftransaction, minimum, maximum, isactive) VALUES(feeid, 'Development flat fee', 10.00, 0.0000, 0.00, 0.00, true);
    INSERT INTO dbo.schemes(id, name, sourcenodeid, routeid, isactive) VALUES(schemeid, 'Development scheme', sourceid, routeid, true);
    INSERT INTO dbo.schemepermissions(schemeid, transactiontypecode, channelcode, feeid) VALUES(schemeid, '00', '01', feeid), (schemeid, '20', '01', feeid);
END
$$;

create table dbo.configchangerequests (

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

create index ix_configchangerequests_stateeffective on dbo.configchangerequests(state, effectiveat);

create index ix_configchangerequests_ticket on dbo.configchangerequests(ticketreference);

-- ============================================================================

-- migration 002_core_prepaid_cms_phase1.sql

-- ============================================================================

/\*

  phase 1 core prepaid cms schema.

  run after db/001_production_schema.sql.

*/

create table dbo.prepaidprograms

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

create table dbo.limitprofiles

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

create table dbo.cardproducts

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

create table dbo.customers

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

create table dbo.walletaccounts

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

create table dbo.prepaidcards

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

create table dbo.ledgerentries

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

create table dbo.cmstransactionlogs

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

create index ix_cardproducts_program on dbo.cardproducts(programid, status);

create index ix_customers_status on dbo.customers(status, kycstatus);

create index ix_walletaccounts_customer on dbo.walletaccounts(customerid);

create index ix_prepaidcards_customerstatus on dbo.prepaidcards(customerid, status);

create index ix_prepaidcards_panhash on dbo.prepaidcards(panhash);

create index ix_ledgerentries_walletdatetype on dbo.ledgerentries(walletaccountid, createdat, entrytype);

create index ix_cmstransactionlogs_rrnstanpanhash on dbo.cmstransactionlogs(rrn, stan, panhash);

create index ix_cmstransactionlogs_createdat on dbo.cmstransactionlogs(createdat);

-- ============================================================================

-- migration 003_operational_control_phase2.sql

-- ============================================================================

/\*

phase 2 operational control schema for prepaid cms.

apply after:

  db/001_production_schema.sql

  db/002_core_prepaid_cms_phase1.sql

*/

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='PrepaidCards' and column_name='OwnerType') then
    alter table dbo.prepaidcards add

        ownertype varchar(40) not null constraint df_prepaidcards_ownertype default ('Customer'),

        agencyid uuid null,

        corporateid uuid null,

        corporatedepartmentid uuid null,

        corporateemployeeid uuid null,

        inventorybatchreference varchar(80) not null constraint df_prepaidcards_inventorybatchreference default ('');
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.agencyprofiles') is null then
    create table dbo.agencyprofiles

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

    create unique index ux_agencyprofiles_agencycode on dbo.agencyprofiles(agencycode);

    create index ix_agencyprofiles_status on dbo.agencyprofiles(status);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.agencycreditledgerentries') is null then
    create table dbo.agencycreditledgerentries

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

    create index ix_agencycreditledger_agencydate on dbo.agencycreditledgerentries(agencyid, createdat);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.corporateprofiles') is null then
    create table dbo.corporateprofiles

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

    create unique index ux_corporateprofiles_corporatecode on dbo.corporateprofiles(corporatecode);

    create index ix_corporateprofiles_status on dbo.corporateprofiles(status);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.corporatedepartments') is null then
    create table dbo.corporatedepartments

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

    create unique index ux_corporatedepartments_corpcode on dbo.corporatedepartments(corporateid, departmentcode);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.corporateemployees') is null then
    create table dbo.corporateemployees

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

    create unique index ux_corporateemployees_corpemployee on dbo.corporateemployees(corporateid, employeenumber);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.corporatebudgets') is null then
    create table dbo.corporatebudgets

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

    create index ix_corporatebudgets_corpperiod on dbo.corporatebudgets(corporateid, periodstart, periodend, status);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.cardstockbatches') is null then
    create table dbo.cardstockbatches

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

    create unique index ux_cardstockbatches_batchreference on dbo.cardstockbatches(batchreference);

    create index ix_cardstockbatches_productowner on dbo.cardstockbatches(productid, ownertype, ownerid, status);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.advancedlimitrules') is null then
    create table dbo.advancedlimitrules

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

    create unique index ux_advancedlimitrules_rulecode on dbo.advancedlimitrules(rulecode);

    create index ix_advancedlimitrules_activepriority on dbo.advancedlimitrules(isactive, priority);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.riskrules') is null then
    create table dbo.riskrules

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

    create unique index ux_riskrules_rulecode on dbo.riskrules(rulecode);

    create index ix_riskrules_activepriority on dbo.riskrules(isactive, priority);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.notificationmessages') is null then
    create table dbo.notificationmessages

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

    create index ix_notificationmessages_statusdate on dbo.notificationmessages(status, createdat);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.statementdocuments') is null then
    create table dbo.statementdocuments

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

    create unique index ux_statementdocuments_statementnumber on dbo.statementdocuments(statementnumber);

    create index ix_statementdocuments_ownerperiod on dbo.statementdocuments(ownertype, ownerid, periodstart, periodend);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.statementlines') is null then
    create table dbo.statementlines

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

    create index ix_statementlines_statementdate on dbo.statementlines(statementid, transactiondate);
    end if;
end
$$;

create index if not exists ix_prepaidcards_agency on dbo.prepaidcards(agencyid) where agencyid is not null;

create index if not exists ix_prepaidcards_corporate on dbo.prepaidcards(corporateid) where corporateid is not null;

create index if not exists ix_prepaidcards_employee on dbo.prepaidcards(corporateemployeeid) where corporateemployeeid is not null;

-- ============================================================================

-- migration 004_financial_operations_phase3.sql

-- ============================================================================

/\* phase 3 - financial operations schema for core prepaid cms. apply after 001, 002 and 003. */

do $$
begin
    if to_regclass('dbo.settlementbatches') is null then
    create table dbo.settlementbatches

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

    create unique index ux_settlementbatches_batchreference on dbo.settlementbatches(batchreference);

    create index ix_settlementbatches_statusdate on dbo.settlementbatches(status, settlementdate);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.settlementrecords') is null then
    create table dbo.settlementrecords

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

    create index ix_settlementrecords_batch on dbo.settlementrecords(batchid);

    create index ix_settlementrecords_matching on dbo.settlementrecords(rrn, stan, panhash);

    create index ix_settlementrecords_status on dbo.settlementrecords(status, recordtype);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.reconciliationexceptions') is null then
    create table dbo.reconciliationexceptions

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

    create index ix_reconciliationexceptions_status on dbo.reconciliationexceptions(status, createdat);

    create index ix_reconciliationexceptions_reference on dbo.reconciliationexceptions(reference);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.gljournalentries') is null then
    create table dbo.gljournalentries

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

    create unique index ux_gljournalentries_journalnumber on dbo.gljournalentries(journalnumber);

    create index ix_gljournalentries_sourcereference on dbo.gljournalentries(sourcemodule, reference);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.gljournallines') is null then
    create table dbo.gljournallines

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

    create index ix_gljournallines_journal on dbo.gljournallines(journalentryid);

    create index ix_gljournallines_account on dbo.gljournallines(accountcode);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.financialoperations') is null then
    create table dbo.financialoperations

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

    create index ix_financialoperations_original on dbo.financialoperations(operationtype, originalrrn, originalstan, panhash, status);

    create index ix_financialoperations_statusdate on dbo.financialoperations(status, createdat);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.settlementstatements') is null then
    create table dbo.settlementstatements

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

    create unique index ux_settlementstatements_number on dbo.settlementstatements(statementnumber);

    create index ix_settlementstatements_partyperiod on dbo.settlementstatements(partytype, partyid, periodstart, periodend);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.settlementstatementlines') is null then
    create table dbo.settlementstatementlines

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

    create index ix_settlementstatementlines_statement on dbo.settlementstatementlines(settlementstatementid);
    end if;
end
$$;

-- ============================================================================

-- migration 005_enterprise_production_phase4.sql

-- ============================================================================

-- phase 4 enterprise production cms migration

-- apply after 001_production_schema.sql, 002_core_prepaid_cms_phase1.sql,

-- 003_operational_control_phase2.sql, and 004_financial_operations_phase3.sql.

-- this schema matches sqlenterpriseproductionrepository and the phase 4 domain model.

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

create index if not exists ix_cryptokeyprofiles_purposestatus on dbo.cryptokeyprofiles(purpose, status, rotationdueat);

create index if not exists ix_amlwatchlist_active on dbo.amlwatchlistentries(isactive, listtype, entityname);

create index if not exists ix_amlscreening_entity on dbo.amlscreeningrecords(entityreference, createdat desc);

create index if not exists ix_threeds_dstransaction on dbo.threedsauthenticationrecords(directoryservertransactionid);

create index if not exists ix_threeds_panhashdate on dbo.threedsauthenticationrecords(panhash, createdat desc);

create index if not exists ix_fraudevents_panhashdate on dbo.fraudmonitoringevents(panhash, createdat desc);

create index if not exists ix_fraudalerts_status on dbo.fraudalerts(status, severity, createdat desc);

create index if not exists ix_siemsecurityevents_pending on dbo.siemsecurityevents(deliverystatus, createdat);

create index if not exists ix_datawarehouseexportjobs_due on dbo.datawarehouseexportjobs(status, createdat);

create index if not exists ix_clusterheartbeats_status on dbo.clusternodeheartbeats(healthstatus, lastheartbeatat desc);

create index if not exists ix_regulatoryreports_period on dbo.regulatoryreports(reporttype, periodstart, periodend, status);

-- ============================================================================

-- migration 006_tokenization_terminal_keys_institutions.sql

-- ============================================================================

-- phase 5 migration: card-on-file tokenization (coft), dynamic terminal session keys, and

-- multi-institutional configuration.

-- apply after 001_production_schema.sql through 005_enterprise_production_phase4.sql.

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

create index if not exists ix_cardtokens_panhash_merchant on dbo.cardtokens(panhash, merchantid);

create table if not exists dbo.terminalkeyprofiles

(

    id uuid not null constraint pk_terminalkeyprofiles primary key,

    terminalid varchar(16) not null,

    sourcenodeid varchar(64) not null,

    keyprofile varchar(128) not null,

    keyserialnumber varchar(20) not null constraint df_terminalkeyprofiles_ksn default(''),

    keycheckvalue varchar(16) not null constraint df_terminalkeyprofiles_kcv default(''),

    isactive boolean not null constraint df_terminalkeyprofiles_active DEFAULT true,

    createdat timestamptz not null,

    lastrotatedat timestamptz null,

    constraint ux_terminalkeyprofiles_terminalid unique(terminalid)

);

create table if not exists dbo.institutions

(

    id uuid not null constraint pk_institutions primary key,

    code varchar(32) not null,

    name varchar(200) not null,

    type varchar(16) not null,

    countrycode varchar(2) not null constraint df_institutions_country default(''),

    defaultcurrencycode varchar(3) not null constraint df_institutions_currency default(''),

    isactive boolean not null constraint df_institutions_active DEFAULT true,

    constraint ux_institutions_code unique(code)

);

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='sourcenodes' and column_name='institutioncode') then
        alter table dbo.sourcenodes add institutioncode varchar(32) not null constraint df_sourcenodes_institutioncode default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='sinknodes' and column_name='institutioncode') then
        alter table dbo.sinknodes add institutioncode varchar(32) not null constraint df_sinknodes_institutioncode default('');
    end if;
end
$$;

-- ============================================================================

-- migration 007_card_fee_rules.sql

-- ============================================================================

-- phase 6 migration: card-lifecycle fee/waiver configuration (issuance, replacement, upgrade,

-- repin, annual maintenance, add-on card), each scoped bin-wise, account-scheme-wise, or

-- card-wise.

-- apply after 001_production_schema.sql through 006_tokenization_terminal_keys_institutions.sql.

create table if not exists dbo.cardfeerules

(

    id uuid not null constraint pk_cardfeerules primary key,

    feetype varchar(32) not null,

    scopetype varchar(16) not null,

    scopevalue varchar(64) not null,

    iswaiver boolean not null constraint df_cardfeerules_iswaiver DEFAULT false,

    feeid uuid null,

    isactive boolean not null constraint df_cardfeerules_active DEFAULT true,

    description varchar(400) not null constraint df_cardfeerules_description default(''),

    createdat timestamptz not null,

    constraint fk_cardfeerules_fees foreign key (feeid) references dbo.fees(id)

);

create index if not exists ix_cardfeerules_lookup on dbo.cardfeerules(feetype, scopetype, scopevalue, isactive);

-- ============================================================================

-- migration 008_ledger_and_crypto_hardening.sql

-- ============================================================================

-- ============================================================

-- migration 008 — financial ledger & cryptographic hardening

-- fixes for cd-01 (double-entry ledger), cd-02 (concurrency),

-- and cd-04 (cvv storage).

-- apply after 001 through 007.

-- ============================================================

-- ---------------------------------------------------------------

-- cd-01 fix: chart of accounts master table

-- gljournallines references account codes as free text — adding

-- the accounts master for referential integrity and reporting.

-- ---------------------------------------------------------------

create table if not exists dbo.glaccounts

(

    id           uuid not null constraint pk_glaccounts primary key default gen_random_uuid(),

    accountcode  varchar(64)     not null,

    name         varchar(200)    not null,

    accounttype  varchar(16)     not null,  -- asset | liability | income | expense | clearing | suspense

    currencycode varchar(3)      not null constraint df_glaccounts_currency default (''),

    isactive     boolean              not null constraint df_glaccounts_active DEFAULT true,

    createdat    timestamptz   not null constraint df_glaccounts_created default (clock_timestamp()),

    constraint ux_glaccounts_code unique (accountcode)

);

-- seed the five standard accounts used by financialoperationsservice.

-- these match the constants: glsettlementclearing, glnostrofunding,

-- glcardholderliability, glfeeincome, gladjustmentexpense.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.glaccounts WHERE accountcode = '1000-SETTLEMENT-CLEARING') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)

values ('1000-SETTLEMENT-CLEARING', 'Settlement Clearing Account', 'Clearing', '');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.glaccounts WHERE accountcode = '1100-NOSTRO-FUNDING') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)

values ('1100-NOSTRO-FUNDING', 'Nostro / Funding Receivable', 'Asset', '');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.glaccounts WHERE accountcode = '2100-CARDHOLDER-LIABILITY') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)

values ('2100-CARDHOLDER-LIABILITY', 'Cardholder E-Money Liability', 'Liability', '');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.glaccounts WHERE accountcode = '4000-FEE-INCOME') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)

values ('4000-FEE-INCOME', 'Transaction Fee Income', 'Income', '');
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.glaccounts WHERE accountcode = '5000-ADJUSTMENT-EXPENSE') THEN
        insert into dbo.glaccounts (accountcode, name, accounttype, currencycode)

values ('5000-ADJUSTMENT-EXPENSE', 'Financial Adjustment Expense', 'Expense', '');
    END IF;
END
$$;

-- ---------------------------------------------------------------

-- verify walletaccounts already has the bytea column

-- (added in migration 002). guard is idempotent.

-- ---------------------------------------------------------------

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='walletaccounts' and column_name='rowversion') then
    alter table dbo.walletaccounts add rowversion bytea not null;

    -- SQL Server PRINT omitted: 'Added bytea column to dbo.WalletAccounts'
    END IF;
END
$$;

-- ---------------------------------------------------------------

-- cd-04 fix: cvv2token — encrypted cvv2 value on prepaidcards.

-- populated at card issuance via hsm generatecvv + aes-gcm protect.

-- null allowed for cards issued before this migration.

-- ---------------------------------------------------------------

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='prepaidcards' AND column_name='cvv2token') THEN
        alter table dbo.prepaidcards

    add cvv2token text null constraint df_prepaidcards_cvv2token default ('');
    END IF;
END
$$;

-- SQL Server PRINT omitted: 'Added Cvv2Token column to dbo.PrepaidCards'

-- ============================================================================

-- migration 009_eft_orchestration.sql

-- ============================================================================

-- ============================================================

-- migration 009 — eft orchestration & interbank transfer processing

-- implements a1 from the enterprise gap analysis.

-- apply after 001 through 008.

-- ============================================================

-- ---------------------------------------------------------------

-- transaction lifecycle state machine

-- ---------------------------------------------------------------

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

create index if not exists ix_tls_correlationid on dbo.transactionlifecyclestates(correlationid, occurredat);

create index if not exists ix_tls_newstate_occurredat on dbo.transactionlifecyclestates(newstate, occurredat);

-- ---------------------------------------------------------------

-- extend transactionlog with lifecyclestate

-- ---------------------------------------------------------------

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='transactionlog' and column_name='lifecyclestate') then
        alter table dbo.transactionlog add lifecyclestate varchar(32) not null constraint df_txlog_lifecyclestate default ('Received');
    end if;
end
$$;

-- ---------------------------------------------------------------

-- extend routes with fallbacksinknodeid for network failover

-- ---------------------------------------------------------------

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='fallbacksinknodeid') then
        alter table dbo.routes add fallbacksinknodeid uuid null;
    end if;
end
$$;

-- ---------------------------------------------------------------

-- eft transfers (neft / rtgs / imps / ach)

-- ---------------------------------------------------------------

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

create index if not exists ix_efttransfers_correlationid on dbo.efttransfers(correlationid);

create index if not exists ix_efttransfers_status_rail on dbo.efttransfers(status, railtype, createdat);

-- ---------------------------------------------------------------

-- clearing batches

-- ---------------------------------------------------------------

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

create index if not exists ix_clearingbatches_date_profile on dbo.clearingbatches(businessdate, settlementprofile);

-- ---------------------------------------------------------------

-- clearing records (one per transaction per batch)

-- ---------------------------------------------------------------

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

    isincluded           boolean              not null constraint df_cr_included DEFAULT true,

    exclusionreason      varchar(500)    not null constraint df_cr_exclreason default (''),

    constraint fk_clearingrecords_batch foreign key (clearingbatchid) references dbo.clearingbatches(id)

);

create index if not exists ix_clearingrecords_batch on dbo.clearingrecords(clearingbatchid);

-- ============================================================================

-- migration 010_card_lifecycle.sql

-- ============================================================================

-- ============================================================

-- migration 010 — customer onboarding & card lifecycle

-- implements a2 from the enterprise gap analysis.

-- apply after 001 through 009.

-- ============================================================

-- ---------------------------------------------------------------

-- prepaidcards — new lifecycle columns

-- ---------------------------------------------------------------

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='pintoken') then
        alter table dbo.prepaidcards add pintoken text null constraint df_cards_pintoken default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='blockreason') then
        alter table dbo.prepaidcards add blockreason varchar(64) not null constraint df_cards_blockreason default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='blockedat') then
        alter table dbo.prepaidcards add blockedat timestamptz null;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='replacedbycardid') then
        alter table dbo.prepaidcards add replacedbycardid uuid null;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='prepaidcards' and column_name='updatedat') then
        alter table dbo.prepaidcards add updatedat timestamptz null;
    end if;
end
$$;

-- ---------------------------------------------------------------

-- customers — new profile fields

-- ---------------------------------------------------------------

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='updatedat') then
        alter table dbo.customers add updatedat timestamptz null;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='dateofbirth') then
        alter table dbo.customers add dateofbirth varchar(10) not null constraint df_customers_dob default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='addressline1') then
        alter table dbo.customers add addressline1 varchar(200) not null constraint df_customers_addr1 default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='city') then
        alter table dbo.customers add city varchar(100) not null constraint df_customers_city default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='stateorregion') then
        alter table dbo.customers add stateorregion varchar(100) not null constraint df_customers_state default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='countrycode') then
        alter table dbo.customers add countrycode varchar(2) not null constraint df_customers_country default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='customers' and column_name='postalcode') then
        alter table dbo.customers add postalcode varchar(16) not null constraint df_customers_postal default ('');
    end if;
end
$$;

-- ---------------------------------------------------------------

-- kyc documents

-- ---------------------------------------------------------------

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

create index if not exists ix_kycdocuments_customer on dbo.kycdocuments(customerid, submittedat desc);

-- ---------------------------------------------------------------

-- authorization holds (pre-auth / iso 0100 / 0220 / 0420)

-- ---------------------------------------------------------------

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

create index if not exists ix_authholds_wallet_status on dbo.authorizationholds(walletaccountid, status, expiresat);

create index if not exists ix_authholds_rrn on dbo.authorizationholds(rrn, walletaccountid);

-- ============================================================================

-- migration 011_monitoring_alerting.sql

-- ============================================================================

-- ============================================================

-- migration 011 — monitoring, alerting & siem hardening

-- implements a3 from the enterprise gap analysis.

-- apply after 001 through 010.

-- ============================================================

-- ---------------------------------------------------------------

-- alert rules (configurable threshold definitions)

-- ---------------------------------------------------------------

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

    isactive            boolean              not null constraint df_ar_active DEFAULT true,

    createdat           timestamptz   not null constraint df_ar_created default (clock_timestamp())

);

-- ---------------------------------------------------------------

-- alert events (fired instances)

-- ---------------------------------------------------------------

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

    forwardedtosiem     boolean              not null constraint df_ae_forwardedsiem DEFAULT false

);

create index if not exists ix_alertevents_status_firedat on dbo.alertevents(status, firedat desc);

create index if not exists ix_alertevents_ruleid_firedat on dbo.alertevents(ruleid, firedat desc);

-- ============================================================================

-- migration 012_payment_switch_enhancements.sql

-- ============================================================================

-- ============================================================

-- migration 012 — payment switch b1 enhancements

-- pre-auth tracking, stand-in profiles, distributed idempotency

-- apply after 001 through 011.

-- ============================================================

-- ---------------------------------------------------------------

-- pre-authorization records (iso 0100 / 0220 / 0420)

-- ---------------------------------------------------------------

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

create index if not exists ix_preauthrecords_rrn_source on dbo.preauthrecords(rrn, sourcenodeid, status);

create index if not exists ix_preauthrecords_expiresat on dbo.preauthrecords(expiresat) where status = 'Approved';

-- ---------------------------------------------------------------

-- stand-in processing profiles

-- ---------------------------------------------------------------

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

    isactive              boolean              not null constraint df_sip_active DEFAULT true,

    createdat             timestamptz   not null constraint df_sip_created default (clock_timestamp()),

    constraint ux_standinprofiles_bincode unique (binprefix, profilecode)

);

-- default global stand-in profile (empty bin prefix = catch-all)

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.standinprofiles WHERE binprefix = '' and profilecode = 'GLOBAL-DEFAULT') THEN
        insert into dbo.standinprofiles (profilecode, binprefix, floorlimitamount, currencycode, velocitycountlimit, velocitywindowseconds, eligibletransactiontypes, isactive)

values ('GLOBAL-DEFAULT', '', 10000.00, '566', 3, 86400, '00', false);
    END IF;
END
$$; -- disabled by default, operators enable per policy

-- ---------------------------------------------------------------

-- distributed idempotency keys

-- ---------------------------------------------------------------

create table if not exists dbo.idempotencykeys

(

    key         varchar(256)   not null constraint pk_idempotencykeys primary key,

    correlationid varchar(64)    not null,

    claimedat     timestamptz  not null constraint df_ik_claimedat default (clock_timestamp()),

    expiresat     timestamptz  not null

);

-- ttl-based cleanup: rows with expiresat in the past can be purged by a maintenance job

create index if not exists ix_idempotencykeys_expiresat on dbo.idempotencykeys(expiresat);

-- ============================================================================

-- migration 013_clearing_settlement_schema.sql

-- ============================================================================

-- ============================================================

-- migration 013 — clearing & settlement engine schema completion

-- fixes the clearing pipeline: adds iscleared / clearingbatchid /

-- settlementprofile to dbo.transactionlogs so the clearing engine

-- can identify uncleared transactions and mark them once batched.

-- also creates dbo.netsettlementpositions for the outbound

-- settlement engine net position calculation.

-- apply after 001 through 012.

-- ============================================================

-- ---------------------------------------------------------------

-- dbo.transactionlogs — clearing tracking columns

-- ---------------------------------------------------------------

-- settlementprofile: copied from the sink node at transaction time.

-- used by the clearing engine to group transactions per network

-- (visa_ng, mastercard_ng, verve_nibss, default, etc.)

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='transactionlogs' AND column_name='settlementprofile') THEN
        alter table dbo.transactionlogs

        add settlementprofile varchar(64) not null constraint df_tl_settlementprofile default ('');
    END IF;
END
$$;

-- iscleared: flipped to 1 when the clearing engine includes the

-- transaction in a clearingbatch. prevents double-clearing.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='transactionlogs' AND column_name='iscleared') THEN
        alter table dbo.transactionlogs

        add iscleared boolean not null constraint df_tl_iscleared DEFAULT false;
    END IF;
END
$$;

-- clearingbatchid: fk-style link to dbo.clearingbatches once cleared.

-- null until the transaction is included in a batch.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='dbo' AND table_name='transactionlogs' AND column_name='clearingbatchid') THEN
        alter table dbo.transactionlogs

        add clearingbatchid uuid null;
    END IF;
END
$$;

-- index to make getunclearedtransactionsasync efficient:

-- filters on iscleared = false, approved responsecode, mti, and business date

create index if not exists ix_tl_iscleared_profile_date

        on dbo.transactionlogs (iscleared, settlementprofile, responsecode, mti, createdat)

        where iscleared = false;

-- ---------------------------------------------------------------

-- dbo.netsettlementpositions — outbound settlement generation

-- ---------------------------------------------------------------

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

create index if not exists ix_nsp_date_status on dbo.netsettlementpositions(businessdate, status);

-- ============================================================================

-- migration 014_b2_eft_chargeback_dispute_reconciliation.sql

-- ============================================================================

-- ============================================================

-- migration 014 — b2: eft rails, chargeback, dispute, reconciliation

-- apply after 001 through 013.

-- ============================================================

-- extend efttransfers for return / mmid / mandate fields

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='isreturn') then
        alter table dbo.efttransfers add isreturn boolean not null constraint df_eft_isreturn DEFAULT false;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='returnreasoncode') then
        alter table dbo.efttransfers add returnreasoncode varchar(8) not null constraint df_eft_returncode default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='originaltransferid') then
        alter table dbo.efttransfers add originaltransferid uuid null;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='mmidnumber') then
        alter table dbo.efttransfers add mmidnumber varchar(7) not null constraint df_eft_mmid default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='mobilenumber') then
        alter table dbo.efttransfers add mobilenumber varchar(10) not null constraint df_eft_mobile default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='npcitransactionid') then
        alter table dbo.efttransfers add npcitransactionid varchar(64) not null constraint df_eft_npci default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='efttransfers' and column_name='directdebitmandateid') then
        alter table dbo.efttransfers add directdebitmandateid uuid null;
    end if;
end
$$;

-- neft batches

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

-- swift messages

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

-- ach files

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

-- direct debit mandates

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

-- chargeback reason codes

create table if not exists dbo.chargebackreasoncodes(

    id uuid not null constraint pk_chargebackreasoncodes primary key default gen_random_uuid(),

    network varchar(16) not null, code varchar(16) not null,

    category varchar(64) not null constraint df_crc_cat default(''),

    description varchar(200) not null constraint df_crc_desc default(''),

    initialchargebackdays int not null constraint df_crc_initial default(120),

    representmentdays int not null constraint df_crc_repr default(45),

    prearbitrationdays int not null constraint df_crc_prearb default(45),

    arbitrationdays int not null constraint df_crc_arb default(10),

    representmentallowed boolean not null constraint df_crc_reprallowed DEFAULT true,

    isactive boolean not null constraint df_crc_active DEFAULT true,

    constraint ux_chargebackreasoncodes unique(network, code));

-- chargeback cases

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

create index ix_chargebackcases_stage on dbo.chargebackcases(stage, network);

-- customer disputes

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

create table if not exists dbo.disputeevidence(

    id uuid not null constraint pk_disputeevidence primary key default gen_random_uuid(),

    disputeid uuid not null, evidencetype varchar(32) not null,

    description varchar(500) not null constraint df_de_desc default(''),

    documentvaultreference varchar(500) not null constraint df_de_vault default(''),

    submittedby varchar(128) not null constraint df_de_by default(''),

    submittedbyrole varchar(64) not null constraint df_de_role default(''),

    submittedat timestamptz not null constraint df_de_at default(clock_timestamp()));

create index ix_disputeevidence_dispute on dbo.disputeevidence(disputeid);

-- reconciliation runs

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

create index ix_reconciliationruns_date on dbo.reconciliationruns(businessdate, startedat desc);

-- reconciliation breaks

create table if not exists dbo.reconciliationbreaks(

    id uuid not null constraint pk_reconciliationbreaks primary key default gen_random_uuid(),

    reconciliationrunid uuid not null,

    breaktype varchar(64) not null, correlationid varchar(64) not null constraint df_rb_corr default(''),

    rrn varchar(12) not null constraint df_rb_rrn default(''),

    switchamount decimal(18,4) null, ledgeramount decimal(18,4) null,

    glamount decimal(18,4) null, clearingamount decimal(18,4) null,

    description text not null constraint df_rb_desc default(''),

    isresolved boolean not null constraint df_rb_resolved DEFAULT false,

    resolutionnotes varchar(500) not null constraint df_rb_resnotes default(''),

    detectedat timestamptz not null constraint df_rb_detected default(clock_timestamp()),

    resolvedat timestamptz null,

    constraint fk_reconciliationbreaks_run foreign key(reconciliationrunid) references dbo.reconciliationruns(id));

create index ix_reconciliationbreaks_run on dbo.reconciliationbreaks(reconciliationrunid, isresolved);

-- ============================================================================

-- migration 015_security_controls_schema.sql

-- ============================================================================

-- ============================================================

-- migration 015 — b3 security controls schema

-- totp enrollment, pci dss control results, hsm lifecycle,

-- dukpt key state, and key rotation audit tables.

-- apply after 001 through 014.

-- ============================================================

-- ---------------------------------------------------------------

-- totp / mfa enrollment (rfc 6238)

-- ---------------------------------------------------------------

create table if not exists dbo.totpenrollments

(

    id                    uuid not null constraint pk_totpenrollments primary key default gen_random_uuid(),

    userid                varchar(128)    not null,

    username              varchar(256)    not null,

    encryptedsecret       varchar(2000)   not null,     -- aes-256-gcm encrypted base32 seed

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

-- ---------------------------------------------------------------

-- pci dss v4.0 control results

-- ---------------------------------------------------------------

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

create index if not exists ix_pcicontrolresults_code_date on dbo.pcicontrolresults(requirementcode, evaluatedat desc);

-- ---------------------------------------------------------------

-- hsm partition snapshots

-- ---------------------------------------------------------------

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

-- ---------------------------------------------------------------

-- hsm key load events (pci dss req 3.6 — dual custodian audit)

-- ---------------------------------------------------------------

create table if not exists dbo.hsmkeyloadevents

(

    id                    uuid not null constraint pk_hsmkeyloadevents primary key default gen_random_uuid(),

    keyprofilecode        varchar(64)     not null,

    hsmpartitionname      varchar(64)     not null,

    eventtype             varchar(32)     not null,

    custodian1            varchar(128)    not null,     -- first custodian (dual-control)

    custodian2            varchar(128)    not null,     -- second custodian (dual-control)

    purpose               varchar(256)    not null constraint df_hkl_purpose default (''),

    keycheckvalue         varchar(16)     not null,

    encryptedkeyunderlmk  varchar(2000)   not null constraint df_hkl_enc default (''),

    correlationid         varchar(64)     not null,

    occurredat            timestamptz   not null constraint df_hkl_at default (clock_timestamp())

);

-- key load events are immutable — no update allowed (enforced by application layer)

create index if not exists ix_hsmkeyloadevents_profile_date on dbo.hsmkeyloadevents(keyprofilecode, occurredat desc);

-- ---------------------------------------------------------------

-- dukpt key state (ansi x9.24-1 terminal key tracking)

-- ---------------------------------------------------------------

create table if not exists dbo.dukptkeystates

(

    id                    uuid not null constraint pk_dukptkeystates primary key default gen_random_uuid(),

    terminalid            varchar(64)     not null,

    keyserialnumber       varchar(20)     not null,     -- 10-byte ksn as 20 hex chars

    basederivationkeyid   varchar(64)     not null,

    keytype               varchar(16)     not null constraint df_dks_type default ('Tdes2Key'),

    usage                 varchar(32)     not null constraint df_dks_usage default ('PinEncryption'),

    transactioncounter    bigint           not null constraint df_dks_counter default (0),

    exhaustedshiftcount   int              not null constraint df_dks_shifts default (0),

    isexhausted           boolean              not null constraint df_dks_exhausted DEFAULT false,

    lastkcv               varchar(16)     not null constraint df_dks_kcv default (''),

    createdat             timestamptz   not null constraint df_dks_created default (clock_timestamp()),

    lastusedat            timestamptz   null,

    exhaustedat           timestamptz   null,

    constraint ux_dukptkeystates_terminalid unique (terminalid)

);

-- ============================================================================

-- migration 016_financial_processing_b4.sql

-- ============================================================================

-- ============================================================

-- migration 016 — b4 financial processing: immutable ledger,

-- chart of accounts balances, gl periods (end-of-day)

-- apply after 001 through 015.

-- ============================================================

-- ---------------------------------------------------------------

-- gl journal entries — hash chain columns (b4: immutable ledger)

-- ---------------------------------------------------------------

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='businessdate') then
        alter table dbo.gljournalentries add businessdate date not null constraint df_gje_businessdate default (cast(clock_timestamp() as date));
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='chainsequence') then
        alter table dbo.gljournalentries add chainsequence bigint not null constraint df_gje_seq default (0);
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='previoushash') then
        alter table dbo.gljournalentries add previoushash varchar(64) not null constraint df_gje_prevhash default ('GENESIS');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='entryhash') then
        alter table dbo.gljournalentries add entryhash varchar(64) not null constraint df_gje_hash default ('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='reversesjournalid') then
        alter table dbo.gljournalentries add reversesjournalid uuid null;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='gljournalentries' and column_name='isvoided') then
        alter table dbo.gljournalentries add isvoided boolean not null constraint df_gje_voided DEFAULT false;
    end if;
end
$$;

-- index for chain verification by date and sequence

create unique index if not exists ix_gje_businessdate_seq on dbo.gljournalentries(chainsequence) where chainsequence > 0;

-- ---------------------------------------------------------------

-- gl account balances (running balances per account per day)

-- ---------------------------------------------------------------

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

create index if not exists ix_gab_date on dbo.glaccountbalances(balancedate, accountcode);

-- ---------------------------------------------------------------

-- gl periods (end-of-day accounting periods)

-- ---------------------------------------------------------------

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

-- ============================================================================

-- migration 017_performance_optimization.sql

-- ============================================================================

-- ============================================================

-- migration 017 — b5 performance: database partitioning,

-- archive strategy, and index optimization

-- apply after 001 through 016.

-- ============================================================

-- ---------------------------------------------------------------

-- 1. partition function — monthly partitioning on createdat

--    each month gets its own filegroup partition.

--    new months are added by alter partition function split range.

-- ---------------------------------------------------------------

-- postgresql does not have standalone partition-function objects; partitioning is defined on partitioned tables.
-- ---------------------------------------------------------------

-- 2. partition scheme — maps each partition to primary

--    in production: map older partitions to cheaper storage filegroups.

-- ---------------------------------------------------------------

-- postgresql does not have a separate partition-scheme object; storage mapping is defined by partitions/tablespaces.
-- ---------------------------------------------------------------

-- 3. transactionlogs archive table — receives aged-out rows

--    identical schema to dbo.transactionlogs so select union works.

--    in production: place on a compressed filegroup or azure blob storage.

-- ---------------------------------------------------------------

create table if not exists dbo.transactionlogsarchive

    (

        id                    uuid not null constraint pk_tla primary key,

        correlationid         varchar(64)     not null,

        mti                   varchar(4)      not null,

        sourcenodeid          varchar(64)     not null,

        sinknodeid            varchar(64)     not null,

        maskedpan             varchar(32)     not null,

        pantoken              varchar(64)     not null,

        panhash               varchar(128)    not null,

        stan                  varchar(12)     not null,

        rrn                   varchar(12)     not null,

        amount                decimal(18,4)    not null,

        currencycode          varchar(3)      not null,

        responsecode          varchar(4)      not null,

        latencymilliseconds   bigint           not null,

        routeused             varchar(64)     not null,

        schemeused            varchar(64)     not null,

        feeapplied            varchar(64)     not null,

        reversalstate         varchar(16)     not null,

        macvalidationstatus   varchar(32)     not null,

        settlementprofile     varchar(64)     not null,

        iscleared             boolean              not null,

        clearingbatchid       uuid null,

        createdat             timestamptz   not null,

        archivedat            timestamptz   not null default (clock_timestamp())

    );  -- page compression for archive efficiency

-- ---------------------------------------------------------------

-- 4. PostgreSQL procedure: archive and purge old transaction log rows
CREATE OR REPLACE PROCEDURE dbo.usp_archivetransactionlogs(
    retentiondays integer DEFAULT 90,
    archiveretentiondays integer DEFAULT 730,
    batchsize integer DEFAULT 10000,
    maxbatches integer DEFAULT 100
)
LANGUAGE plpgsql
AS $$
DECLARE
    cutoff timestamptz := clock_timestamp() - make_interval(days => retentiondays);
    archivecutoff timestamptz := clock_timestamp() - make_interval(days => archiveretentiondays);
    batch integer := 0;
    moved integer := 0;
    deleted_count integer := 0;
BEGIN
    WHILE batch < maxbatches LOOP
        WITH moved_rows AS (
            DELETE FROM dbo.transactionlogs
            WHERE id IN (
                SELECT id FROM dbo.transactionlogs
                WHERE createdat < cutoff
                ORDER BY createdat
                LIMIT batchsize
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, correlationid, mti, sourcenodeid, sinknodeid, maskedpan, pantoken, panhash,
                      stan, rrn, amount, currencycode, responsecode, latencymilliseconds, routeused,
                      schemeused, feeapplied, reversalstate, macvalidationstatus, settlementprofile,
                      iscleared, clearingbatchid, createdat
        )
        INSERT INTO dbo.transactionlogsarchive
        (id, correlationid, mti, sourcenodeid, sinknodeid, maskedpan, pantoken, panhash, stan, rrn, amount,
         currencycode, responsecode, latencymilliseconds, routeused, schemeused, feeapplied, reversalstate,
         macvalidationstatus, settlementprofile, iscleared, clearingbatchid, createdat)
        SELECT id, correlationid, mti, sourcenodeid, sinknodeid, maskedpan, pantoken, panhash, stan, rrn, amount,
               currencycode, responsecode, latencymilliseconds, routeused, schemeused, feeapplied, reversalstate,
               macvalidationstatus, settlementprofile, iscleared, clearingbatchid, createdat
        FROM moved_rows;

        GET DIAGNOSTICS moved = ROW_COUNT;
        EXIT WHEN moved = 0;
        batch := batch + 1;
    END LOOP;

    LOOP
        DELETE FROM dbo.transactionlogsarchive
        WHERE id IN (
            SELECT id FROM dbo.transactionlogsarchive
            WHERE createdat < archivecutoff
            ORDER BY createdat
            LIMIT batchsize
        );
        GET DIAGNOSTICS deleted_count = ROW_COUNT;
        EXIT WHEN deleted_count = 0;
    END LOOP;
END
$$;

-- 5. covering indexes for high-frequency switch queries

-- ---------------------------------------------------------------

-- 5a. existsduplicateasync — the most critical query path (called on every transaction)

create index if not exists ix_tl_duplicate_check

        on dbo.transactionlogs (sourcenodeid, stan, rrn, createdat)

        include (amount, responsecode)

        with (fillfactor = 90);

-- 5b. getunclearedtransactionsasync — clearing engine queries

create index if not exists ix_tl_clearing_query

        on dbo.transactionlogs (iscleared, settlementprofile, createdat)

        include (id, correlationid, amount, currencycode, responsecode, mti)
        with (fillfactor = 85)
        where iscleared = false;

-- 5c. getapprovedtransactionsbydateasync — reconciliation query

create index if not exists ix_tl_approved_bydate

        on dbo.transactionlogs (createdat, responsecode, mti)

        include (id, correlationid, sourcenodeid, amount, currencycode, settlementprofile, iscleared)
        with (fillfactor = 85)
        where responsecode in ('00','08','10','11') and mti in ('0200','0210');

-- ---------------------------------------------------------------

-- 6. gl journal entries — hash chain query index

-- ---------------------------------------------------------------

create index if not exists ix_gje_chainseq

        on dbo.gljournalentries (chainsequence, businessdate)

        include (journalnumber, debittotal, credittotal, previoushash, entryhash, postedat)

        with (fillfactor = 90);

-- ---------------------------------------------------------------

-- 7. performance configuration documentation (advisory)

-- ---------------------------------------------------------------

-- sql server instance-level settings (apply via dba/runbook, not t-sql migration):

--   max server memory (mb): set to 75% of total ram (leave os headroom)

--   max degree of parallelism: 1 for oltp workloads (no query parallelism on auth path)

--   cost threshold for parallelism: 50 (prevents short queries from going parallel)

--   optimize for ad hoc workloads: 1 (plan cache efficiency)

--   tempdb: one data file per logical cpu core (max 8)

-- SQL Server PRINT omitted: 'Migration 017 complete: partitioning, archive, and covering indexes applied.'

-- ============================================================================

-- migration 018_b7_compliance_schema.sql

-- ============================================================================

-- ============================================================

-- migration 018 — b7 compliance: aml reports, fraud baselines,

--                 owasp results, iso 27001, audit evidence

-- ============================================================

-- applied by: bankswitch v25 b7 compliance implementation

-- ─── aml regulatory reports ─────────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.cashtransactionreports') IS NULL THEN
create table dbo.cashtransactionreports(

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

    create index ix_cashtransactionreports_customer on dbo.cashtransactionreports(customernumber, reportdate desc);

    create index ix_cashtransactionreports_status   on dbo.cashtransactionreports(status) where status in ('Draft');
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.suspiciousactivityreports') IS NULL THEN
create table dbo.suspiciousactivityreports(

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

    create index ix_suspiciousactivityreports_customer on dbo.suspiciousactivityreports(customernumber, generatedat desc);

    create index ix_suspiciousactivityreports_status   on dbo.suspiciousactivityreports(status) where status in ('Draft', 'UnderReview');
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.amlfeedsnapshots') IS NULL THEN
create table dbo.amlfeedsnapshots(

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

    create index ix_amlfeedsnapshots_source on dbo.amlfeedsnapshots(source, fetchedat desc);
    END IF;
END
$$;

-- ─── fraud — behavioral baselines ────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.cardbehavioralbaselines') IS NULL THEN
create table dbo.cardbehavioralbaselines(

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
END
$$;

-- ─── owasp asvs results ──────────────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.owaspcontrolresults') IS NULL THEN
create table dbo.owaspcontrolresults(

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

    create index ix_owaspcontrolresults_status      on dbo.owaspcontrolresults(status, evaluatedat desc);

    create index ix_owaspcontrolresults_requirement on dbo.owaspcontrolresults(requirementid, evaluatedat desc);
    END IF;
END
$$;

-- ─── iso 27001:2022 risk register ─────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.iso27001riskentries') IS NULL THEN
create table dbo.iso27001riskentries(

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

    create index ix_iso27001riskentries_score on dbo.iso27001riskentries(likelihood, impact desc);
    END IF;
END
$$;

-- ─── iso 27001 statement of applicability ─────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.iso27001controlevaluations') IS NULL THEN
create table dbo.iso27001controlevaluations(

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

    create index ix_iso27001controlevaluations_status on dbo.iso27001controlevaluations(status, isapplicable);
    END IF;
END
$$;

-- ─── audit evidence packages ─────────────────────────────────

DO $$
BEGIN
    IF to_regclass('dbo.auditevidencepackages') IS NULL THEN
create table dbo.auditevidencepackages(

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

    create index ix_auditevidencepackages_period on dbo.auditevidencepackages(periodfrom, periodto desc);
    END IF;
END
$$;

-- SQL Server PRINT omitted: 'Migration 018: B7 Compliance tables created successfully.'

-- ============================================================================

-- migration 019_advanced_routing_criteria.sql

-- ============================================================================

do $$
begin
    if to_regclass('dbo.ux_routes_binprefix_active') is not null then
        drop index if exists ux_routes_binprefix_active;
    end if;
end
$$;

/\* v26 - advanced tier-1 routing criteria for eft switch

   adds routing by country, mcc, currency, device, interchange, card range, institution,

   product, network and account number while keeping legacy bin routing compatible.

*/

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='priority') then
        alter table dbo.routes add priority int not null constraint df_routes_priority default(0);
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='countrycodes') then
        alter table dbo.routes add countrycodes text not null constraint df_routes_countrycodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='merchantcategorycodes') then
        alter table dbo.routes add merchantcategorycodes text not null constraint df_routes_mcc default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='currencycodes') then
        alter table dbo.routes add currencycodes text not null constraint df_routes_currencycodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='devicecodes') then
        alter table dbo.routes add devicecodes text not null constraint df_routes_devicecodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='interchangecodes') then
        alter table dbo.routes add interchangecodes text not null constraint df_routes_interchangecodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='cardrangeprefixes') then
        alter table dbo.routes add cardrangeprefixes text not null constraint df_routes_cardrangeprefixes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='institutioncodes') then
        alter table dbo.routes add institutioncodes text not null constraint df_routes_institutioncodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='productcodes') then
        alter table dbo.routes add productcodes text not null constraint df_routes_productcodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='networkcodes') then
        alter table dbo.routes add networkcodes text not null constraint df_routes_networkcodes default('');
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='routes' and column_name='accountranges') then
        alter table dbo.routes add accountranges text not null constraint df_routes_accountranges default('');
    end if;
end
$$;

create index if not exists ix_routes_advancedlookup on dbo.routes(isactive, priority desc, binprefix);

-- ============================================================================

-- migration 020_debit_card_production_lifecycle.sql

-- ============================================================================

-- v27: enterprise debit card production lifecycle

-- covers full production order workflow, embossing files, pin mailer files,

-- personalization bureau integration, instant branch card stock, virtual debit card

-- audit trail, and hotlist propagation to card networks.

create table dbo.debitcardproductionorders (

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

create index ix_debitcardproductionorders_status on dbo.debitcardproductionorders(status);

create index ix_debitcardproductionorders_customernumber on dbo.debitcardproductionorders(customernumber);

create index ix_debitcardproductionorders_cardid on dbo.debitcardproductionorders(cardid);

create table dbo.debitcardbranchstockitems (

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

create index ix_debitcardbranchstock_branchproductstatus on dbo.debitcardbranchstockitems(branchcode, productcode, status);

create table dbo.debitcardbureaufiles (

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

create index ix_debitcardbureaufiles_typestatus on dbo.debitcardbureaufiles(filetype, status);

create table dbo.hotlistpropagationevents (

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

create index ix_hotlistpropagationevents_cardnetwork on dbo.hotlistpropagationevents(cardid, network);

create index ix_hotlistpropagationevents_status on dbo.hotlistpropagationevents(status);

-- ============================================================================

-- migration 021_network_settlement_clearing_gl.sql

-- ============================================================================

-- v28 network settlement, clearing and gl certification schema

-- adds visa/mastercard/rupay/npci settlement evidence, interchange fee rule engine,

-- and rbi/npci audit controls.

do $$
begin
    if to_regclass('dbo.interchangefeerule') is null then
    create table dbo.interchangefeerule (

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

    create unique index ux_interchangefeerule_rulecode on dbo.interchangefeerule(rulecode);

    create index ix_interchangefeerule_lookup on dbo.interchangefeerule(network, effectivefrom, effectiveto, productcode, channelcode, merchantcategorycode, countrycode, currencycode, transactiontypecode, isactive);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.networksettlementrun') is null then
    create table dbo.networksettlementrun (

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

    create unique index ux_networksettlementrun_batch on dbo.networksettlementrun(clearingbatchid);

    create index ix_networksettlementrun_networkcycle on dbo.networksettlementrun(network, settlementcycle, certificationstatus);
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='ClearingRecord' and column_name='FeeAmount') then
    alter table dbo.clearingrecord add feeamount decimal(18,4) not null constraint df_clearingrecord_feeamount default 0;
    end if;
end
$$;

do $$
begin
    if not exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='ClearingBatch' and column_name='InstitutionCode') then
    alter table dbo.clearingbatch add institutioncode varchar(32) not null constraint df_clearingbatch_institutioncode default '';
    end if;
end
$$;

merge into dbo.interchangefeerule as target

using (values

    (gen_random_uuid(), 'VISA-DEBIT-DOM-ATM', 'Visa', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4500, 0.0000, 25.0000, 'AcquirerReceives', date '2020-01-01', null, true, 90),

    (gen_random_uuid(), 'VISA-DEBIT-POS', 'Visa', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.3500, 0.0000, 20.0000, 'IssuerReceives', date '2020-01-01', null, true, 80),

    (gen_random_uuid(), 'MC-DEBIT-DOM-ATM', 'Mastercard', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4500, 0.0000, 25.0000, 'AcquirerReceives', date '2020-01-01', null, true, 90),

    (gen_random_uuid(), 'MC-DEBIT-POS', 'Mastercard', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.3500, 0.0000, 20.0000, 'IssuerReceives', date '2020-01-01', null, true, 80),

    (gen_random_uuid(), 'RUPAY-POS', 'Rupay', 'DEBIT', 'POS', '*', 'IN', '356', 'PURCHASE', 0.0000, 0.2500, 0.0000, 15.0000, 'IssuerReceives', date '2020-01-01', null, true, 80),

    (gen_random_uuid(), 'NPCI-NFS-ATM', 'NpciNfs', 'DEBIT', 'ATM', '*', 'IN', '356', 'WITHDRAWAL', 0.0000, 0.4000, 0.0000, 20.0000, 'AcquirerReceives', date '2020-01-01', null, true, 80)

) as source(id, rulecode, network, productcode, channelcode, merchantcategorycode, countrycode, currencycode, transactiontypecode, flatfee, percentfee, minimumfee, maximumfee, direction, effectivefrom, effectiveto, isactive, priority)

on target.rulecode = source.rulecode

when not matched then

    insert (id, rulecode, network, productcode, channelcode, merchantcategorycode, countrycode, currencycode, transactiontypecode, flatfee, percentfee, minimumfee, maximumfee, direction, effectivefrom, effectiveto, isactive, priority)

    values (source.id, source.rulecode, source.network, source.productcode, source.channelcode, source.merchantcategorycode, source.countrycode, source.currencycode, source.transactiontypecode, source.flatfee, source.percentfee, source.minimumfee, source.maximumfee, source.direction, source.effectivefrom, source.effectiveto, source.isactive, source.priority);

-- ============================================================================

-- migration 022_advanced_reconciliation_odr_udir.sql

-- ============================================================================

-- v29 advanced reconciliation, atm evidence, c3r and odr/udir

-- sql server oriented schema. provides persistence for network reconciliation files,

-- atm ej/cctv/pinhole evidence, c3r cash reconciliation and rbi odr / npci udir cases.

do $$
begin
    if to_regclass('dbo.networkreconciliationfile') is null then
    create table dbo.networkreconciliationfile (

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

    create index ix_networkreconciliationfile_datenetwork on dbo.networkreconciliationfile(businessdate, network, format);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.networkreconciliationrecord') is null then
    create table dbo.networkreconciliationrecord (

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

    create index ix_networkreconrecord_match on dbo.networkreconciliationrecord(businessdate, network, rrn, stan, arn, networkreference);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.atmevidenceitem') is null then
    create table dbo.atmevidenceitem (

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

    create index ix_atmevidenceitem_lookup on dbo.atmevidenceitem(businessdate, terminalid, rrn, stan, evidencetype);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.c3ratmreconciliationrun') is null then
    create table dbo.c3ratmreconciliationrun (

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

    create unique index ux_c3ratmreconciliationrun_ref on dbo.c3ratmreconciliationrun(runreference);

    create index ix_c3ratmreconciliationrun_dateterminal on dbo.c3ratmreconciliationrun(businessdate, terminalid, status);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.odrudircase') is null then
    create table dbo.odrudircase (

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

    create unique index ux_odrudircase_casereference on dbo.odrudircase(casereference);

    create index ix_odrudircase_status on dbo.odrudircase(network, status, rrn);
    end if;
end
$$;

-- ============================================================================

-- migration 023_network_dispute_exchange_odr_udir.sql

-- ============================================================================

-- v30 network dispute exchange and external odr/udir integration

-- bank-grade audit tables for visa vrol, mastercard mcom/file express,

-- npci rupay/nfs udir, rbi odr and npci udir exchange files/api payloads.

do $$
begin
    if to_regclass('dbo.network_dispute_exchange_files') is null then
create table dbo.network_dispute_exchange_files (

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

    created_at timestamptz not null,

    transmitted_at timestamptz null,

    acknowledged_at timestamptz null,

    created_by varchar(128) not null

);
    end if;
end
$$;

create index if not exists ix_network_dispute_exchange_files_date_network

    on dbo.network_dispute_exchange_files (business_date, network, status);

do $$
begin
    if to_regclass('dbo.network_dispute_exchange_records') is null then
create table dbo.network_dispute_exchange_records (

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
    end if;
end
$$;

create index if not exists ix_network_dispute_exchange_records_file

    on dbo.network_dispute_exchange_records (file_id);

create index if not exists ix_network_dispute_exchange_records_lookup

    on dbo.network_dispute_exchange_records (rrn, stan, network_case_id, udir_transaction_id);

-- in production, replace the simulated gateways with certified adapters:

-- visa vrol / visa resolve online, mastercard mcom/file express, npci udir sftp/api, rbi odr api.

-- ============================================================================

-- migration 024_atm_driving_protocols.sql

-- ============================================================================

-- v31: atm driving protocols, screen distribution, lod, admin card cash workflow,

-- c3r, ej/cctv/pinhole evidence, voice guidance and multilingual runtime

create table dbo.atm_terminal_profile(

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

create table dbo.atm_vendor_certification_artifact(

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

create table dbo.atm_screen_definition(

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

create table dbo.atm_lod_file_artifact(

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

create table dbo.atm_screen_distribution_job(

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

create table dbo.atm_admin_cash_operation(

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

create table dbo.atm_c3r_reconciliation_run(

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

create table dbo.atm_evidence_artifact(

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

create table dbo.atm_voice_prompt_pack(

    id                    varchar(128) primary key,

    language_code         varchar(16) not null,

    description           varchar(512) not null,

    prompt_file_uris_json text not null,

    sha256_manifest       varchar(64) not null,

    updated_at_utc        timestamptz not null

);

create index ix_atm_terminal_profile_vendor_protocol on dbo.atm_terminal_profile(vendor, protocol);

create index ix_atm_c3r_terminal_date on dbo.atm_c3r_reconciliation_run(terminal_id, business_date);

create index ix_atm_evidence_terminal_type on dbo.atm_evidence_artifact(terminal_id, evidence_type);

create index ix_atm_screen_distribution_status on dbo.atm_screen_distribution_job(status, scheduled_at_utc);

-- ============================================================================

-- migration 025_pos_mpos_ecommerce_terminal_driving.sql

-- ============================================================================

-- v32 pos / mpos / e-commerce terminal driving

-- v44.6 canonicalized: creates the same production sql server objects consumed by sqlposterminaldrivingrepository.

-- complements v33 posacquiringproduction persistence and removes the sql-provider dependency on in-memory v32 records.

do $$
begin
    if to_regclass('dbo.posterminalprofiles') is null then
    create table dbo.posterminalprofiles (

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

    create index ix_posterminalprofiles_merchant on dbo.posterminalprofiles(merchantid, status);

    create index ix_posterminalprofiles_vendorprotocol on dbo.posterminalprofiles(vendor, protocol, status);

    END IF;
END
$$;

do $$
begin
    if to_regclass('dbo.posmposenrollments') is null then
    create table dbo.posmposenrollments (

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

    create index ix_posmposenrollments_terminal on dbo.posmposenrollments(terminalid, status);

    create index ix_posmposenrollments_merchant on dbo.posmposenrollments(merchantid, status);

    END IF;
END
$$;

do $$
begin
    if to_regclass('dbo.poskeydownloadcertifications') is null then
    create table dbo.poskeydownloadcertifications (

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

    create index ix_poskeydownloadcertifications_terminal on dbo.poskeydownloadcertifications(terminalid, scheme, status);

end

DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadsessions') IS NULL THEN
    create table dbo.poskeydownloadsessions (

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

    create index ix_poskeydownloadsessions_terminal on dbo.poskeydownloadsessions(terminalid, scheme, status, requestedat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') IS NULL THEN
    create table dbo.poscontactlesstransactionflows (

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

    create index ix_poscontactlesstransactionflows_merchant on dbo.poscontactlesstransactionflows(merchantid, currencycode, createdat);

    create index ix_poscontactlesstransactionflows_terminal on dbo.poscontactlesstransactionflows(terminalid, createdat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') IS NULL THEN
    create table dbo.postipadjustments (

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

    create unique index ux_postipadjustments_original on dbo.postipadjustments(originaltransactionid);

    create index ix_postipadjustments_merchant on dbo.postipadjustments(merchantid, createdat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') IS NULL THEN
    create table dbo.poscashatposacquiring (

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

    create index ix_poscashatposacquiring_merchant on dbo.poscashatposacquiring(merchantid, currencycode, createdat);

    create index ix_poscashatposacquiring_terminal on dbo.poscashatposacquiring(terminalid, createdat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementbatches') IS NULL THEN
    create table dbo.posmerchantsettlementbatches (

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

    create index ix_posmerchantsettlementbatches_merchant on dbo.posmerchantsettlementbatches(merchantid, settlementdate, status);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') IS NULL THEN
    create table dbo.posdevicecommands (

        id uuid not null primary key,

        terminalid varchar(64) not null,

        command varchar(128) not null,

        parametersjson text not null,

        status varchar(32) not null,

        createdat timestamptz not null,

        appliedat timestamptz null,

        correlationid varchar(128) not null

    );

    create index ix_posdevicecommands_terminal on dbo.posdevicecommands(terminalid, status, createdat);
    END IF;
END
$$;
-- ============================================================================

-- migration 026_pos_acquiring_production_core.sql

-- ============================================================================

-- v33 pos acquiring production core

-- v44.6 canonicalized: v32 terminal-driving records are owned by migration 025; this migration adds only v33 acquiring-specific persistence.

DO $$
BEGIN
    IF to_regclass('dbo.posmerchants') IS NULL THEN
    create table dbo.posmerchants (

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

    create index ix_posmerchants_mcc_status on dbo.posmerchants(mcc, status);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posmdrrules') IS NULL THEN
    create table dbo.posmdrrules (

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

    create index ix_posmdrrules_lookup on dbo.posmdrrules(network, merchantid, mcc, scheme, productcode, currencycode, flowtype, isactive, effectivefrom, effectiveto);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posterminallifecycle') IS NULL THEN
    create table dbo.posterminallifecycle (

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
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') IS NULL THEN
    create table dbo.poscommandqueue (

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

    create index ix_poscommandqueue_pending on dbo.poscommandqueue(status, terminalid, notbefore, expiresat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlesstxns') IS NULL THEN
    create table dbo.posofflinecontactlesstxns (

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

    create index ix_posofflinecontactlesstxns_clearing on dbo.posofflinecontactlesstxns(merchantid, currencycode, terminalapprovedat, status);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posofflinecontactlessbatches') IS NULL THEN
    create table dbo.posofflinecontactlessbatches (

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
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poskeyceremonies') IS NULL THEN
    create table dbo.poskeyceremonies (

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
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posemvcertificationevidence') IS NULL THEN
    create table dbo.posemvcertificationevidence (

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
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementpostings') IS NULL THEN
    create table dbo.posmerchantsettlementpostings (

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
END
$$;
-- ============================================================================

-- migration 027_acquiring_certification_simulator.sql

-- ============================================================================

-- v34 card network acquiring certification simulator

-- certification lab tables for visa/mastercard/rupay/npci acquiring simulators, test packs,

-- validation results, emv/contactless checklist and evidence reports.

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_cases') IS NULL THEN
create table dbo.acquiring_cert_test_cases (

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

    created_at timestamp not null

);
    end if;
    END IF;
END
$$;
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_packs') is null then
create table dbo.acquiring_cert_packs (

    id uuid primary key,

    pack_code varchar(80) not null unique,

    scheme varchar(32) not null,

    terminal_model varchar(120) not null,

    pos_protocol varchar(80) not null,

    version varchar(40) not null,

    test_case_ids_json text not null,

    status varchar(32) not null,

    created_at timestamp not null,

    updated_at timestamp not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_runs') is null then
create table dbo.acquiring_cert_runs (

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

    started_at timestamp not null,

    completed_at timestamp null,

    actor varchar(120) not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_test_results') is null then
create table dbo.acquiring_cert_test_results (

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

    executed_at timestamp not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_message_validation_results') is null then
create table dbo.acquiring_message_validation_results (

    id uuid primary key,

    scheme varchar(32) not null,

    flow_kind varchar(64) not null,

    mti varchar(4) not null,

    is_valid boolean not null,

    findings_json text not null,

    message_hash varchar(128) not null,

    validated_at timestamp not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_host_response_validation_results') is null then
create table dbo.acquiring_host_response_validation_results (

    id uuid primary key,

    scheme varchar(32) not null,

    flow_kind varchar(64) not null,

    is_valid boolean not null,

    findings_json text not null,

    validated_at timestamp not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.emv_contactless_cert_checklist') is null then
create table dbo.emv_contactless_cert_checklist (

    id uuid primary key,

    scheme varchar(32) not null,

    terminal_model varchar(120) not null,

    kernel_type varchar(80) not null,

    requirement_code varchar(80) not null,

    requirement_text text not null,

    status varchar(32) not null,

    evidence_reference varchar(250) not null,

    remarks text not null,

    updated_at timestamp not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_flow_results') is null then
create table dbo.acquiring_cert_flow_results (

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

    executed_at timestamp not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_evidence_reports') is null then
create table dbo.acquiring_cert_evidence_reports (

    id uuid primary key,

    run_id uuid not null,

    report_format varchar(20) not null,

    report_body text not null,

    report_hash varchar(128) not null,

    generated_at timestamp not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

create index if not exists ix_acquiring_cert_test_cases_scheme on dbo.acquiring_cert_test_cases (scheme, category, flow_kind);

create index if not exists ix_acquiring_cert_runs_pack on dbo.acquiring_cert_runs (pack_id, status);

create index if not exists ix_acquiring_cert_results_run on dbo.acquiring_cert_test_results (run_id, status);

create index if not exists ix_emv_contactless_checklist_terminal on dbo.emv_contactless_cert_checklist (scheme, terminal_model, requirement_code);

-- ============================================================================

-- migration 028_acquiring_certification_lab_extensions.sql

-- ============================================================================

-- v35: acquiring certification lab extensions

-- adds schema for scenario designer, masked production replay, fuzz testing,

-- regression suites, endurance profiles, fault injection and plugin registry.

do $$
begin
    if to_regclass('dbo.acquiring_cert_scenarios') is null then
create table dbo.acquiring_cert_scenarios (

    id uuid primary key,

    scenario_code varchar(80) not null unique,

    name varchar(200) not null,

    scheme varchar(30) not null,

    flow_kind varchar(40) not null,

    description text not null default '',

    steps_json text not null,

    is_active boolean not null default true,

    version varchar(40) not null default '1.0',

    created_at timestamptz not null,

    updated_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_replay_runs') is null then
create table dbo.acquiring_cert_replay_runs (

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

    executed_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_fuzz_runs') is null then
create table dbo.acquiring_cert_fuzz_runs (

    id uuid primary key,

    scheme varchar(30) not null,

    flow_kind varchar(40) not null,

    case_count integer not null,

    passed_cases integer not null,

    failed_cases integer not null,

    critical_findings integer not null,

    findings_json text not null,

    evidence_hash char(64) not null,

    executed_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_regression_runs') is null then
create table dbo.acquiring_cert_regression_runs (

    id uuid primary key,

    suite_code varchar(80) not null,

    baseline_version varchar(80) not null,

    candidate_version varchar(80) not null,

    total_packs integer not null,

    passed_packs integer not null,

    failed_packs integer not null,

    regressions_json text not null,

    evidence_hash char(64) not null,

    executed_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_endurance_runs') is null then
create table dbo.acquiring_cert_endurance_runs (

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

    executed_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_fault_injection_runs') is null then
create table dbo.acquiring_cert_fault_injection_runs (

    id uuid primary key,

    scheme varchar(30) not null,

    fault_kind varchar(40) not null,

    duration_seconds integer not null,

    failure_percentage numeric(6,2) not null,

    impacted_messages integer not null,

    recovered_messages integer not null,

    recovery_validated boolean not null,

    evidence_hash char(64) not null,

    executed_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.acquiring_cert_plugins') is null then
create table dbo.acquiring_cert_plugins (

    id uuid primary key,

    plugin_code varchar(80) not null unique,

    name varchar(200) not null,

    plugin_kind varchar(50) not null,

    scheme varchar(30) null,

    version varchar(40) not null,

    entry_point varchar(500) not null,

    capabilities_json text not null,

    is_enabled boolean not null default true,

    registered_at timestamptz not null,

    correlation_id varchar(80) not null

);
    end if;
end
$$;

create index if not exists ix_acquiring_cert_scenarios_scheme on dbo.acquiring_cert_scenarios (scheme, flow_kind);

create index if not exists ix_acquiring_cert_replay_scheme on dbo.acquiring_cert_replay_runs (scheme, executed_at desc);

create index if not exists ix_acquiring_cert_plugins_kind on dbo.acquiring_cert_plugins (plugin_kind, is_enabled);

-- ============================================================================

-- migration 029_issuer_certification_full_lab.sql

-- ============================================================================

-- v36: issuer certification simulator & host validation lab

-- canonical sql server ddl for issuer-side certification test packs, runs, evidence and validation.

do $$
begin
    if to_regclass('dbo.issuer_cert_test_cases') is null then
    create table dbo.issuer_cert_test_cases (

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

        requires_hsm boolean not null constraint df_issuer_cert_test_cases_hsm DEFAULT false,

        requires_cbs boolean not null constraint df_issuer_cert_test_cases_cbs DEFAULT false,

        is_mandatory boolean not null constraint df_issuer_cert_test_cases_mandatory DEFAULT true,

        created_at timestamptz not null,

        correlation_id varchar(128) not null,

        constraint ck_issuer_cert_test_cases_request_json check ((request_fields_json)::jsonb is not null),

        constraint ck_issuer_cert_test_cases_expected_json check ((expected_host_fields_json)::jsonb is not null)

    );
    end if;
end
$$;

create unique index if not exists ux_issuer_cert_test_cases_code_scheme on dbo.issuer_cert_test_cases(test_case_code, scheme);

create index if not exists ix_issuer_cert_test_cases_scheme_category on dbo.issuer_cert_test_cases(scheme, category, flow_kind);

do $$
begin
    if to_regclass('dbo.issuer_cert_packs') is null then
    create table dbo.issuer_cert_packs (

        id uuid not null constraint pk_issuer_cert_packs primary key,

        pack_code varchar(80) not null,

        scheme varchar(32) not null,

        host_profile varchar(128) not null,

        card_product varchar(128) not null,

        version varchar(40) not null,

        test_case_ids_json text not null,

        status varchar(32) not null,

        created_at timestamptz not null,

        updated_at timestamptz not null,

        correlation_id varchar(128) not null,

        constraint ck_issuer_cert_packs_test_case_ids_json check ((test_case_ids_json)::jsonb is not null)

    );
    end if;
end
$$;

create unique index if not exists ux_issuer_cert_packs_code_scheme on dbo.issuer_cert_packs(pack_code, scheme);

do $$
begin
    if to_regclass('dbo.issuer_cert_runs') is null then
    create table dbo.issuer_cert_runs (

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

        started_at timestamptz not null,

        completed_at timestamptz null,

        actor varchar(128) not null,

        correlation_id varchar(128) not null,

        constraint ck_issuer_cert_runs_counts check (total_tests >= 0 and passed_tests >= 0 and failed_tests >= 0 and blocked_tests >= 0)

    );
    end if;
end
$$;

create index if not exists ix_issuer_cert_runs_pack_started on dbo.issuer_cert_runs(pack_id, started_at desc);

do $$
begin
    if to_regclass('dbo.issuer_cert_run_results') is null then
    create table dbo.issuer_cert_run_results (

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

        executed_at timestamptz not null,

        constraint ck_issuer_cert_run_results_findings_json check ((findings_json)::jsonb is not null)

    );
    end if;
end
$$;

create index if not exists ix_issuer_cert_run_results_run on dbo.issuer_cert_run_results(run_id, executed_at);

do $$
begin
    if to_regclass('dbo.issuer_cert_evidence_reports') is null then
    create table dbo.issuer_cert_evidence_reports (

        id uuid not null constraint pk_issuer_cert_evidence_reports primary key,

        run_id uuid not null,

        report_format varchar(20) not null,

        report_body text not null,

        report_hash char(64) not null,

        generated_at timestamptz not null,

        correlation_id varchar(128) not null

    );
    end if;
end
$$;

create index if not exists ix_issuer_cert_evidence_reports_run on dbo.issuer_cert_evidence_reports(run_id, generated_at desc);

do $$
begin
    if to_regclass('dbo.issuer_cert_validation_evidence') is null then
    create table dbo.issuer_cert_validation_evidence (

        id uuid not null constraint pk_issuer_cert_validation_evidence primary key,

        scheme varchar(32) not null,

        flow_kind varchar(64) not null,

        validation_type varchar(64) not null,

        is_valid boolean not null,

        findings_json text not null,

        evidence_hash char(64) not null,

        validated_at timestamptz not null,

        correlation_id varchar(128) not null,

        constraint ck_issuer_cert_validation_findings_json check ((findings_json)::jsonb is not null)

    );
    end if;
end
$$;

create index if not exists ix_issuer_cert_validation_evidence_scheme_type on dbo.issuer_cert_validation_evidence(scheme, validation_type, validated_at desc);

-- ============================================================================

-- migration 030_real_hsm_key_management_production_core.sql

-- ============================================================================

-- v37 real hsm & key management production core

-- adds production-grade hsm inventory, key lifecycle, key ceremony, tr-31/tr-34, dukpt/ukpt, and audit evidence tables.

do $$
begin
    if to_regclass('dbo.hsm_connector_profiles') is null then
    create table dbo.hsm_connector_profiles (

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
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.hsm_key_inventory') is null then
    create table dbo.hsm_key_inventory (

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

    create index ix_hsm_key_inventory_type_status on dbo.hsm_key_inventory(key_type, status);

    create index ix_hsm_key_inventory_network on dbo.hsm_key_inventory(network, institution_id);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.hsm_key_ceremonies') is null then
    create table dbo.hsm_key_ceremonies (

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

    create index ix_hsm_key_ceremonies_status on dbo.hsm_key_ceremonies(status, created_at);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.hsm_tr34_remote_key_load_sessions') is null then
    create table dbo.hsm_tr34_remote_key_load_sessions (

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

    create index ix_hsm_tr34_terminal_status on dbo.hsm_tr34_remote_key_load_sessions(terminal_id, status);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.hsm_dukpt_device_state') is null then
    create table dbo.hsm_dukpt_device_state (

        terminal_id varchar(40) not null primary key,

        bdk_alias varchar(120) not null,

        ksn varchar(40) not null,

        counter bigint not null,

        current_kcv varchar(16) not null,

        last_derived_at timestamptz not null,

        status varchar(40) not null

    );
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.hsm_tamper_proof_audit_log') is null then
    create table dbo.hsm_tamper_proof_audit_log (

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

    create index ix_hsm_audit_target_time on dbo.hsm_tamper_proof_audit_log(target, created_at desc);

    create index ix_hsm_audit_action_time on dbo.hsm_tamper_proof_audit_log(action, created_at desc);
    end if;
end
$$;

-- ============================================================================

-- migration 031_real_card_network_host_integration_core.sql

-- ============================================================================

-- v38 real card network host integration core

-- visa base i/ii, mastercard mip/ipm/file express, rupay/npci host integration boundaries.

do $$
begin
    if to_regclass('dbo.network_host_profiles') is null then
create table dbo.network_host_profiles (

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

    created_at timestamptz not null,

    last_sign_on_at timestamptz null,

    last_echo_at timestamptz null,

    settings_json text not null default '{}'

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.iso8583_network_profiles') is null then
create table dbo.iso8583_network_profiles (

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

    created_at timestamptz not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.network_message_journal') is null then
create table dbo.network_message_journal (

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

    created_at timestamptz not null,

    direction varchar(8) not null,

    message_hash varchar(128) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.network_saf_replay_queue') is null then
create table dbo.network_saf_replay_queue (

    replay_id uuid primary key,

    host_id uuid not null references dbo.network_host_profiles(host_id),

    scheme varchar(40) not null,

    flow varchar(40) not null,

    original_reference varchar(128) not null,

    status varchar(30) not null,

    attempt_count int not null default 0,

    created_at timestamptz not null,

    last_attempt_at timestamptz null,

    last_response_code varchar(16) null,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.network_settlement_calendars') is null then
create table dbo.network_settlement_calendars (

    calendar_id uuid primary key,

    scheme varchar(40) not null,

    institution_id varchar(64) not null,

    currency_code varchar(3) not null,

    business_date varchar(10) not null,

    cutover_time_local varchar(16) not null,

    status varchar(30) not null,

    created_at timestamptz not null,

    cutover_at timestamptz null,

    notes text null

);
    end if;
end
$$;

create index if not exists ix_network_host_profiles_scheme_status on dbo.network_host_profiles (scheme, status);

create index if not exists ix_iso8583_network_profiles_scheme_flow on dbo.iso8583_network_profiles (scheme, flow, mti);

create index if not exists ix_network_message_journal_host_created on dbo.network_message_journal (host_id, created_at desc);

create index if not exists ix_network_saf_replay_queue_host_status on dbo.network_saf_replay_queue (host_id, status);

create index if not exists ix_network_settlement_calendars_scheme_date on dbo.network_settlement_calendars (scheme, business_date);

-- ============================================================================

-- migration 032_core_banking_enterprise_integration_core.sql

-- ============================================================================

-- v39 core banking & enterprise integration production core

-- adds cbs/finacle, esb/api manager, payment hub/iph, acs/3ds, frm, dwh/bi and notification integration records.

do $$
begin
    if to_regclass('dbo.enterprise_connector_profiles') is null then
create table dbo.enterprise_connector_profiles (

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

    created_at timestamptz not null default current_timestamp

);
    end if;
end
$$;

create index if not exists ix_enterprise_connector_system_status on dbo.enterprise_connector_profiles (system_code, status);

do $$
begin
    if to_regclass('dbo.cbs_posting_audit') is null then
create table dbo.cbs_posting_audit (

    posting_id uuid primary key default gen_random_uuid(),

    transaction_reference varchar(128) not null,

    cbs_reference varchar(128) not null,

    account_number varchar(64) null,

    posting_type varchar(32) null,

    amount decimal(18,2) null,

    currency_code char(3) null,

    response_code varchar(8) not null,

    audit_hash char(64) not null,

    posted_at timestamptz not null default current_timestamp

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.card_account_linkage_sync') is null then
create table dbo.card_account_linkage_sync (

    linkage_id uuid primary key,

    customer_id varchar(64) not null,

    card_masked varchar(32) not null,

    account_number varchar(64) not null,

    product_code varchar(64) not null,

    is_primary boolean not null,

    status varchar(32) not null,

    audit_hash char(64) not null,

    created_at timestamptz not null default current_timestamp

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.enterprise_external_events') is null then
create table dbo.enterprise_external_events (

    event_id uuid primary key default gen_random_uuid(),

    event_type varchar(64) not null,

    external_reference varchar(128) null,

    status varchar(32) not null,

    payload_hash char(64) not null,

    created_at timestamptz not null default current_timestamp

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.dwh_bi_feeds') is null then
create table dbo.dwh_bi_feeds (

    feed_id uuid primary key,

    feed_type varchar(64) not null,

    business_date date not null,

    output_format varchar(16) not null,

    status varchar(32) not null,

    file_name varchar(255) not null,

    sha256_hash char(64) not null,

    created_at timestamptz not null default current_timestamp

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.enterprise_notifications') is null then
create table dbo.enterprise_notifications (

    notification_id uuid primary key,

    channel varchar(32) not null,

    recipient_masked varchar(128) not null,

    template_code varchar(64) null,

    status varchar(32) not null,

    provider_reference varchar(128) null,

    audit_hash char(64) not null,

    created_at timestamptz not null default current_timestamp

);
    end if;
end
$$;

-- ============================================================================

-- migration 033_operations_command_center_sla_automation.sql

-- ============================================================================

-- v40 operations command center & sla automation core

-- provides db-backed structures for 24x7 operations dashboard, incidents,

-- sla breach detection, escalation, technical decline analytics, rca, dr drill evidence,

-- capacity/performance telemetry and regulatory uptime reporting.

do $$
begin
    if to_regclass('dbo.ops_health_snapshots') is null then
create table dbo.ops_health_snapshots (

    snapshot_id uuid primary key,

    component_type varchar(40) not null,

    component_code varchar(80) not null,

    status varchar(30) not null,

    status_message varchar(500),

    availability_percent numeric(7,4) not null,

    current_tps integer not null default 0,

    technical_declines integer not null default 0,

    captured_at timestamptz not null,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

create index if not exists ix_ops_health_component on dbo.ops_health_snapshots (component_type, component_code, captured_at desc);

do $$
begin
    if to_regclass('dbo.ops_incidents') is null then
create table dbo.ops_incidents (

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

    opened_at timestamptz not null,

    acknowledged_at timestamptz null,

    resolved_at timestamptz null,

    closed_at timestamptz null,

    root_cause text,

    corrective_action text,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

create index if not exists ix_ops_incidents_status on dbo.ops_incidents (status, severity, opened_at desc);

do $$
begin
    if to_regclass('dbo.ops_sla_policies') is null then
create table dbo.ops_sla_policies (

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

    created_at timestamptz not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ops_sla_evaluations') is null then
create table dbo.ops_sla_evaluations (

    evaluation_id uuid primary key,

    policy_id uuid not null references dbo.ops_sla_policies(policy_id),

    policy_code varchar(80) not null,

    status varchar(30) not null,

    observed_value numeric(18,6) not null,

    threshold_value numeric(18,6) not null,

    unit varchar(30) not null,

    message varchar(500) not null,

    incident_id uuid null references dbo.ops_incidents(incident_id),

    evaluated_at timestamptz not null,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

create index if not exists ix_ops_sla_eval_status on dbo.ops_sla_evaluations (status, evaluated_at desc);

do $$
begin
    if to_regclass('dbo.ops_escalation_rules') is null then
create table dbo.ops_escalation_rules (

    rule_id uuid primary key,

    severity varchar(30) not null,

    from_level varchar(30) not null,

    to_level varchar(30) not null,

    escalate_after_minutes integer not null,

    notify_group varchar(120) not null,

    enabled boolean not null default true

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ops_technical_declines') is null then
create table dbo.ops_technical_declines (

    decline_id uuid primary key,

    transaction_reference varchar(80) not null,

    category varchar(40) not null,

    component_code varchar(80) not null,

    response_code varchar(20) not null,

    reason varchar(500) not null,

    channel varchar(40) not null,

    amount numeric(18,2) not null,

    currency_code varchar(3) not null,

    occurred_at timestamptz not null,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

create index if not exists ix_ops_declines_time on dbo.ops_technical_declines (occurred_at desc, category, channel);

do $$
begin
    if to_regclass('dbo.ops_rca_cases') is null then
create table dbo.ops_rca_cases (

    rca_id uuid primary key,

    incident_id uuid not null references dbo.ops_incidents(incident_id),

    interim_report text,

    final_report text,

    root_cause text not null,

    corrective_action text not null,

    preventive_action text not null,

    prepared_by varchar(120) not null,

    due_at timestamptz not null,

    submitted_at timestamptz null,

    status varchar(30) not null,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ops_dr_drills') is null then
create table dbo.ops_dr_drills (

    drill_id uuid primary key,

    drill_code varchar(80) not null unique,

    status varchar(30) not null,

    planned_at timestamptz not null,

    started_at timestamptz null,

    completed_at timestamptz null,

    observed_rpo_minutes integer not null default 0,

    observed_rto_minutes integer not null default 0,

    evidence_file varchar(500),

    report text,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ops_capacity_metrics') is null then
create table dbo.ops_capacity_metrics (

    metric_id uuid primary key,

    metric_type varchar(40) not null,

    component_code varchar(80) not null,

    value numeric(18,6) not null,

    unit varchar(30) not null,

    captured_at timestamptz not null,

    audit_hash varchar(128) not null

);
    end if;
end
$$;

create index if not exists ix_ops_capacity_component on dbo.ops_capacity_metrics (component_code, metric_type, captured_at desc);

do $$
begin
    if to_regclass('dbo.ops_regulatory_uptime_reports') is null then
create table dbo.ops_regulatory_uptime_reports (

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

    generated_at timestamptz not null

);
    end if;
end
$$;

-- ============================================================================

-- migration 034_regulatory_compliance_audit_evidence_pack.sql

-- ============================================================================

-- v41 regulatory compliance, audit & evidence pack core

-- adds tables for rbi dpsc/pci/iso/npci/visa/mastercard evidence packs,

-- audit observations, vapt/appsec findings, secure sdlc evidence,

-- maker-checker evidence, access reviews, retention policies and generated packs.

create table dbo.compliance_controls(

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

create table dbo.compliance_evidence_items(

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

    constraint fk_compliance_evidence_control foreign key(control_id) references compliance_controls(control_id)

);

create index ix_compliance_evidence_framework_control on dbo.compliance_evidence_items(framework, control_code);

create table dbo.audit_observations(

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

create index ix_audit_observations_status_due on dbo.audit_observations(status, due_date);

create table dbo.security_findings(

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

create index ix_security_findings_status_target on dbo.security_findings(status, target_date);

create table dbo.secure_sdlc_artifacts(

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

create index ix_secure_sdlc_release on dbo.secure_sdlc_artifacts(release_version, artifact_type);

create table dbo.maker_checker_evidence(

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

create index ix_maker_checker_module on dbo.maker_checker_evidence(module, checker_at desc);

create table dbo.access_review_campaigns(

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

create table dbo.data_retention_policies(

    policy_id uuid not null primary key,

    data_set varchar(160) not null unique,

    retention_days int not null,

    default_action varchar(40) not null,

    legal_hold boolean not null default false,

    owner_role varchar(80) not null,

    created_at timestamptz not null,

    audit_hash char(64) not null

);

create table dbo.retention_executions(

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

    constraint fk_retention_execution_policy foreign key(policy_id) references data_retention_policies(policy_id)

);

create table dbo.compliance_packs(

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

create index ix_compliance_packs_framework_date on dbo.compliance_packs(framework, to_date desc);

-- ============================================================================

-- migration 035_realtime_fraud_risk_aml_production_core.sql

-- ============================================================================

-- v42 real-time fraud risk & aml production core

-- adds db-backed structures for rules, watchlists, aml screening, risk cases and risk model profiles.

do $$
begin
    if to_regclass('dbo.risk_rules') is null then
create table dbo.risk_rules (

    rule_id uuid primary key,

    rule_code varchar(64) not null unique,

    category varchar(40) not null,

    description text not null,

    expression text not null,

    score int not null check (score between 0 and 100),

    action varchar(40) not null,

    enabled boolean not null default true,

    priority int not null default 100,

    updated_at timestamptz not null,

    updated_by varchar(128) not null,

    audit_hash char(64) not null

);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.risk_list_entries') is null then
create table dbo.risk_list_entries (

    entry_id uuid primary key,

    list_type varchar(40) not null,

    entity_type varchar(40) not null,

    entity_value varchar(256) not null,

    reason text not null,

    source varchar(128) not null,

    effective_from date not null,

    effective_to date null,

    enabled boolean not null default true,

    created_at timestamptz not null,

    created_by varchar(128) not null,

    audit_hash char(64) not null

);
    end if;
end
$$;

create index if not exists ix_risk_list_lookup on dbo.risk_list_entries (list_type, entity_type, entity_value) where enabled = true;

do $$
begin
    if to_regclass('dbo.risk_evaluations') is null then
create table dbo.risk_evaluations (

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

    evaluated_at timestamptz not null,

    audit_hash char(64) not null

);
    end if;
end
$$;

create index if not exists ix_risk_eval_date_decision on dbo.risk_evaluations (evaluated_at, decision);

create index if not exists ix_risk_eval_corr on dbo.risk_evaluations (correlation_id);

do $$
begin
    if to_regclass('dbo.aml_screenings') is null then
create table dbo.aml_screenings (

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

    screened_at timestamptz not null,

    audit_hash char(64) not null

);
    end if;
end
$$;

create index if not exists ix_aml_status_date on dbo.aml_screenings (status, screened_at);

do $$
begin
    if to_regclass('dbo.risk_cases') is null then
create table dbo.risk_cases (

    case_id uuid primary key,

    case_number varchar(40) not null unique,

    correlation_id varchar(128) not null,

    status varchar(40) not null,

    decision varchar(40) not null,

    score int not null,

    title varchar(256) not null,

    details text not null,

    assigned_to varchar(128) not null,

    created_at timestamptz not null,

    closed_at timestamptz null,

    audit_hash char(64) not null

);
    end if;
end
$$;

create index if not exists ix_risk_case_status on dbo.risk_cases (status, created_at);

do $$
begin
    if to_regclass('dbo.risk_model_profiles') is null then
create table dbo.risk_model_profiles (

    model_id uuid primary key,

    model_code varchar(64) not null unique,

    model_name varchar(128) not null,

    status varchar(40) not null,

    version varchar(40) not null,

    feature_set_json text not null,

    review_threshold int not null,

    decline_threshold int not null,

    updated_at timestamptz not null,

    updated_by varchar(128) not null,

    audit_hash char(64) not null

);
    end if;
end
$$;

-- ============================================================================

-- migration 036_persistent_pos_terminal_driving_repository.sql

-- ============================================================================

-- v43 persistent pos terminal driving repository

-- sql server migration for v32 pos/mpos/ecommerce terminal-driving records.

-- complements v33 posacquiringproduction persistence and removes the sql-provider dependency on in-memory v32 records.

do $$
begin
    if to_regclass('dbo.posterminalprofiles') is null then
    create table dbo.posterminalprofiles (

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

    create index ix_posterminalprofiles_merchant on dbo.posterminalprofiles(merchantid, status);

    create index ix_posterminalprofiles_vendorprotocol on dbo.posterminalprofiles(vendor, protocol, status);

end

DO $$
BEGIN
    IF to_regclass('dbo.posmposenrollments') IS NULL THEN
    create table dbo.posmposenrollments (

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

    create index ix_posmposenrollments_terminal on dbo.posmposenrollments(terminalid, status);

    create index ix_posmposenrollments_merchant on dbo.posmposenrollments(merchantid, status);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadcertifications') IS NULL THEN
    create table dbo.poskeydownloadcertifications (

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

    create index ix_poskeydownloadcertifications_terminal on dbo.poskeydownloadcertifications(terminalid, scheme, status);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poskeydownloadsessions') IS NULL THEN
    create table dbo.poskeydownloadsessions (

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

    create index ix_poskeydownloadsessions_terminal on dbo.poskeydownloadsessions(terminalid, scheme, status, requestedat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poscontactlesstransactionflows') IS NULL THEN
    create table dbo.poscontactlesstransactionflows (

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

    create index ix_poscontactlesstransactionflows_merchant on dbo.poscontactlesstransactionflows(merchantid, currencycode, createdat);

    create index ix_poscontactlesstransactionflows_terminal on dbo.poscontactlesstransactionflows(terminalid, createdat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.postipadjustments') IS NULL THEN
    create table dbo.postipadjustments (

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

    create unique index ux_postipadjustments_original on dbo.postipadjustments(originaltransactionid);

    create index ix_postipadjustments_merchant on dbo.postipadjustments(merchantid, createdat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.poscashatposacquiring') IS NULL THEN
    create table dbo.poscashatposacquiring (

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

    create index ix_poscashatposacquiring_merchant on dbo.poscashatposacquiring(merchantid, currencycode, createdat);

    create index ix_poscashatposacquiring_terminal on dbo.poscashatposacquiring(terminalid, createdat);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posmerchantsettlementbatches') IS NULL THEN
    create table dbo.posmerchantsettlementbatches (

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

    create index ix_posmerchantsettlementbatches_merchant on dbo.posmerchantsettlementbatches(merchantid, settlementdate, status);
    END IF;
END
$$;
DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') IS NULL THEN
    create table dbo.posdevicecommands (

        id uuid not null primary key,

        terminalid varchar(64) not null,

        command varchar(128) not null,

        parametersjson text not null,

        status varchar(32) not null,

        createdat timestamptz not null,

        appliedat timestamptz null,

        correlationid varchar(128) not null

    );

    create index ix_posdevicecommands_terminal on dbo.posdevicecommands(terminalid, status, createdat);
    END IF;
END
$$;
-- ============================================================================

-- migration 037_repository_persistence_data_integrity_hardening.sql

-- ============================================================================



-- transaction batch boundary omitted; outer PostgreSQL transaction is used;

-- kyc tables were introduced in migration 010. v44 augments them with durable json snapshots,

-- audit timestamps, bytea and additional lookup indexes without dropping existing data.

IF to_regclass('dbo.kycdocuments') IS NULL THEN
        RAISE EXCEPTION 'dbo.KycDocuments is missing. Apply migration 010_card_lifecycle.sql before v44.';
    END IF;
END
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='kycdocuments' and column_name='payloadjson') then 1 else null end) is null then
        alter table dbo.kycdocuments add payloadjson text null;
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='kycdocuments' and column_name='createdat') then 1 else null end) is null then
        alter table dbo.kycdocuments add createdat timestamptz not null constraint df_kycdocuments_createdat default clock_timestamp();
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='kycdocuments' and column_name='updatedat') then 1 else null end) is null then
        alter table dbo.kycdocuments add updatedat timestamptz not null constraint df_kycdocuments_updatedat default clock_timestamp();
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='kycdocuments' and column_name='rowversion') then 1 else null end) is null then
        alter table dbo.kycdocuments add rowversion bytea;
    end if;
end
$$;

update dbo.kycdocuments

set payloadjson = (

    select id,customerid,customernumber,documenttype,documentnumber,issuingauthority,issuingcountrycode,

           issuedate,expirydate,status,documentvaultreference,providerverificationid,rejectionreason,

           submittedby,reviewedby,submittedat,reviewedat

    for json path, without_array_wrapper

)

where payloadjson is null or not ((payloadjson)::jsonb is not null);

alter table dbo.kycdocuments alter column payloadjson type text;
alter table dbo.kycdocuments alter column payloadjson set not null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname='ck_kycdocuments_payloadjson') then
        alter table dbo.kycdocuments add constraint ck_kycdocuments_payloadjson check ((payloadjson)::jsonb is not null);
    end if;
end
$$;

create index if not exists ix_kycdocuments_customernumber_status on dbo.kycdocuments(customernumber,status,submittedat desc);

create index if not exists ix_kycdocuments_documentnumber on dbo.kycdocuments(documentnumber);

do $$
begin
    if to_regclass('dbo.authorizationholds') is null then
        raise exception 'dbo.AuthorizationHolds is missing. Apply migration 010_card_lifecycle.sql before v44.';
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='authorizationholds' and column_name='payloadjson') then 1 else null end) is null then
        alter table dbo.authorizationholds add payloadjson text null;
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='authorizationholds' and column_name='createdat') then 1 else null end) is null then
        alter table dbo.authorizationholds add createdat timestamptz not null constraint df_authorizationholds_createdat default clock_timestamp();
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='authorizationholds' and column_name='updatedat') then 1 else null end) is null then
        alter table dbo.authorizationholds add updatedat timestamptz not null constraint df_authorizationholds_updatedat default clock_timestamp();
    end if;
end
$$;

do $$
begin
    if (select case when exists (select 1 from information_schema.columns where table_schema='dbo' and table_name='authorizationholds' and column_name='rowversion') then 1 else null end) is null then
        alter table dbo.authorizationholds add rowversion bytea;
    end if;
end
$$;

update dbo.authorizationholds

set payloadjson = (

    select id,walletaccountid,cardid,correlationid,stan,rrn,authorizationcode,holdamount,currencycode,

           merchantid,merchantname,terminalid,status,placedat,expiresat,releasedat,capturedamount,capturecorrelationid

    for json path, without_array_wrapper

)

where payloadjson is null or not ((payloadjson)::jsonb is not null);

alter table dbo.authorizationholds alter column payloadjson type text;
alter table dbo.authorizationholds alter column payloadjson set not null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname='ck_authorizationholds_payloadjson') then
        alter table dbo.authorizationholds add constraint ck_authorizationholds_payloadjson check ((payloadjson)::jsonb is not null);
    end if;
end
$$;

create index if not exists ix_authorizationholds_expiry on dbo.authorizationholds(status,expiresat) include(walletaccountid,rrn);

-- explicit postgresql definitions replacing the sql server metadata cursor.
do $$
declare
    t text;
begin
    foreach t in array array['acquiringcertificationstore','acquiringcertificationlabstore','issuercertificationstore'] loop
        execute format('CREATE TABLE IF NOT EXISTS dbo.%I (id uuid NOT NULL CONSTRAINT %I PRIMARY KEY, recordtype varchar(64) NOT NULL, scheme varchar(64) NULL, parentid uuid NULL, secondarykey varchar(160) NULL, status varchar(64) NULL, occurredat timestamptz NOT NULL, payloadjson text NOT NULL, createdat timestamptz NOT NULL DEFAULT clock_timestamp(), updatedat timestamptz NOT NULL DEFAULT clock_timestamp(), rowversion bytea NOT NULL, CONSTRAINT %I CHECK (payloadjson::jsonb IS NOT NULL))', t, 'pk_'||t, 'ck_'||t||'_payloadjson');
        execute format('CREATE INDEX IF NOT EXISTS %I ON dbo.%I (recordtype, scheme, occurredat DESC)', 'ix_'||t||'_type_scheme_time', t);
        execute format('CREATE INDEX IF NOT EXISTS %I ON dbo.%I (recordtype, parentid, occurredat DESC)', 'ix_'||t||'_type_parent_time', t);
        execute format('CREATE INDEX IF NOT EXISTS %I ON dbo.%I (recordtype, secondarykey) INCLUDE (status, occurredat)', 'ix_'||t||'_type_key', t);
    end loop;
end $$;

-- v33/v43 pos acquiring repository is already sql-backed. strengthen common lookup indexes idempotently.

do $$
begin
    if to_regclass('dbo.posmerchants') is not null and to_regclass('dbo.ix_posmerchants_status_mcc') is null then
        create index ix_posmerchants_status_mcc on dbo.posmerchants(status,mcc) include(settlementcurrencycode,settlementcycle);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.poscommandqueue') is not null and to_regclass('dbo.ix_poscommandqueue_dispatch') is null then
        create index ix_poscommandqueue_dispatch on dbo.poscommandqueue(status,notbefore,expiresat,attemptcount) include(terminalid,command,maxattempts);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.posmerchantsettlementpostings') is not null and to_regclass('dbo.ix_posmerchantsettlementpostings_merchantdate') is null then
        create index ix_posmerchantsettlementpostings_merchantdate on dbo.posmerchantsettlementpostings(merchantid,settlementdate desc,status);
    end if;
end
$$;

-- transaction batch boundary omitted;

-- ============================================================================

-- migration 038_enterprise_configuration_control_plane.sql

-- ============================================================================

/\* bankswitch v44.1 — enterprise configuration control plane

   persistent, versioned, maker-checker governed configuration management.

*/



-- transaction batch boundary omitted; outer PostgreSQL transaction is used;

create table if not exists dbo.configurationdomains(

    code varchar(64) not null constraint pk_configurationdomains primary key,

    name varchar(128) not null,

    description varchar(512) not null constraint df_configdomains_description default(''),

    displayorder int not null constraint df_configdomains_order default(0),

    enabled boolean not null constraint df_configdomains_enabled DEFAULT true,

    createdat timestamptz not null constraint df_configdomains_created default(clock_timestamp())

);

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

    issecret boolean not null constraint df_configdefinitions_issecret DEFAULT false,

    issensitive boolean not null constraint df_configdefinitions_issensitive DEFAULT false,

    productionlocked boolean not null constraint df_configdefinitions_prodlocked DEFAULT false,

    validationpattern varchar(1000) null,

    validationexpression varchar(2000) null,

    displayorder int not null constraint df_configdefinitions_order default(0),

    enabled boolean not null constraint df_configdefinitions_enabled DEFAULT true,

    createdat timestamptz not null constraint df_configdefinitions_created default(clock_timestamp()),

    constraint fk_configurationdefinitions_domain foreign key(domaincode) references dbo.configurationdomains(code),

    constraint uq_configurationdefinitions unique(domaincode,key),

    constraint ck_configurationdefinitions_allowedjson check(allowedvaluesjson is null or (allowedvaluesjson)::jsonb is not null)

);

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

create unique index ux_configurationvalues_active on dbo.configurationvalues(definitionid,environment,institutionscope) where effectiveto is null;

create index ix_configurationvalues_scope on dbo.configurationvalues(environment,institutionscope,version desc);

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

create index ix_configurationchangerequests_state on dbo.configurationchangerequests(state,createdat desc);

create index ix_configurationchangerequests_scope on dbo.configurationchangerequests(environment,institutionscope,createdat desc);

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

create index ix_configurationchangeitems_request on dbo.configurationchangeitems(changerequestid);

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

create unique index ux_configurationhistory_version on dbo.configurationhistory(version);

create index ix_configurationhistory_scope on dbo.configurationhistory(environment,institutionscope,changedat desc);

create index ix_configurationhistory_key on dbo.configurationhistory(domaincode,key,changedat desc);

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

    constraint ck_configurationsnapshots_json check((payloadjson)::jsonb is not null)

);

create index ix_configurationsnapshots_scope on dbo.configurationsnapshots(environment,institutionscope,createdat desc);

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

create index ix_configurationdeployments_request on dbo.configurationdeployments(changerequestid,startedat desc);

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

create index ix_certificateinventory_expiry on dbo.certificateinventory(environment,validto);

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

-- 34 enterprise settings domains.

merge into dbo.configurationdomains as t

using (values

('general','General / System','Institution, environment and business-date settings',10),

('api','API & Backend','API runtime, timeout and retry settings',20),

('realtime','Realtime / SignalR','Realtime event and reconnect settings',30),

('database','Database & Repositories','Repository provider and database performance settings',40),

('transactions','Transaction Processing','Authorization, reversal, SAF and idempotency settings',50),

('iso8583','ISO 8583','Message profile and field configuration',60),

('routing','Routing','Advanced switch routing controls',70),

('network-hosts','Network Hosts','Visa, Mastercard, RuPay and NPCI host controls',80),

('atm','ATM','ATM protocol and runtime controls',90),

('pos','POS / mPOS','POS and terminal-driving controls',100),

('merchant','Merchant Acquiring','Merchant, MDR and settlement controls',110),

('cards','Card Management','Card product and lifecycle controls',120),

('hsm','HSM & Key Management','HSM profile, key policy and rotation controls',130),

('fraud','Fraud / Risk','Fraud rules and scoring thresholds',140),

('aml','AML','AML, sanctions and PEP screening controls',150),

('settlement','Settlement','Network and merchant settlement controls',160),

('gl','GL / Accounting','GL mappings and financial posting controls',170),

('reconciliation','Reconciliation','Matching, tolerance and exception controls',180),

('disputes','Disputes / Chargeback','Dispute SLA and evidence controls',190),

('cbs','CBS / Finacle','Core banking integration controls',200),

('enterprise','Enterprise Integrations','ESB, ACS, FRM, DWH and notification controls',210),

('certification','Certification Lab','Simulator, test and evidence controls',220),

('security','Security','Identity, MFA, session and TLS controls',230),

('rbac','Users & RBAC','Role and permission control-plane settings',240),

('maker-checker','Maker / Checker','Four-eyes governance controls',250),

('audit','Audit','Audit retention and SIEM forwarding controls',260),

('compliance','Compliance','PCI, RBI, NPCI and ISO evidence controls',270),

('monitoring','Monitoring & SLA','Health and SLA threshold controls',280),

('alerts','Alerts','Alert channel and escalation controls',290),

('observability','Logging & Observability','Logging, metrics and tracing controls',300),

('dr','Disaster Recovery','RPO, RTO, failover and DR controls',310),

('retention','Data Retention','Archival and purge controls',320),

('feature-flags','Feature Flags','Controlled functional rollout settings',330),

('diagnostics','Diagnostics','Runtime health and diagnostics controls',340)

) s(code,name,description,displayorder)

on t.code=s.code

when matched then update set name=s.name,description=s.description,displayorder=s.displayorder,enabled = true

when not matched then insert(code,name,description,displayorder,enabled) values(s.code,s.name,s.description,s.displayorder,true);

-- core definitions. more domain-specific definitions can be registered without schema changes.

drop table if exists tmp_configurationdefinitions;

create temp table tmp_configurationdefinitions (domaincode varchar(64),key varchar(160),displayname varchar(160),description varchar(1024),valuetype varchar(32),defaultvalue text,allowed text,minval decimal(28,8),maxval decimal(28,8),sensitivity varchar(32),reloadpolicy varchar(32),requiresapproval boolean,issecret boolean,issensitive boolean,productionlocked boolean,displayorder int);

insert into tmp_configurationdefinitions values

('general', 'Environment', 'Environment', 'DEV/SIT/UAT/PREPROD/PROD/DR', 'Enum', 'DEV', '"DEV","SIT","UAT","PREPROD","PROD","DR"', null, null, 'Critical', 'ClusterRestart', 1, false, true, true, 10),

('general', 'InstitutionCode', 'Institution Code', 'Authoritative institution identifier', 'String', 'BANK', null, null, null, 'Critical', 'ServiceRestart', 1, false, true, true, 20),

('general', 'BaseCurrency', 'Base Currency', 'ISO numeric/alphabetic base currency', 'String', 'INR', null, null, null, 'Sensitive', 'HotReload', 1, false, false, false, 30),

('general', 'TimeZone', 'Time Zone', 'Business timezone', 'String', 'Asia/Kolkata', null, null, null, 'Operational', 'HotReload', 0, false, false, false, 40),

('api', 'RequestTimeoutSeconds', 'Request Timeout', 'Backend request timeout in seconds', 'Integer', '30', null, 1, 300, 'Operational', 'HotReload', 0, false, false, false, 10),

('api', 'RetryCount', 'Retry Count', 'Transient retry count', 'Integer', '3', null, 0, 10, 'Operational', 'HotReload', 0, false, false, false, 20),

('realtime', 'SignalREnabled', 'SignalR Enabled', 'Enable realtime operational events', 'Boolean', 'true', null, null, null, 'Operational', 'HotReload', 0, false, false, false, 10),

('realtime', 'HeartbeatSeconds', 'Heartbeat Interval', 'Realtime heartbeat interval', 'Integer', '15', null, 5, 300, 'Operational', 'HotReload', 0, false, false, false, 20),

('database', 'RepositoryProvider', 'Repository Provider', 'Persistence provider', 'Enum', 'SqlServer', '"SqlServer","InMemory"', null, null, 'Critical', 'ClusterRestart', 1, false, true, true, 10),

('database', 'CommandTimeoutSeconds', 'SQL Command Timeout', 'SQL command timeout', 'Integer', '30', null, 1, 300, 'Sensitive', 'ServiceRestart', 1, false, false, false, 20),

('database', 'MaxPoolSize', 'SQL Max Pool Size', 'Maximum ADO.NET connection pool size', 'Integer', '200', null, 10, 2000, 'Sensitive', 'ServiceRestart', 1, false, false, false, 30),

('transactions', 'AuthorizationTimeoutSeconds', 'Authorization Timeout', 'Authorization processing timeout', 'Integer', '30', null, 1, 120, 'Critical', 'HotReload', 1, false, false, false, 10),

('transactions', 'DuplicateWindowSeconds', 'Duplicate Detection Window', 'Duplicate transaction protection window', 'Integer', '300', null, 1, 86400, 'Critical', 'HotReload', 1, false, false, false, 20),

('transactions', 'SafEnabled', 'SAF Enabled', 'Enable store-and-forward', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', 1, false, false, false, 30),

('transactions', 'AutoReversalEnabled', 'Auto Reversal', 'Enable automatic timeout reversal', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', 1, false, false, false, 40),

('iso8583', 'DefaultProfile', 'Default ISO Profile', 'Default network message profile', 'String', 'ISO8583-1987', null, null, null, 'Critical', 'ServiceRestart', 1, false, false, false, 10),

('iso8583', 'MacField', 'MAC Field', 'ISO MAC field number', 'Enum', '64', '"64","128"', null, null, 'Critical', 'ServiceRestart', 1, false, false, false, 20),

('routing', 'FallbackEnabled', 'Routing Fallback', 'Enable fallback route selection', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', 1, false, false, false, 10),

('network-hosts', 'TlsEnabled', 'Network TLS', 'Require TLS for network hosts', 'Boolean', 'true', null, null, null, 'Critical', 'ConnectionRestart', 1, false, true, true, 10),

('network-hosts', 'EchoIntervalSeconds', 'Echo Interval', 'Network management echo interval', 'Integer', '30', null, 5, 600, 'Sensitive', 'HotReload', 1, false, false, false, 20),

('atm', 'HeartbeatSeconds', 'ATM Heartbeat', 'ATM heartbeat interval', 'Integer', '30', null, 5, 600, 'Operational', 'HotReload', 0, false, false, false, 10),

('pos', 'OfflineFloorLimit', 'Offline Floor Limit', 'Maximum offline contactless floor limit', 'Decimal', '0', null, 0, 1000000, 'Critical', 'HotReload', 1, false, false, false, 10),

('merchant', 'DefaultSettlementCycle', 'Settlement Cycle', 'Default merchant settlement cycle', 'Enum', 'T+1', '"T+0","T+1","T+2"', null, null, 'Sensitive', 'HotReload', 1, false, false, false, 10),

('cards', 'PinRetryLimit', 'PIN Retry Limit', 'Maximum PIN retry attempts', 'Integer', '3', null, 1, 10, 'Critical', 'HotReload', 1, false, false, false, 10),

('hsm', 'HsmMode', 'HSM Mode', 'HSM operating mode', 'Enum', 'Http', '"Http","Thales","Atalla","Futurex","Mock","BypassForDevelopmentOnly"', null, null, 'Critical', 'ServiceRestart', 1, false, true, true, 10),

('hsm', 'KeyRotationDays', 'Key Rotation Days', 'Default key rotation cadence', 'Integer', '90', null, 1, 365, 'Critical', 'HotReload', 1, false, true, false, 20),

('fraud', 'CriticalScoreThreshold', 'Critical Risk Score', 'Score at which transaction is critical', 'Integer', '90', null, 1, 100, 'Critical', 'HotReload', 1, false, false, false, 10),

('aml', 'RescreenHours', 'AML Rescreen Interval', 'Customer AML rescreen interval', 'Integer', '24', null, 1, 720, 'Sensitive', 'HotReload', 1, false, false, false, 10),

('settlement', 'CutoffTimeUtc', 'Settlement Cutoff', 'Daily settlement cut-off UTC', 'String', '22:00:00', null, null, null, 'Critical', 'HotReload', 1, false, false, false, 10),

('gl', 'BalanceTolerance', 'Balance Tolerance', 'Maximum GL imbalance tolerance', 'Decimal', '0', null, 0, 1000, 'Critical', 'HotReload', 1, false, false, false, 10),

('reconciliation', 'AmountTolerance', 'Amount Tolerance', 'Automatic reconciliation amount tolerance', 'Decimal', '0', null, 0, 1000, 'Critical', 'HotReload', 1, false, false, false, 10),

('disputes', 'AutoEscalationEnabled', 'Dispute Escalation', 'Enable automatic dispute escalation', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', 1, false, false, false, 10),

('cbs', 'Endpoint', 'CBS Endpoint', 'Primary CBS integration endpoint', 'Uri', 'https\://cbs.invalid', null, null, null, 'Critical', 'ConnectionRestart', 1, false, true, false, 10),

('enterprise', 'CircuitBreakerThreshold', 'Circuit Breaker Threshold', 'Enterprise integration failure threshold', 'Integer', '5', null, 1, 100, 'Sensitive', 'HotReload', 1, false, false, false, 10),

('certification', 'SimulatorMode', 'Simulator Mode', 'Allow scheme simulator execution', 'Boolean', 'true', null, null, null, 'Sensitive', 'ServiceRestart', 1, false, false, true, 10),

('security', 'AuthenticationMode', 'Authentication Mode', 'Administrative identity mode', 'Enum', 'OIDC', '"OIDC","AzureAD","Keycloak","Cookie","Disabled"', null, null, 'Critical', 'ServiceRestart', 1, false, true, true, 10),

('security', 'MfaRequired', 'MFA Required', 'Require MFA for privileged administration', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', 1, false, true, true, 20),

('security', 'TlsMinimumVersion', 'TLS Minimum Version', 'Minimum TLS version', 'Enum', '1.2', '"1.2","1.3"', null, null, 'Critical', 'ServiceRestart', 1, false, true, true, 30),

('security', 'AdminClientSecretRef', 'Admin Client Secret Reference', 'Vault/HSM reference onl); never a secret value', 'SecretReference', null, null, null, null, 'Critical', 'ServiceRestart', 1, true, true, false, 40),

('maker-checker', 'CriticalApprovalRequired', 'Critical Approval', 'Force maker-checker for critical settings', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', 1, false, true, true, 10),

('audit', 'RetentionDays', 'Audit Retention', 'Audit retention days', 'Integer', '2555', null, 365, 3650, 'Critical', 'HotReload', 1, false, false, false, 10),

('monitoring', 'LatencyCriticalMs', 'Critical Latency', 'Critical transaction latency threshold', 'Integer', '1000', null, 10, 60000, 'Sensitive', 'HotReload', 1, false, false, false, 10),

('alerts', 'CertificateExpiryDays', 'Certificate Warning', 'Certificate-expiry warning threshold', 'Integer', '30', null, 1, 365, 'Sensitive', 'HotReload', 1, false, false, false, 10),

('observability', 'LogLevel', 'Log Level', 'Minimum structured log level', 'Enum', 'Information', '"Debug","Information","Warning","Error","Critical"', null, null, 'Operational', 'HotReload', 0, false, false, false, 10),

('dr', 'RpoMinutes', 'RPO', 'Recovery point objective in minutes', 'Integer', '5', null, 0, 1440, 'Critical', 'HotReload', 1, false, false, false, 10),

('dr', 'RtoMinutes', 'RTO', 'Recovery time objective in minutes', 'Integer', '30', null, 1, 1440, 'Critical', 'HotReload', 1, false, false, false, 20),

('retention', 'TransactionDays', 'Transaction Retention', 'Online transaction retention days', 'Integer', '365', null, 30, 3650, 'Critical', 'HotReload', 1, false, false, false, 10),

('diagnostics', 'ConnectionTestEnabled', 'Connection Test', 'Allow privileged live dependency connectivity tests', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', 1, false, false, false, 10);

merge into dbo.configurationdefinitions as t

using tmp_configurationdefinitions s on t.domaincode=s.domaincode and t.key=s.key

when matched then update set displayname=s.displayname,description=s.description,valuetype=s.valuetype,defaultvalue=s.defaultvalue,allowedvaluesjson=s.allowed,minimumvalue=s.minval,maximumvalue=s.maxval,sensitivity=s.sensitivity,reloadpolicy=s.reloadpolicy,requiresapproval=s.requiresapproval,issecret=s.issecret,issensitive=s.issensitive,productionlocked=s.productionlocked,displayorder=s.displayorder,enabled = true

when not matched then insert(id,domaincode,key,displayname,description,valuetype,defaultvalue,allowedvaluesjson,minimumvalue,maximumvalue,sensitivity,reloadpolicy,requiresapproval,issecret,issensitive,productionlocked,displayorder,enabled) values(gen_random_uuid(),s.domaincode,s.key,s.displayname,s.description,s.valuetype,s.defaultvalue,s.allowed,s.minval,s.maxval,s.sensitivity,s.reloadpolicy,s.requiresapproval,s.issecret,s.issensitive,s.productionlocked,s.displayorder,true);

-- transaction batch boundary omitted;

-- ============================================================================

-- migration 039_enterprise_settings_completeness_runtime_validation.sql

-- ============================================================================



-- transaction batch boundary omitted; outer PostgreSQL transaction is used;

-- v44.3a enterprise settings completeness: field-level definitions across all control-plane domains.

drop table if exists tmp_configurationdefinitions;

create temp table tmp_configurationdefinitions (

 domaincode varchar(64), key varchar(160), displayname varchar(160), description varchar(1024),

 valuetype varchar(32), defaultvalue text, allowedvaluesjson text, minimumvalue decimal(28,8), maximumvalue decimal(28,8),

 sensitivity varchar(32), reloadpolicy varchar(32), requiresapproval boolean, issecret boolean, issensitive boolean, productionlocked boolean, validationpattern varchar(512), displayorder int);

insert into tmp_configurationdefinitions values

('general', 'InstitutionName', 'Institution Name', 'Display/legal institution name', 'String', 'BankSwitch Institution', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('general', 'CountryCode', 'Country Code', 'ISO 3166-1 alpha-2 country code', 'String', 'IN', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, '^A-Z{2}$', 60),

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

('database', 'IsolationLevel', 'Write Isolation Level', 'Default financial write isolation level', 'Enum', 'ReadCommitted', '"ReadCommitted","RepeatableRead","Serializable","Snapshot"', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 70),

('transactions', 'FinancialTimeoutSeconds', 'Financial Timeout', '0200 transaction timeout', 'Integer', '30', null, 1, 180, 'Critical', 'HotReload', true, false, false, false, null, 50),

('transactions', 'ReversalTimeoutSeconds', 'Reversal Timeout', 'Reversal response timeout', 'Integer', '30', null, 1, 180, 'Critical', 'HotReload', true, false, false, false, null, 60),

('transactions', 'SafRetrySeconds', 'SAF Retry Interval', 'Store-and-forward retry interval', 'Integer', '60', null, 5, 3600, 'Critical', 'HotReload', true, false, false, false, null, 70),

('transactions', 'MaxSafRetries', 'Maximum SAF Retries', 'Maximum SAF delivery attempts', 'Integer', '10', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, null, 80),

('transactions', 'IdempotencyTtlSeconds', 'Idempotency TTL', 'Distributed idempotency retention', 'Integer', '86400', null, 60, 604800, 'Critical', 'HotReload', true, false, false, false, null, 90),

('transactions', 'StipEnabled', 'STIP Enabled', 'Enable stand-in transaction processing', 'Boolean', 'true', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 100),

('iso8583', 'IsoVersion', 'ISO Version', 'Default ISO 8583 dialect', 'Enum', '1987', '"1987","1993","2003"', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 30),

('iso8583', 'CharacterEncoding', 'Character Encoding', 'Network message character encoding', 'Enum', 'ASCII', '"ASCII","EBCDIC","UTF-8"', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 40),

('iso8583', 'BitmapEncoding', 'Bitmap Encoding', 'Bitmap encoding format', 'Enum', 'ASCIIHEX', '"ASCIIHEX","BINARY"', null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 50),

('iso8583', 'TpduEnabled', 'TPDU Enabled', 'Enable TPDU header processing', 'Boolean', 'false', null, null, null, 'Critical', 'ServiceRestart', true, false, false, false, null, 60),

('iso8583', 'Field55MaxLength', 'DE55 Max Length', 'Maximum EMV field 55 length', 'Integer', '999', null, 1, 4096, 'Sensitive', 'ServiceRestart', true, false, false, false, null, 70),

('routing', 'Strategy', 'Routing Strategy', 'Route selection strategy', 'Enum', 'Priority', '"Priority","Weighted","LeastCost","HealthAware"', null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('routing', 'HealthThresholdPercent', 'Health Threshold', 'Minimum destination health percent', 'Integer', '95', null, 1, 100, 'Critical', 'HotReload', true, false, false, false, null, 30),

('routing', 'CacheSeconds', 'Route Cache TTL', 'Route rules cache interval', 'Integer', '30', null, 0, 3600, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('routing', 'SimulationEnabled', 'Route Simulation', 'Allow route simulation in admin', 'Boolean', 'true', null, null, null, 'Operational', 'HotReload', false, false, false, false, null, 50),

('network-hosts', 'VisaPrimaryEndpoint', 'Visa Primary Endpoint', 'Visa authorization endpoint', 'Uri', 'https\://visa.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 30),

('network-hosts', 'MastercardPrimaryEndpoint', 'Mastercard Primary Endpoint', 'Mastercard authorization endpoint', 'Uri', 'https\://mastercard.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 40),

('network-hosts', 'RupayPrimaryEndpoint', 'RuPay Primary Endpoint', 'RuPay/NPCI authorization endpoint', 'Uri', 'https\://rupay.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 50),

('network-hosts', 'ReconnectSeconds', 'Reconnect Interval', 'Network connection reconnect interval', 'Integer', '10', null, 1, 600, 'Sensitive', 'HotReload', true, false, false, false, null, 60),

('network-hosts', 'SignOnIntervalSeconds', 'Sign-On Interval', 'Network sign-on refresh interval', 'Integer', '300', null, 10, 86400, 'Sensitive', 'HotReload', true, false, false, false, null, 70),

('network-hosts', 'CertificateRef', 'Network Certificate Reference', 'Certificate inventory reference for scheme TLS', 'CertificateReference', 'SCHEME-TLS-CERT', null, null, null, 'Critical', 'ConnectionRestart', true, false, true, false, null, 80),

('atm', 'DefaultProtocol', 'Default ATM Protocol', 'Default ATM terminal protocol', 'Enum', 'NDC+', '"NDC","NDC+","DDC","XFS","APTRA"', null, null, 'Sensitive', 'ConnectionRestart', true, false, false, false, null, 20),

('atm', 'CommandTimeoutSeconds', 'ATM Command Timeout', 'Terminal command timeout', 'Integer', '30', null, 1, 300, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('atm', 'EjUploadMinutes', 'EJ Upload Interval', 'Electronic journal upload interval', 'Integer', '15', null, 1, 1440, 'Operational', 'HotReload', false, false, false, false, null, 40),

('atm', 'ScreenPackageVersion', 'Screen Package Version', 'Active ATM screen package version', 'String', '1.0.0', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('pos', 'HeartbeatSeconds', 'POS Heartbeat', 'POS terminal heartbeat interval', 'Integer', '60', null, 5, 3600, 'Operational', 'HotReload', false, false, false, false, null, 20),

('pos', 'CommandTimeoutSeconds', 'POS Command Timeout', 'POS remote command timeout', 'Integer', '30', null, 1, 300, 'Sensitive', 'HotReload', true, false, false, false, null, 30),

('pos', 'ContactlessOfflineEnabled', 'Offline Contactless', 'Enable controlled offline contactless', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 40),

('pos', 'TipAdjustmentEnabled', 'Tip Adjustment', 'Enable post-auth tip adjustment', 'Boolean', 'true', null, null, null, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('pos', 'CashAtPosEnabled', 'Cash@POS', 'Enable Cash@POS acquiring', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 60),

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

('aml', 'ProviderEndpoint', 'AML Provider Endpoint', 'External screening provider endpoint', 'Uri', 'https\://aml.invalid', null, null, null, 'Sensitive', 'ConnectionRestart', true, false, false, false, null, 50),

('settlement', 'AutoSettlementEnabled', 'Auto Settlement', 'Enable automatic settlement posting', 'Boolean', 'false', null, null, null, 'Critical', 'HotReload', true, false, false, false, null, 20),

('settlement', 'DefaultCurrency', 'Settlement Currency', 'Default settlement currency', 'String', 'INR', null, null, null, 'Critical', 'HotReload', true, false, false, false, '^A-Z{3}$', 30),

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

('cbs', 'SecondaryEndpoint', 'CBS Secondary Endpoint', 'Secondary CBS integration endpoint', 'Uri', 'https\://cbs-dr.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 20),

('cbs', 'TimeoutSeconds', 'CBS Timeout', 'Core banking request timeout', 'Integer', '10', null, 1, 120, 'Critical', 'HotReload', true, false, false, false, null, 30),

('cbs', 'RetryCount', 'CBS Retry Count', 'Core banking transient retry count', 'Integer', '2', null, 0, 10, 'Sensitive', 'HotReload', true, false, false, false, null, 40),

('cbs', 'CircuitBreakerFailures', 'CBS Circuit Breaker', 'Failure threshold before circuit opens', 'Integer', '5', null, 1, 100, 'Sensitive', 'HotReload', true, false, false, false, null, 50),

('enterprise', 'EsbEndpoint', 'ESB Endpoint', 'Enterprise service bus endpoint', 'Uri', 'https\://esb.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 20),

('enterprise', 'AcsEndpoint', 'ACS / 3DS Endpoint', 'ACS/3DS integration endpoint', 'Uri', 'https\://acs.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 30),

('enterprise', 'FrmEndpoint', 'FRM Endpoint', 'Fraud risk manager endpoint', 'Uri', 'https\://frm.invalid', null, null, null, 'Critical', 'ConnectionRestart', true, false, false, false, null, 40),

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

('dr', 'Mode', 'DR Mode', 'Disaster recovery topology', 'Enum', 'ActivePassive', '"ActivePassive","ActiveActive"', null, null, 'Critical', 'ClusterRestart', true, false, false, false, null, 30),

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

merge into dbo.configurationdefinitions as t

using tmp_configurationdefinitions s on t.domaincode=s.domaincode and t.key=s.key

when matched then update set

 displayname=s.displayname, description=s.description, valuetype=s.valuetype, defaultvalue=s.defaultvalue, allowedvaluesjson=s.allowedvaluesjson,

 minimumvalue=s.minimumvalue, maximumvalue=s.maximumvalue, sensitivity=s.sensitivity, reloadpolicy=s.reloadpolicy, requiresapproval=s.requiresapproval,

 issecret=s.issecret, issensitive=s.issensitive, productionlocked=s.productionlocked, validationpattern=s.validationpattern, displayorder=s.displayorder, enabled = true

when not matched then insert(id,domaincode,key,displayname,description,valuetype,defaultvalue,allowedvaluesjson,minimumvalue,maximumvalue,sensitivity,reloadpolicy,requiresapproval,issecret,issensitive,productionlocked,validationpattern,displayorder,enabled) values(gen_random_uuid(),s.domaincode,s.key,s.displayname,s.description,s.valuetype,s.defaultvalue,s.allowedvaluesjson,s.minimumvalue,s.maximumvalue,s.sensitivity,s.reloadpolicy,s.requiresapproval,s.issecret,s.issensitive,s.productionlocked,s.validationpattern,s.displayorder,true);

-- transaction batch boundary omitted;

-- ============================================================================

-- migration 040_production_ndc_ndcplus_atm_protocol_engine.sql

-- ============================================================================

-- v44.5 production ndc/ndc+ atm protocol engine

-- sql server canonical migration. stores restart-safe atm protocol session state,

-- trace hashes, device status, download blocks and electronic journal events.

do $$
begin
    if to_regclass('dbo.ndcterminalsessions') is null then
create table dbo.ndcterminalsessions (

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
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ndcprotocoltraces') is null then
create table dbo.ndcprotocoltraces (

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

    constraint ck_ndcprotocoltraces_json check ((parsedfieldsjson)::jsonb is not null),

    constraint ck_ndcprotocoltraces_direction check (direction in ('Inbound','Outbound'))

);

create index ix_ndcprotocoltraces_terminal_time on dbo.ndcprotocoltraces(terminalid, recordedat desc);

create index ix_ndcprotocoltraces_correlation on dbo.ndcprotocoltraces(correlationid);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ndcdevicestatusevents') is null then
create table dbo.ndcdevicestatusevents (

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

create index ix_ndcdevicestatus_terminal_time on dbo.ndcdevicestatusevents(terminalid, occurredat desc);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ndcdownloadartifacts') is null then
create table dbo.ndcdownloadartifacts (

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

create index ix_ndcdownload_terminal_version on dbo.ndcdownloadartifacts(terminalid, downloadtype, version, blocknumber);
    end if;
end
$$;

do $$
begin
    if to_regclass('dbo.ndcelectronicjournalentries') is null then
create table dbo.ndcelectronicjournalentries (

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

create index ix_ndcej_terminal_time on dbo.ndcelectronicjournalentries(terminalid, occurredat desc);

create index ix_ndcej_rrn_stan on dbo.ndcelectronicjournalentries(rrn, stan);
    end if;
end
$$;

-- ============================================================================

-- ============================================================================
-- migration 041_canonical_sql_server_schema_referential_integrity_hardening.sql
-- ============================================================================
-- v44.6 canonical sql server schema & referential integrity hardening
-- establishes the authoritative schema contract and production repository mappings.

CREATE TABLE IF NOT EXISTS dbo.repositorytablemappings (
    interfacename varchar(160) NOT NULL CONSTRAINT pk_repositorytablemappings PRIMARY KEY,
    implementationname varchar(200) NOT NULL,
    primarytable varchar(128) NOT NULL,
    environmentscope varchar(32) NOT NULL,
    isauthoritative boolean NOT NULL DEFAULT true,
    introducedversion varchar(32) NOT NULL,
    lastvalidatedat timestamptz NOT NULL DEFAULT clock_timestamp()
);

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('IPosAcquiringProductionRepository', 'SqlPosAcquiringProductionRepository', 'dbo.posmerchants', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('IPosTerminalDrivingRepository', 'SqlPosTerminalDrivingRepository', 'dbo.posterminalprofiles', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('IKycRepository', 'SqlKycRepository', 'dbo.kycdocuments', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('IAcquiringCertificationRepository', 'SqlAcquiringCertificationRepository', 'dbo.acquiringcertificationstore', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('IAcquiringCertificationLabRepository', 'SqlAcquiringCertificationLabRepository', 'dbo.acquiringcertificationlabstore', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('IIssuerCertificationRepository', 'SqlIssuerCertificationRepository', 'dbo.issuercertificationstore', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

INSERT INTO dbo.repositorytablemappings (interfacename, implementationname, primarytable, environmentscope, isauthoritative, introducedversion)
VALUES ('INdcProtocolRepository', 'SqlNdcProtocolRepository', 'dbo.ndcterminalsessions', 'Production', true, 'v44.6')
ON dbo.CONFLICT(interfacename) DO UPDATE SET
    implementationname = EXCLUDED.implementationname,
    primarytable = EXCLUDED.primarytable,
    environmentscope = EXCLUDED.environmentscope,
    isauthoritative = EXCLUDED.isauthoritative,
    introducedversion = EXCLUDED.introducedversion,
    lastvalidatedat = clock_timestamp();

-- Consolidate obsolete v32 snake_case POS tables into canonical production tables.

DO $$
BEGIN
    IF to_regclass('dbo.pos_terminal_profile') IS NOT NULL THEN
        insert into dbo.posterminalprofiles(terminalid,merchantid,vendor,protocol,serialnumber,devicemodel,branchcode,locationcode,countrycode,currencycode,ismpos,contactlessenabled,status,capabilitiesjson,createdat,updatedat)
        
            select terminal_id,merchant_id,vendor,protocol,serial_number,device_model,branch_code,location_code,country_code,currency_code,is_mpos,contactless_enabled,status,coalesce(capabilities_json,'{}'),created_at_utc,updated_at_utc
        
            from dbo.pos_terminal_profile s where not exists(select 1 from dbo.posterminalprofiles t where t.terminalid=s.terminal_id);
        
            drop table dbo.pos_terminal_profile;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.mpos_enrollment') IS NOT NULL THEN
        insert into dbo.posmposenrollments(id,terminalid,merchantid,devicebindingid,mobilenumbermasked,appversion,osname,osversion,status,enrolledat,updatedat)
        
            select id,terminal_id,merchant_id,device_binding_id,mobile_number_masked,app_version,os_name,os_version,status,enrolled_at_utc,updated_at_utc
        
            from dbo.mpos_enrollment s where not exists(select 1 from dbo.posmposenrollments t where t.id=s.id);
        
            drop table dbo.mpos_enrollment;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_key_download_certification') IS NOT NULL THEN
        insert into dbo.poskeydownloadcertifications(id,terminalid,vendor,protocol,scheme,keyscheme,certificationpackreference,evidencehash,status,certifiedat,remarks)
        
            select id,terminal_id,vendor,protocol,scheme,key_scheme,certification_pack_ref,evidence_hash,status,certified_at_utc,coalesce(remarks,'')
        
            from dbo.pos_key_download_certification s where not exists(select 1 from dbo.poskeydownloadcertifications t where t.id=s.id);
        
            drop table dbo.pos_key_download_certification;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_key_download_session') IS NOT NULL THEN
        insert into dbo.poskeydownloadsessions(id,terminalid,scheme,tmkkcv,tpkkcv,takkcv,status,requestedat,completedat,correlationid)
        
            select id,terminal_id,scheme,tmk_kcv,tpk_kcv,tak_kcv,status,requested_at_utc,completed_at_utc,correlation_id
        
            from dbo.pos_key_download_session s where not exists(select 1 from dbo.poskeydownloadsessions t where t.id=s.id);
        
            drop table dbo.pos_key_download_session;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_contactless_transaction_flow') IS NOT NULL THEN
        insert into dbo.poscontactlesstransactionflows(id,terminalid,merchantid,mode,panmasked,amount,currencycode,emvcryptogram,offlineapprovedbyterminal,onlinehostauthorised,responsecode,createdat,correlationid)
        
            select id,terminal_id,merchant_id,mode,pan_masked,amount,currency_code,emv_cryptogram,offline_approved,online_authorised,response_code,created_at_utc,correlation_id
        
            from dbo.pos_contactless_transaction_flow s where not exists(select 1 from dbo.poscontactlesstransactionflows t where t.id=s.id);
        
            drop table dbo.pos_contactless_transaction_flow;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_tip_adjustment') IS NOT NULL THEN
        insert into dbo.postipadjustments(id,originaltransactionid,terminalid,merchantid,originalamount,tipamount,finalamount,currencycode,approvalcode,status,createdat,correlationid)
        
            select id,original_transaction_id,terminal_id,merchant_id,original_amount,tip_amount,final_amount,currency_code,approval_code,status,created_at_utc,correlation_id
        
            from dbo.pos_tip_adjustment s where not exists(select 1 from dbo.postipadjustments t where t.id=s.id);
        
            drop table dbo.pos_tip_adjustment;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_cash_at_pos_acquiring') IS NOT NULL THEN
        insert into dbo.poscashatposacquiring(id,terminalid,merchantid,panmasked,purchaseamount,cashamount,totalamount,currencycode,approvalcode,responsecode,createdat,correlationid)
        
            select id,terminal_id,merchant_id,pan_masked,purchase_amount,cash_amount,total_amount,currency_code,approval_code,response_code,created_at_utc,correlation_id
        
            from dbo.pos_cash_at_pos_acquiring s where not exists(select 1 from dbo.poscashatposacquiring t where t.id=s.id);
        
            drop table dbo.pos_cash_at_pos_acquiring;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.merchant_settlement_batch') IS NOT NULL THEN
        insert into dbo.posmerchantsettlementbatches(id,merchantid,settlementdate,currencycode,transactioncount,grossamount,interchangefee,mdrfee,gstamount,netpayable,status,createdat,filehash,correlationid)
        
            select id,merchant_id,settlement_date,currency_code,transaction_count,gross_amount,interchange_fee,mdr_fee,gst_amount,net_payable,status,created_at_utc,file_hash,correlation_id
        
            from dbo.merchant_settlement_batch s where not exists(select 1 from dbo.posmerchantsettlementbatches t where t.id=s.id);
        
            drop table dbo.merchant_settlement_batch;
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.pos_device_command') IS NOT NULL THEN
        insert into dbo.posdevicecommands(id,terminalid,command,parametersjson,status,createdat,appliedat,correlationid)
        
            select id,terminal_id,command,coalesce(parameters_json,'{}'),status,created_at_utc,applied_at_utc,correlation_id
        
            from dbo.pos_device_command s where not exists(select 1 from dbo.posdevicecommands t where t.id=s.id);
        
            drop table dbo.pos_device_command;
    END IF;
END
$$;

-- High-confidence foreign keys. Existing orphaned rows intentionally cause the ADD CONSTRAINT to fail.
DO $$
BEGIN
    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customers') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_kycdocuments_customers'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_kycdocuments_customers FOREIGN KEY (customerid) REFERENCES dbo.customers (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.walletaccounts') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_authorizationholds_walletaccounts'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_authorizationholds_walletaccounts FOREIGN KEY (walletaccountid) REFERENCES dbo.walletaccounts (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.prepaidcards') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_authorizationholds_prepaidcards'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_authorizationholds_prepaidcards FOREIGN KEY (cardid) REFERENCES dbo.prepaidcards (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.prepaidcards') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_prepaidcards_replacedbycard'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_prepaidcards_replacedbycard FOREIGN KEY (replacedbycardid) REFERENCES dbo.prepaidcards (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customers') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_directdebitmandates_customers'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_directdebitmandates_customers FOREIGN KEY (customerid) REFERENCES dbo.customers (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customers') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_customerdisputes_customers'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_customerdisputes_customers FOREIGN KEY (customerid) REFERENCES dbo.customers (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.chargebackcases') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_customerdisputes_chargebackcases'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_customerdisputes_chargebackcases FOREIGN KEY (linkedchargebackid) REFERENCES dbo.chargebackcases (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customerdisputes') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_disputeevidence_customerdisputes'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_disputeevidence_customerdisputes FOREIGN KEY (disputeid) REFERENCES dbo.customerdisputes (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.glaccounts') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_gljournallines_glaccounts'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_gljournallines_glaccounts FOREIGN KEY (accountcode) REFERENCES dbo.glaccounts (accountcode);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.glaccounts') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_glaccountbalances_glaccounts'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_glaccountbalances_glaccounts FOREIGN KEY (accountcode) REFERENCES dbo.glaccounts (accountcode);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.gljournalentries') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_gljournalentries_reversesjournal'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_gljournalentries_reversesjournal FOREIGN KEY (reversesjournalid) REFERENCES dbo.gljournalentries (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customers') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardproductionorders_customers'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardproductionorders_customers FOREIGN KEY (customerid) REFERENCES dbo.customers (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.cardproducts') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardproductionorders_cardproducts'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardproductionorders_cardproducts FOREIGN KEY (productid) REFERENCES dbo.cardproducts (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.prepaidcards') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardproductionorders_prepaidcards'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardproductionorders_prepaidcards FOREIGN KEY (cardid) REFERENCES dbo.prepaidcards (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.prepaidcards') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardproductionorders_oldcard'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardproductionorders_oldcard FOREIGN KEY (oldcardid) REFERENCES dbo.prepaidcards (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.debitcardbranchstockitems') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardproductionorders_branchstock'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardproductionorders_branchstock FOREIGN KEY (branchstockitemid) REFERENCES dbo.debitcardbranchstockitems (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customers') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardbranchstock_customers'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardbranchstock_customers FOREIGN KEY (assignedcustomerid) REFERENCES dbo.customers (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.prepaidcards') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_debitcardbranchstock_prepaidcards'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_debitcardbranchstock_prepaidcards FOREIGN KEY (assignedcardid) REFERENCES dbo.prepaidcards (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.prepaidcards') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_hotlistpropagation_prepaidcards'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_hotlistpropagation_prepaidcards FOREIGN KEY (cardid) REFERENCES dbo.prepaidcards (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.chargebackcases') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_networkdisputerecords_chargeback'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_networkdisputerecords_chargeback FOREIGN KEY (local_chargeback_case_id) REFERENCES dbo.chargebackcases (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.customerdisputes') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_networkdisputerecords_dispute'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_networkdisputerecords_dispute FOREIGN KEY (local_dispute_id) REFERENCES dbo.customerdisputes (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.atm_screen_definition') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_atmlod_screendefinition'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_atmlod_screendefinition FOREIGN KEY (screen_definition_id) REFERENCES dbo.atm_screen_definition (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.atm_screen_definition') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_atmscreendistribution_screendefinition'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_atmscreendistribution_screendefinition FOREIGN KEY (screen_definition_id) REFERENCES dbo.atm_screen_definition (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.atm_terminal_profile') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_atmadmincash_terminal'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_atmadmincash_terminal FOREIGN KEY (terminal_id) REFERENCES dbo.atm_terminal_profile (terminal_id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.atm_terminal_profile') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_atmc3r_terminal'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_atmc3r_terminal FOREIGN KEY (terminal_id) REFERENCES dbo.atm_terminal_profile (terminal_id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.atm_terminal_profile') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_atmevidence_terminal'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_atmevidence_terminal FOREIGN KEY (terminal_id) REFERENCES dbo.atm_terminal_profile (terminal_id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.atm_voice_prompt_pack') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_atmscreendefinition_voicepack'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_atmscreendefinition_voicepack FOREIGN KEY (voice_prompt_pack_id) REFERENCES dbo.atm_voice_prompt_pack (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posterminalprofiles_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posterminalprofiles_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posmposenrollments_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posmposenrollments_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posmposenrollments_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posmposenrollments_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poskeydownloadcertifications_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poskeydownloadcertifications_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poskeydownloadsessions_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poskeydownloadsessions_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poscontactlesstransactionflows_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poscontactlesstransactionflows_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poscontactlesstransactionflows_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poscontactlesstransactionflows_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_postipadjustments_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_postipadjustments_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_postipadjustments_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_postipadjustments_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poscashatposacquiring_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poscashatposacquiring_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poscashatposacquiring_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poscashatposacquiring_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posmerchantsettlementbatches_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posmerchantsettlementbatches_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posdevicecommands_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posdevicecommands_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poscommandqueue_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poscommandqueue_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posofflinecontactlesstxns_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posofflinecontactlesstxns_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posofflinecontactlesstxns_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posofflinecontactlesstxns_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posofflinecontactlessbatches_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posofflinecontactlessbatches_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posterminalprofiles') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poskeyceremonies_terminalprofiles'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poskeyceremonies_terminalprofiles FOREIGN KEY (terminalid) REFERENCES dbo.posterminalprofiles (terminalid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_poskeyceremonies_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_poskeyceremonies_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.posmerchants') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_posmerchantsettlementpostings_merchants'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_posmerchantsettlementpostings_merchants FOREIGN KEY (merchantid) REFERENCES dbo.posmerchants (merchantid);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.acquiring_cert_packs') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_acquiringcertruns_packs'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_acquiringcertruns_packs FOREIGN KEY (pack_id) REFERENCES dbo.acquiring_cert_packs (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.acquiring_cert_runs') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_acquiringcertresults_runs'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_acquiringcertresults_runs FOREIGN KEY (run_id) REFERENCES dbo.acquiring_cert_runs (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.acquiring_cert_test_cases') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_acquiringcertresults_testcases'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_acquiringcertresults_testcases FOREIGN KEY (test_case_id) REFERENCES dbo.acquiring_cert_test_cases (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.acquiring_cert_runs') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_acquiringcertreports_runs'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_acquiringcertreports_runs FOREIGN KEY (run_id) REFERENCES dbo.acquiring_cert_runs (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.issuer_cert_packs') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_issuercertruns_packs'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_issuercertruns_packs FOREIGN KEY (pack_id) REFERENCES dbo.issuer_cert_packs (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.issuer_cert_runs') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_issuercertresults_runs'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_issuercertresults_runs FOREIGN KEY (run_id) REFERENCES dbo.issuer_cert_runs (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.issuer_cert_test_cases') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_issuercertresults_testcases'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_issuercertresults_testcases FOREIGN KEY (test_case_id) REFERENCES dbo.issuer_cert_test_cases (id);
    END IF;

    IF to_regclass('dbo.kycdocuments') IS NOT NULL
       AND to_regclass('dbo.issuer_cert_runs') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           JOIN pg_class r ON r.oid = c.conrelid
           JOIN pg_namespace n ON n.oid = r.relnamespace
           WHERE n.nspname = 'dbo' AND r.relname = 'kycdocuments' AND c.conname = 'fk_issuercertreports_runs'
       )
    THEN
        ALTER TABLE dbo.kycdocuments ADD CONSTRAINT fk_issuercertreports_runs FOREIGN KEY (run_id) REFERENCES dbo.issuer_cert_runs (id);
    END IF;

END
$$;

-- Referential-integrity indexes plus the two source unique indexes.
DO $$
BEGIN
    IF to_regclass('dbo.kycdocuments') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_kycdocuments_customers ON dbo.kycdocuments (customerid);
    END IF;

    IF to_regclass('dbo.authorizationholds') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_authorizationholds_walletaccounts ON dbo.authorizationholds (walletaccountid);
    END IF;

    IF to_regclass('dbo.authorizationholds') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_authorizationholds_prepaidcards ON dbo.authorizationholds (cardid);
    END IF;

    IF to_regclass('dbo.prepaidcards') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_prepaidcards_replacedbycard ON dbo.prepaidcards (replacedbycardid);
    END IF;

    IF to_regclass('dbo.directdebitmandates') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_directdebitmandates_customers ON dbo.directdebitmandates (customerid);
    END IF;

    IF to_regclass('dbo.customerdisputes') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_customerdisputes_customers ON dbo.customerdisputes (customerid);
    END IF;

    IF to_regclass('dbo.customerdisputes') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_customerdisputes_chargebackcases ON dbo.customerdisputes (linkedchargebackid);
    END IF;

    IF to_regclass('dbo.disputeevidence') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_disputeevidence_customerdisputes ON dbo.disputeevidence (disputeid);
    END IF;

    IF to_regclass('dbo.gljournallines') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_gljournallines_glaccounts ON dbo.gljournallines (accountcode);
    END IF;

    IF to_regclass('dbo.glaccountbalances') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_glaccountbalances_glaccounts ON dbo.glaccountbalances (accountcode);
    END IF;

    IF to_regclass('dbo.gljournalentries') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_gljournalentries_reversesjournal ON dbo.gljournalentries (reversesjournalid);
    END IF;

    IF to_regclass('dbo.debitcardproductionorders') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardproductionorders_customers ON dbo.debitcardproductionorders (customerid);
    END IF;

    IF to_regclass('dbo.debitcardproductionorders') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardproductionorders_cardproducts ON dbo.debitcardproductionorders (productid);
    END IF;

    IF to_regclass('dbo.debitcardproductionorders') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardproductionorders_prepaidcards ON dbo.debitcardproductionorders (cardid);
    END IF;

    IF to_regclass('dbo.debitcardproductionorders') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardproductionorders_oldcard ON dbo.debitcardproductionorders (oldcardid);
    END IF;

    IF to_regclass('dbo.debitcardproductionorders') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardproductionorders_branchstock ON dbo.debitcardproductionorders (branchstockitemid);
    END IF;

    IF to_regclass('dbo.debitcardbranchstockitems') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardbranchstock_customers ON dbo.debitcardbranchstockitems (assignedcustomerid);
    END IF;

    IF to_regclass('dbo.debitcardbranchstockitems') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_debitcardbranchstock_prepaidcards ON dbo.debitcardbranchstockitems (assignedcardid);
    END IF;

    IF to_regclass('dbo.hotlistpropagationevents') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_hotlistpropagation_prepaidcards ON dbo.hotlistpropagationevents (cardid);
    END IF;

    IF to_regclass('dbo.network_dispute_exchange_records') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_networkdisputerecords_chargeback ON dbo.network_dispute_exchange_records (local_chargeback_case_id);
    END IF;

    IF to_regclass('dbo.network_dispute_exchange_records') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_networkdisputerecords_dispute ON dbo.network_dispute_exchange_records (local_dispute_id);
    END IF;

    IF to_regclass('dbo.atm_lod_file_artifact') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_atmlod_screendefinition ON dbo.atm_lod_file_artifact (screen_definition_id);
    END IF;

    IF to_regclass('dbo.atm_screen_distribution_job') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_atmscreendistribution_screendefinition ON dbo.atm_screen_distribution_job (screen_definition_id);
    END IF;

    IF to_regclass('dbo.atm_admin_cash_operation') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_atmadmincash_terminal ON dbo.atm_admin_cash_operation (terminal_id);
    END IF;

    IF to_regclass('dbo.atm_c3r_reconciliation_run') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_atmc3r_terminal ON dbo.atm_c3r_reconciliation_run (terminal_id);
    END IF;

    IF to_regclass('dbo.atm_evidence_artifact') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_atmevidence_terminal ON dbo.atm_evidence_artifact (terminal_id);
    END IF;

    IF to_regclass('dbo.atm_screen_definition') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_atmscreendefinition_voicepack ON dbo.atm_screen_definition (voice_prompt_pack_id);
    END IF;

    IF to_regclass('dbo.posterminalprofiles') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posterminalprofiles_merchants ON dbo.posterminalprofiles (merchantid);
    END IF;

    IF to_regclass('dbo.posmposenrollments') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posmposenrollments_terminalprofiles ON dbo.posmposenrollments (terminalid);
    END IF;

    IF to_regclass('dbo.posmposenrollments') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posmposenrollments_merchants ON dbo.posmposenrollments (merchantid);
    END IF;

    IF to_regclass('dbo.poskeydownloadcertifications') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poskeydownloadcertifications_terminalprofiles ON dbo.poskeydownloadcertifications (terminalid);
    END IF;

    IF to_regclass('dbo.poskeydownloadsessions') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poskeydownloadsessions_terminalprofiles ON dbo.poskeydownloadsessions (terminalid);
    END IF;

    IF to_regclass('dbo.poscontactlesstransactionflows') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poscontactlesstransactionflows_terminalprofiles ON dbo.poscontactlesstransactionflows (terminalid);
    END IF;

    IF to_regclass('dbo.poscontactlesstransactionflows') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poscontactlesstransactionflows_merchants ON dbo.poscontactlesstransactionflows (merchantid);
    END IF;

    IF to_regclass('dbo.postipadjustments') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_postipadjustments_terminalprofiles ON dbo.postipadjustments (terminalid);
    END IF;

    IF to_regclass('dbo.postipadjustments') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_postipadjustments_merchants ON dbo.postipadjustments (merchantid);
    END IF;

    IF to_regclass('dbo.poscashatposacquiring') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poscashatposacquiring_terminalprofiles ON dbo.poscashatposacquiring (terminalid);
    END IF;

    IF to_regclass('dbo.poscashatposacquiring') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poscashatposacquiring_merchants ON dbo.poscashatposacquiring (merchantid);
    END IF;

    IF to_regclass('dbo.posmerchantsettlementbatches') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posmerchantsettlementbatches_merchants ON dbo.posmerchantsettlementbatches (merchantid);
    END IF;

    IF to_regclass('dbo.posdevicecommands') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posdevicecommands_terminalprofiles ON dbo.posdevicecommands (terminalid);
    END IF;

    IF to_regclass('dbo.poscommandqueue') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poscommandqueue_terminalprofiles ON dbo.poscommandqueue (terminalid);
    END IF;

    IF to_regclass('dbo.posofflinecontactlesstxns') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posofflinecontactlesstxns_terminalprofiles ON dbo.posofflinecontactlesstxns (terminalid);
    END IF;

    IF to_regclass('dbo.posofflinecontactlesstxns') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posofflinecontactlesstxns_merchants ON dbo.posofflinecontactlesstxns (merchantid);
    END IF;

    IF to_regclass('dbo.posofflinecontactlessbatches') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posofflinecontactlessbatches_merchants ON dbo.posofflinecontactlessbatches (merchantid);
    END IF;

    IF to_regclass('dbo.poskeyceremonies') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poskeyceremonies_terminalprofiles ON dbo.poskeyceremonies (terminalid);
    END IF;

    IF to_regclass('dbo.poskeyceremonies') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_poskeyceremonies_merchants ON dbo.poskeyceremonies (merchantid);
    END IF;

    IF to_regclass('dbo.posmerchantsettlementpostings') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_posmerchantsettlementpostings_merchants ON dbo.posmerchantsettlementpostings (merchantid);
    END IF;

    IF to_regclass('dbo.acquiring_cert_runs') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_acquiringcertruns_packs ON dbo.acquiring_cert_runs (pack_id);
    END IF;

    IF to_regclass('dbo.acquiring_cert_test_results') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_acquiringcertresults_runs ON dbo.acquiring_cert_test_results (run_id);
    END IF;

    IF to_regclass('dbo.acquiring_cert_test_results') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_acquiringcertresults_testcases ON dbo.acquiring_cert_test_results (test_case_id);
    END IF;

    IF to_regclass('dbo.acquiring_cert_evidence_reports') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_acquiringcertreports_runs ON dbo.acquiring_cert_evidence_reports (run_id);
    END IF;

    IF to_regclass('dbo.issuer_cert_runs') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_issuercertruns_packs ON dbo.issuer_cert_runs (pack_id);
    END IF;

    IF to_regclass('dbo.issuer_cert_run_results') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_issuercertresults_runs ON dbo.issuer_cert_run_results (run_id);
    END IF;

    IF to_regclass('dbo.issuer_cert_run_results') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_issuercertresults_testcases ON dbo.issuer_cert_run_results (test_case_id);
    END IF;

    IF to_regclass('dbo.issuer_cert_evidence_reports') IS NOT NULL THEN
        CREATE INDEX IF NOT EXISTS ix_ri_issuercertreports_runs ON dbo.issuer_cert_evidence_reports (run_id);
    END IF;

    IF to_regclass('dbo.posterminalprofiles') IS NOT NULL THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ux_posterminalprofiles_serialnumber ON dbo.posterminalprofiles (serialnumber);
    END IF;

    IF to_regclass('dbo.network_host_profiles') IS NOT NULL THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ux_network_host_profiles_code ON dbo.network_host_profiles (host_code);
    END IF;

END
$$;

-- JSON integrity and core invariant checks from the source migration.
DO $$
BEGIN
    IF to_regclass('dbo.atm_terminal_profile') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.atm_terminal_profile'::regclass AND conname='ck_json_atm_terminal_profile_capabilities_json') THEN
        ALTER TABLE dbo.atm_terminal_profile ADD CONSTRAINT ck_json_atm_terminal_profile_capabilities_json CHECK (capabilities_json is null or (capabilities_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_definition') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.atm_screen_definition'::regclass AND conname='ck_json_atm_screen_definition_screen_flow_json') THEN
        ALTER TABLE dbo.atm_screen_definition ADD CONSTRAINT ck_json_atm_screen_definition_screen_flow_json CHECK (screen_flow_json is null or (screen_flow_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_distribution_job') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.atm_screen_distribution_job'::regclass AND conname='ck_json_atm_screen_distribution_job_terminal_ids_json') THEN
        ALTER TABLE dbo.atm_screen_distribution_job ADD CONSTRAINT ck_json_atm_screen_distribution_job_terminal_ids_json CHECK (terminal_ids_json is null or (terminal_ids_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_screen_distribution_job') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.atm_screen_distribution_job'::regclass AND conname='ck_json_atm_screen_distribution_job_terminal_statuses_json') THEN
        ALTER TABLE dbo.atm_screen_distribution_job ADD CONSTRAINT ck_json_atm_screen_distribution_job_terminal_statuses_json CHECK (terminal_statuses_json is null or (terminal_statuses_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_admin_cash_operation') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.atm_admin_cash_operation'::regclass AND conname='ck_json_atm_admin_cash_operation_cassettes_json') THEN
        ALTER TABLE dbo.atm_admin_cash_operation ADD CONSTRAINT ck_json_atm_admin_cash_operation_cassettes_json CHECK (cassettes_json is null or (cassettes_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.atm_voice_prompt_pack') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.atm_voice_prompt_pack'::regclass AND conname='ck_json_atm_voice_prompt_pack_prompt_file_uris_json') THEN
        ALTER TABLE dbo.atm_voice_prompt_pack ADD CONSTRAINT ck_json_atm_voice_prompt_pack_prompt_file_uris_json CHECK (prompt_file_uris_json is null or (prompt_file_uris_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.posterminalprofiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.posterminalprofiles'::regclass AND conname='ck_json_posterminalprofiles_capabilitiesjson') THEN
        ALTER TABLE dbo.posterminalprofiles ADD CONSTRAINT ck_json_posterminalprofiles_capabilitiesjson CHECK (capabilitiesjson is null or (capabilitiesjson)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.posdevicecommands') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.posdevicecommands'::regclass AND conname='ck_json_posdevicecommands_parametersjson') THEN
        ALTER TABLE dbo.posdevicecommands ADD CONSTRAINT ck_json_posdevicecommands_parametersjson CHECK (parametersjson is null or (parametersjson)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.poscommandqueue') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.poscommandqueue'::regclass AND conname='ck_json_poscommandqueue_parametersjson') THEN
        ALTER TABLE dbo.poscommandqueue ADD CONSTRAINT ck_json_poscommandqueue_parametersjson CHECK (parametersjson is null or (parametersjson)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_cases') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_test_cases'::regclass AND conname='ck_json_acquiring_cert_test_cases_input_fields_json') THEN
        ALTER TABLE dbo.acquiring_cert_test_cases ADD CONSTRAINT ck_json_acquiring_cert_test_cases_input_fields_json CHECK (input_fields_json is null or (input_fields_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_cases') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_test_cases'::regclass AND conname='ck_json_acquiring_cert_test_cases_expected_fields_json') THEN
        ALTER TABLE dbo.acquiring_cert_test_cases ADD CONSTRAINT ck_json_acquiring_cert_test_cases_expected_fields_json CHECK (expected_fields_json is null or (expected_fields_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_packs') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_packs'::regclass AND conname='ck_json_acquiring_cert_packs_test_case_ids_json') THEN
        ALTER TABLE dbo.acquiring_cert_packs ADD CONSTRAINT ck_json_acquiring_cert_packs_test_case_ids_json CHECK (test_case_ids_json is null or (test_case_ids_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_test_results') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_test_results'::regclass AND conname='ck_json_acquiring_cert_test_results_findings_json') THEN
        ALTER TABLE dbo.acquiring_cert_test_results ADD CONSTRAINT ck_json_acquiring_cert_test_results_findings_json CHECK (findings_json is null or (findings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_message_validation_results') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_message_validation_results'::regclass AND conname='ck_json_acquiring_message_validation_results_findings_json') THEN
        ALTER TABLE dbo.acquiring_message_validation_results ADD CONSTRAINT ck_json_acquiring_message_validation_results_findings_json CHECK (findings_json is null or (findings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_host_response_validation_results') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_host_response_validation_results'::regclass AND conname='ck_json_acquiring_host_response_validation_results_findings_json') THEN
        ALTER TABLE dbo.acquiring_host_response_validation_results ADD CONSTRAINT ck_json_acquiring_host_response_validation_results_findings_json CHECK (findings_json is null or (findings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_flow_results') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_flow_results'::regclass AND conname='ck_json_acquiring_cert_flow_results_findings_json') THEN
        ALTER TABLE dbo.acquiring_cert_flow_results ADD CONSTRAINT ck_json_acquiring_cert_flow_results_findings_json CHECK (findings_json is null or (findings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_scenarios') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_scenarios'::regclass AND conname='ck_json_acquiring_cert_scenarios_steps_json') THEN
        ALTER TABLE dbo.acquiring_cert_scenarios ADD CONSTRAINT ck_json_acquiring_cert_scenarios_steps_json CHECK (steps_json is null or (steps_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_replay_runs') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_replay_runs'::regclass AND conname='ck_json_acquiring_cert_replay_runs_masked_samples_json') THEN
        ALTER TABLE dbo.acquiring_cert_replay_runs ADD CONSTRAINT ck_json_acquiring_cert_replay_runs_masked_samples_json CHECK (masked_samples_json is null or (masked_samples_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_fuzz_runs') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_fuzz_runs'::regclass AND conname='ck_json_acquiring_cert_fuzz_runs_findings_json') THEN
        ALTER TABLE dbo.acquiring_cert_fuzz_runs ADD CONSTRAINT ck_json_acquiring_cert_fuzz_runs_findings_json CHECK (findings_json is null or (findings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_regression_runs') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_regression_runs'::regclass AND conname='ck_json_acquiring_cert_regression_runs_regressions_json') THEN
        ALTER TABLE dbo.acquiring_cert_regression_runs ADD CONSTRAINT ck_json_acquiring_cert_regression_runs_regressions_json CHECK (regressions_json is null or (regressions_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.acquiring_cert_plugins') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.acquiring_cert_plugins'::regclass AND conname='ck_json_acquiring_cert_plugins_configuration_json') THEN
        ALTER TABLE dbo.acquiring_cert_plugins ADD CONSTRAINT ck_json_acquiring_cert_plugins_configuration_json CHECK (configuration_json is null or (configuration_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.iso8583_network_profiles'::regclass AND conname='ck_json_iso8583_network_profiles_mandatory_fields_json') THEN
        ALTER TABLE dbo.iso8583_network_profiles ADD CONSTRAINT ck_json_iso8583_network_profiles_mandatory_fields_json CHECK (mandatory_fields_json is null or (mandatory_fields_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.iso8583_network_profiles'::regclass AND conname='ck_json_iso8583_network_profiles_field_mappings_json') THEN
        ALTER TABLE dbo.iso8583_network_profiles ADD CONSTRAINT ck_json_iso8583_network_profiles_field_mappings_json CHECK (field_mappings_json is null or (field_mappings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.iso8583_network_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.iso8583_network_profiles'::regclass AND conname='ck_json_iso8583_network_profiles_response_code_map_json') THEN
        ALTER TABLE dbo.iso8583_network_profiles ADD CONSTRAINT ck_json_iso8583_network_profiles_response_code_map_json CHECK (response_code_map_json is null or (response_code_map_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.network_host_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.network_host_profiles'::regclass AND conname='ck_json_network_host_profiles_settings_json') THEN
        ALTER TABLE dbo.network_host_profiles ADD CONSTRAINT ck_json_network_host_profiles_settings_json CHECK (settings_json is null or (settings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.network_message_journal') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.network_message_journal'::regclass AND conname='ck_json_network_message_journal_fields_json') THEN
        ALTER TABLE dbo.network_message_journal ADD CONSTRAINT ck_json_network_message_journal_fields_json CHECK (fields_json is null or (fields_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.enterprise_connector_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.enterprise_connector_profiles'::regclass AND conname='ck_json_enterprise_connector_profiles_settings_json') THEN
        ALTER TABLE dbo.enterprise_connector_profiles ADD CONSTRAINT ck_json_enterprise_connector_profiles_settings_json CHECK (settings_json is null or (settings_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_evaluations') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.risk_evaluations'::regclass AND conname='ck_json_risk_evaluations_hits_json') THEN
        ALTER TABLE dbo.risk_evaluations ADD CONSTRAINT ck_json_risk_evaluations_hits_json CHECK (hits_json is null or (hits_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_model_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.risk_model_profiles'::regclass AND conname='ck_json_risk_model_profiles_feature_set_json') THEN
        ALTER TABLE dbo.risk_model_profiles ADD CONSTRAINT ck_json_risk_model_profiles_feature_set_json CHECK (feature_set_json is null or (feature_set_json)::jsonb IS NOT NULL);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.gljournalentries') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.gljournalentries'::regclass AND conname='ck_gljournalentries_chainhash') THEN
        ALTER TABLE dbo.gljournalentries ADD CONSTRAINT ck_gljournalentries_chainhash CHECK ((chainsequence=0) or (length(entryhash)=64 and length(previoushash)>=7));
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.risk_model_profiles') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='dbo.risk_model_profiles'::regclass AND conname='ck_risk_model_threshold_order') THEN
        ALTER TABLE dbo.risk_model_profiles ADD CONSTRAINT ck_risk_model_threshold_order CHECK (review_threshold between 0 and 100 and decline_threshold between 0 and 100 and review_threshold <= decline_threshold);
    END IF;
END
$$;

-- End migration 041.

-- migration 042_end_to_end_transaction_failure_recovery_certification.sql
-- ============================================================================

-- Durable unknown-outcome journal, SQL-backed stand-in velocity, certification evidence.

DO $$
BEGIN
    IF to_regclass('dbo.transactionrecoverysnapshots') IS NULL THEN
        CREATE TABLE dbo.transactionrecoverysnapshots
        (
            id                       uuid NOT NULL
                CONSTRAINT pk_transactionrecoverysnapshots PRIMARY KEY
                DEFAULT gen_random_uuid(),
            correlationid            varchar(64) NOT NULL,
            sourcenodeid             varchar(64) NOT NULL,
            sinknodeid               uuid NOT NULL,
            stan                     varchar(12) NOT NULL CONSTRAINT df_trs_stan DEFAULT '',
            rrn                      varchar(12) NOT NULL CONSTRAINT df_trs_rrn DEFAULT '',
            originalmti              varchar(4) NOT NULL,
            originaldataelement     varchar(42) NOT NULL,
            protectedreversalpayload text NOT NULL,
            status                   varchar(24) NOT NULL CONSTRAINT df_trs_status DEFAULT 'Pending',
            attemptcount             integer NOT NULL CONSTRAINT df_trs_attempt DEFAULT 0,
            forwardedat              timestamptz NOT NULL CONSTRAINT df_trs_forwarded DEFAULT clock_timestamp(),
            nextattemptat            timestamptz NOT NULL CONSTRAINT df_trs_next DEFAULT clock_timestamp(),
            resolvedat               timestamptz NULL,
            lastresponsecode         varchar(8) NOT NULL CONSTRAINT df_trs_response DEFAULT '',
            lasterror                varchar(1000) NOT NULL CONSTRAINT df_trs_error DEFAULT '',
            updatedat                timestamptz NOT NULL CONSTRAINT df_trs_updated DEFAULT clock_timestamp(),
            rowversion               bytea NOT NULL,
            CONSTRAINT ux_transactionrecoverysnapshots_correlationid UNIQUE (correlationid),
            CONSTRAINT ck_transactionrecoverysnapshots_status CHECK (status IN ('Pending','TimedOut','RetryScheduled','Reversed','Resolved','Failed')),
            CONSTRAINT ck_transactionrecoverysnapshots_attemptcount CHECK (attemptcount >= 0),
            CONSTRAINT ck_transactionrecoverysnapshots_ode CHECK (length(originaldataelement) = 42)
        );
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_trs_due
    ON dbo.transactionrecoverysnapshots (status, nextattemptat, forwardedat, attemptcount)
    INCLUDE (correlationid, sinknodeid, stan, sourcenodeid);

CREATE INDEX IF NOT EXISTS ix_trs_sinknode
    ON dbo.transactionrecoverysnapshots (sinknodeid, status, forwardedat);

DO $$
BEGIN
    IF to_regclass('dbo.transactionrecoverysnapshots') IS NOT NULL
       AND to_regclass('dbo.sinknodes') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_transactionrecoverysnapshots_sinknodes')
    THEN
        ALTER TABLE dbo.transactionrecoverysnapshots
            ADD CONSTRAINT fk_transactionrecoverysnapshots_sinknodes
            FOREIGN KEY (sinknodeid) REFERENCES dbo.sinknodes(id);
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.standinvelocityevents') IS NULL THEN
        CREATE TABLE dbo.standinvelocityevents
        (
            id         bigint GENERATED BY DEFAULT AS IDENTITY
                CONSTRAINT pk_standinvelocityevents PRIMARY KEY,
            panhash    varchar(128) NOT NULL,
            binprefix  varchar(8) NOT NULL CONSTRAINT df_sive_binprefix DEFAULT '',
            occurredat timestamptz NOT NULL CONSTRAINT df_sive_occurredat DEFAULT clock_timestamp()
        );
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_sive_panhash_bin_occurredat
    ON dbo.standinvelocityevents (panhash, binprefix, occurredat);

DO $$
BEGIN
    IF to_regclass('dbo.transactionrecoverycertificationruns') IS NULL THEN
        CREATE TABLE dbo.transactionrecoverycertificationruns
        (
            runid           uuid NOT NULL CONSTRAINT pk_transactionrecoverycertificationruns PRIMARY KEY,
            environment     varchar(32) NOT NULL,
            buildversion    varchar(64) NOT NULL CONSTRAINT df_trcr_build DEFAULT '',
            startedat       timestamptz NOT NULL,
            completedat     timestamptz NULL,
            totalcases      integer NOT NULL CONSTRAINT df_trcr_total DEFAULT 0,
            passedcases     integer NOT NULL CONSTRAINT df_trcr_passed DEFAULT 0,
            failedcases     integer NOT NULL CONSTRAINT df_trcr_failed DEFAULT 0,
            skippedcases    integer NOT NULL CONSTRAINT df_trcr_skipped DEFAULT 0,
            evidencesha256  char(64) NOT NULL CONSTRAINT df_trcr_hash DEFAULT '',
            createdby       varchar(128) NOT NULL CONSTRAINT df_trcr_createdby DEFAULT 'SYSTEM',
            createdat       timestamptz NOT NULL CONSTRAINT df_trcr_createdat DEFAULT clock_timestamp()
        );
    END IF;
END
$$;

DO $$
BEGIN
    IF to_regclass('dbo.transactionrecoverycertificationcaseresults') IS NULL THEN
        CREATE TABLE dbo.transactionrecoverycertificationcaseresults
        (
            id              uuid NOT NULL CONSTRAINT pk_transactionrecoverycertificationcaseresults PRIMARY KEY DEFAULT gen_random_uuid(),
            runid           uuid NOT NULL,
            caseid          varchar(64) NOT NULL,
            category        varchar(64) NOT NULL,
            description     varchar(500) NOT NULL,
            outcome         varchar(16) NOT NULL,
            durationms      bigint NOT NULL CONSTRAINT df_trcc_duration DEFAULT 0,
            evidence        text NOT NULL CONSTRAINT df_trcc_evidence DEFAULT '',
            evidencesha256  char(64) NOT NULL CONSTRAINT df_trcc_hash DEFAULT '',
            completedat     timestamptz NOT NULL,
            CONSTRAINT fk_transactionrecoverycertificationcaseresults_run
                FOREIGN KEY (runid) REFERENCES dbo.transactionrecoverycertificationruns(runid),
            CONSTRAINT ux_transactionrecoverycertificationcaseresults_runcase
                UNIQUE (runid, caseid),
            CONSTRAINT ck_transactionrecoverycertificationcaseresults_outcome
                CHECK (outcome IN ('Pass','Fail','Skipped'))
        );
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS ix_trcc_run_outcome
    ON dbo.transactionrecoverycertificationcaseresults (runid, outcome, completedat);

CREATE INDEX IF NOT EXISTS ix_tls_correlationid_state_at
    ON dbo.transactionlifecyclestates (correlationid, newstate, occurredat DESC)
    INCLUDE (stan, sourcenodeid, previousstate, reason, latencyfromreceivedms);

CREATE INDEX IF NOT EXISTS ix_preauthrecords_stan_source
    ON dbo.preauthrecords (stan, sourcenodeid, status, createdat DESC);

COMMIT;
