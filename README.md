# TaskTrack API · PRN232 Assignment 2

Public ASP.NET Core 8 Web API for managing departments, projects, tasks, and tags. The solution has three projects: `TaskTrack.API` for HTTP endpoints, `TaskTrack.Service` for business rules, and `TaskTrack.Repo` for EF Core database access.

## Database ERD

![TaskTrack PostgreSQL entity relationship diagram](docs/erd.svg)

Assignment 2 reuses the existing PostgreSQL database on Render. Back it up before extending its schema with SQL updates. Do not rerun the supplied `TaskManagementDB_Postgres (1).sql` initialization script: it starts with DROP TABLE statements. `TaskTag` implements the many-to-many relationship between tasks and tags. The ERD currently shows the base schema; update it when the account schema is added.

## Run locally

1. Use the existing PostgreSQL database and preserve its data. Back it up before applying Assignment 2 schema updates; do not rerun the initialization script.
2. Apply `database/001_assignment2_schema.sql` once after backup. Set `ConnectionStrings__DefaultConnection` or `DATABASE_URL` and a `JWT_SECRET` of at least 32 UTF-8 bytes in your environment (or local ASP.NET Core user secrets). Keep real credentials out of Git.
3. Set `FRONTEND_URL` to the allowed frontend origin, without a trailing slash. It defaults to `http://localhost:3000` for local development.
4. From the repository root, run `dotnet restore` and `dotnet run --project TaskTrack.API --launch-profile http`.

Swagger is available locally at `http://localhost:5117/swagger`. This project has its own user secrets ID; configure its database connection separately.

## Deployment

The Render backend and Vercel frontend URLs are pending. Set `DATABASE_URL` to the existing database and `FRONTEND_URL` to the deployed Assignment 2 frontend origin when deployment is configured.

## API behavior

- All business GET endpoints and search remain public. POST, PUT and DELETE require Staff (role 0) or Admin (role 1).
- Department/project deletion is rejected when child records exist; tag deletion is rejected while any task uses the tag.
- Task DELETE sets `IsActive` to false and preserves `CreatedByID`. Task creation takes its creator from the authenticated JWT, ignoring client input.
- Register creates only Staff and hashes passwords with PBKDF2. Login issues 15-minute access tokens and stores only hashes of 7-day refresh tokens.
- `GET /api/auth/me` returns the current account. All `/api/accounts` endpoints require Admin. Account deletion returns 409 if the account created any task, including a soft-deleted task.
- Role changes revoke the account's sessions. JWT authentication checks the live account, current role and active session, so deleted accounts and revoked privileges cannot keep using old tokens.
- CORS allows the origin configured by `FRONTEND_URL`, or the local frontend by default.

See [tests/README.md](tests/README.md) for HTTP/database regression checks. GitHub Actions builds the API and regression runner on every push; live integration checks run separately with a configured database.
