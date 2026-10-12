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
