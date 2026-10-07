-- ============================================================
-- CardFeeRules
-- ============================================================

CREATE TABLE IF NOT EXISTS dbo.cardfeerules
(
    id uuid NOT NULL,
    feetype varchar(32) NOT NULL,
    scopetype varchar(16) NOT NULL,
    scopevalue varchar(64) NOT NULL,
    iswaiver boolean NOT NULL DEFAULT false,
    feeid uuid NULL,
    isactive boolean NOT NULL DEFAULT true,
    description varchar(400) NOT NULL DEFAULT '',
    createdat timestamptz NOT NULL,

    CONSTRAINT pk_cardfeerules
        PRIMARY KEY (id),

    CONSTRAINT fk_cardfeerules_fees
        FOREIGN KEY (feeid)
        REFERENCES dbo.fees (id)
);

-- ============================================================
-- Lookup Index
-- ============================================================

CREATE INDEX IF NOT EXISTS ix_cardfeerules_lookup
    ON dbo.cardfeerules
    (
        feetype,
        scopetype,
        scopevalue,
        isactive
    );