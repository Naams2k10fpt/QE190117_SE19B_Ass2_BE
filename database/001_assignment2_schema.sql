-- Extend the existing database after backup; keep all business data.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

CREATE TABLE IF NOT EXISTS public."SystemAccount" (
    "AccountID" SERIAL PRIMARY KEY,
    "FullName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(255) NOT NULL UNIQUE,
    "PasswordHash" TEXT NOT NULL,
    "Role" SMALLINT NOT NULL DEFAULT 0,
    "CreatedDate" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "CK_SystemAccount_Role" CHECK ("Role" IN (0, 1)),
    CONSTRAINT "CK_SystemAccount_Email" CHECK ("Email" = LOWER(BTRIM("Email")) AND "Email" <> '')
);

CREATE TABLE IF NOT EXISTS public."RefreshToken" (
    "RefreshTokenID" SERIAL PRIMARY KEY,
    "AccountID" INTEGER NOT NULL,
    "SessionID" UUID NOT NULL,
    "TokenHash" VARCHAR(64) NOT NULL UNIQUE,
    "CreatedDate" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ExpiresDate" TIMESTAMPTZ NOT NULL,
    "RevokedDate" TIMESTAMPTZ NULL,
    CONSTRAINT "FK_RefreshToken_SystemAccount" FOREIGN KEY ("AccountID")
        REFERENCES public."SystemAccount" ("AccountID") ON DELETE CASCADE,
    CONSTRAINT "CK_RefreshToken_Hash" CHECK ("TokenHash" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "CK_RefreshToken_Expiry" CHECK ("ExpiresDate" > "CreatedDate")
);

CREATE INDEX IF NOT EXISTS "IX_RefreshToken_AccountID"
    ON public."RefreshToken" ("AccountID");
CREATE INDEX IF NOT EXISTS "IX_RefreshToken_SessionID"
    ON public."RefreshToken" ("SessionID");

ALTER TABLE public."Task"
    ADD COLUMN IF NOT EXISTS "CreatedByID" INTEGER NULL
    CONSTRAINT "FK_Task_SystemAccount"
        REFERENCES public."SystemAccount" ("AccountID") ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS "IX_Task_CreatedByID"
    ON public."Task" ("CreatedByID");

COMMIT;
