# API regression checks

Run from the repository root against a separate local API process:

```powershell
dotnet run --project TaskTrack.API --no-launch-profile --urls http://127.0.0.1:5127
dotnet run --project tests/TaskTrack.Checks
```

The checks read `DATABASE_URL` and `JWT_SECRET` from the API's development User Secrets or environment variables. `TEST_API_URL` can select another test API using that same database and JWT configuration. Do not commit credentials.

The runner registers temporary Staff accounts and directly inserts a temporary Admin with the application's PBKDF2 hasher. It exercises public reads/search, all business write routes, validation/not-found responses, guest 401, Staff 403, Admin accounts, role revocation, JWT rejection, task creator and soft-delete. Refresh checks cover rotation, absolute expiry, replay, simultaneous refresh, expired tokens and logout revocation. It cleans only fixtures with a unique generated prefix and compares hashes of all business rows before/after. Run while other users are not editing that database; concurrent edits intentionally fail the snapshot check.

API deletion remains soft-delete for tasks. The regression runner physically removes its own temporary task rows only during cleanup, so its temporary accounts and projects can also be removed. It never removes legacy tasks.

Bonus checks cover profile validation and account/role spoofing, password validation, PBKDF2 replacement, revocation of every session, simultaneous password changes and a login verified before a password change. Audit checks verify creator/updater IDs, ignored client spoofing, soft-delete tracking and deletion of a non-creator updater without losing the task.
# Health readiness checks

Run `node tests/health-checks.mjs` with `TEST_API_URL` set to the API origin. It checks public access, the readiness body, timestamp, `no-store`, and absence of diagnostic fields. The default expected response is 200. To verify the failure branch, start an isolated API instance with an unreachable test database, set `TEST_HEALTH_STATUS=503`, and run the same check. Do not modify production database configuration to induce failure.

# Deletion history and undo checks

After applying `database/004_deletion_history.sql`, run `node tests/deletion-checks.mjs` with `TEST_API_URL`, `DATABASE_URL` and optionally `TEST_PSQL_PATH` configured privately. This suite checks all five resources, original IDs and task links, Staff/Admin permissions, private snapshots, exact 24-hour expiry, parent/name/email conflicts, account session revocation and concurrent restoration. A controlled row-lock test crosses the deadline to verify a queued request cannot restore late. Time changes and cleanup affect only uniquely prefixed fixtures; all seven original tables must match their initial fingerprints. Run sequentially with other database suites and while other users are not editing. For an isolated loopback PostgreSQL clone only, `TEST_DB_SSLMODE=disable` permits testing without TLS.

