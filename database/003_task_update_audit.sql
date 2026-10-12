-- Add update audit without changing legacy records.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
ALTER TABLE public."Task"
    ADD COLUMN IF NOT EXISTS "UpdatedByID" INTEGER NULL
    CONSTRAINT "FK_Task_UpdatedBy_SystemAccount"
        REFERENCES public."SystemAccount" ("AccountID") ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS "IX_Task_UpdatedByID" ON public."Task" ("UpdatedByID");
COMMIT;
