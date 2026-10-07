-- ============================================================
-- v26 - Advanced Tier-1 routing criteria for EFT Switch
--
-- Adds routing by country, MCC, currency, device, interchange,
-- card range, institution, product, network and account number
-- while keeping legacy BIN routing compatible.
-- ============================================================

-- Drop legacy filtered/unique index if it exists.
-- PostgreSQL equivalent of SQL Server ux_routes_binprefix_active.

DROP INDEX IF EXISTS dbo.ux_routes_binprefix_active;


-- ------------------------------------------------------------
-- Add Priority
-- SQL Server: int NOT NULL DEFAULT(0)
-- PostgreSQL: integer NOT NULL DEFAULT 0
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS priority integer NOT NULL DEFAULT 0;


-- ------------------------------------------------------------
-- Add CountryCodes
-- SQL Server: nvarchar(max)
-- PostgreSQL: text
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS countrycodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add MerchantCategoryCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS merchantcategorycodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add CurrencyCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS currencycodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add DeviceCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS devicecodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add InterchangeCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS interchangecodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add CardRangePrefixes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS cardrangeprefixes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add InstitutionCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS institutioncodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add ProductCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS productcodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add NetworkCodes
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS networkcodes text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Add AccountRanges
-- ------------------------------------------------------------

ALTER TABLE dbo.routes
    ADD COLUMN IF NOT EXISTS accountranges text NOT NULL DEFAULT '';


-- ------------------------------------------------------------
-- Advanced routing lookup index
--
-- SQL Server:
-- CREATE INDEX IX_Routes_AdvancedLookup
-- ON dbo.Routes(IsActive, Priority DESC, BinPrefix);
--
-- PostgreSQL equivalent:
-- ------------------------------------------------------------

CREATE INDEX IF NOT EXISTS ix_routes_advancedlookup
    ON dbo.routes
    (
        isactive,
        priority DESC,
        binprefix
    );