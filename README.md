# TaskTrack API · PRN232 Assignment 2

Public ASP.NET Core 8 Web API for managing departments, projects, tasks, and tags. The solution has three projects: `TaskTrack.API` for HTTP endpoints, `TaskTrack.Service` for business rules, and `TaskTrack.Repo` for EF Core database access.

## Database ERD

![TaskTrack PostgreSQL entity relationship diagram](docs/erd.svg)

Assignment 2 reuses the existing PostgreSQL database on Render. Back it up before extending its schema with SQL updates. Do not rerun the supplied `TaskManagementDB_Postgres (1).sql` initialization script: it starts with DROP TABLE statements. `TaskTag` implements the many-to-many relationship between tasks and tags. The ERD currently shows the base schema; update it when the account schema is added.

## Run locally

1. Use the existing PostgreSQL database and preserve its data. Back it up before applying Assignment 2 schema updates; do not rerun the initialization script.
2. Set `ConnectionStrings__DefaultConnection` or `DATABASE_URL` in your environment (or use local ASP.NET Core user secrets). `.env.example` documents the variables; .NET does not load a `.env` file automatically. Keep real credentials out of Git.
3. Set `FRONTEND_URL` to the allowed frontend origin, without a trailing slash. It defaults to `http://localhost:3000` for local development.
4. From the repository root, run `dotnet restore` and `dotnet run --project TaskTrack.API --launch-profile http`.

Swagger is available locally at `http://localhost:5117/swagger`. This project has its own user secrets ID; configure its database connection separately.

## Deployment

The Render backend and Vercel frontend URLs are pending. Set `DATABASE_URL` to the existing database and `FRONTEND_URL` to the deployed Assignment 2 frontend origin when deployment is configured.

## API behavior

- Departments and projects support public CRUD and search; deletion is rejected when child records exist.
- Tasks support public CRUD and multi-criteria search. DELETE soft-deletes a task by setting `IsActive` to false.
- Tags support public CRUD; deletion is rejected while any task uses the tag.
- CORS allows the origin configured by `FRONTEND_URL`, or the local frontend by default.

JWT authentication, role-based authorization, and account management are not implemented yet.
