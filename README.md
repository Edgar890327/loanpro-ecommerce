# LoanPro E-Commerce

An enterprise e-commerce application foundation built with .NET 10, PostgreSQL, and React. This milestone adds asynchronous, persistent error logging to the runnable application shell; product, import, checkout, and payment business logic are intentionally not included yet.

## Requirements

- .NET SDK 10
- Node.js 24 and npm
- Docker Engine with Docker Compose for the containerized setup and PostgreSQL integration tests

## Run with Docker Compose

From the repository root:

```sh
cp .env.example .env
docker compose up --build
```

Compose starts PostgreSQL, waits for its health check, then starts the API. The API applies EF Core migrations before accepting requests and serves the React build from `wwwroot`.

- Storefront and API: <http://localhost:8080>
- Health check: <http://localhost:8080/health>
- Swagger UI: <http://localhost:8080/swagger>

The checked-in `.env.example` values are local development placeholders. Replace the admin password and JWT secret in `.env` before using those settings in a real environment. Compose connects to the database by its `db` service name; the localhost connection string in `.env.example` is for running the API from the host.

Stop the stack with `docker compose down`. Add `--volumes` only when you intentionally want to delete the local PostgreSQL data.

## Run locally

Start PostgreSQL first. One option is to start only the database service with `docker compose up -d db`. Then install frontend dependencies and start Vite:

```sh
cd web
npm ci
npm run dev
```

In another terminal from the repository root, configure the local database connection and run the API:

```sh
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=store;Username=store;Password=store-local-only'
export ASPNETCORE_URLS=http://localhost:8080
dotnet run --project src/Store.Api/Store.Api.csproj --no-launch-profile
```

Vite serves the development UI (normally at <http://localhost:5173>); the API and Swagger UI are available at port 8080. `ERROR_LOG_RETENTION_DAYS` controls how long error logs are retained and defaults to 30; the admin, JWT, and reservation settings apply to later application features.

## Tests and quality checks

```sh
dotnet test tests/Store.UnitTests/Store.UnitTests.csproj -p:CollectCoverage=true -p:Threshold=80 -p:ThresholdType=line -p:ThresholdStat=total -p:ExcludeByFile="**/Program.cs%2c**/StoreDbContextFactory.cs%2c**/Migrations/*.cs%2c**/EfCoreErrorLogBatchStore.cs"
dotnet test tests/Store.IntegrationTests/Store.IntegrationTests.csproj
cd web
npm ci
npm run lint
npm run test:run
npm run build
```

The PostgreSQL integration tests use Testcontainers and require a working Docker daemon. The unit-test command enforces an 80% line-coverage threshold; it excludes the process entry point, design-time context factory, generated migrations, and the EF batch store whose database operations are covered by PostgreSQL integration tests. GitHub Actions runs the backend build, unit and PostgreSQL integration tests, frontend lint, frontend smoke test, and production frontend build.

## Architecture and decisions

- The solution contains `Store.Domain`, `Store.Application`, `Store.Infrastructure`, and `Store.Api`, plus unit and integration test projects. `Application` depends on `Domain`; `Infrastructure` depends on `Application`. The API references `Application` and `Infrastructure`; its Infrastructure reference is limited to composition-root registration of the database.
- Nullable reference types are enabled and compiler warnings are treated as errors repository-wide.
- EF Core and Npgsql are wired through `StoreDbContext`. Migrations are applied automatically at API startup. The current migration set creates the error-log infrastructure table without introducing business tables.
- The API returns ProblemDetails for unhandled errors and non-success status codes, exposes `/health` with a PostgreSQL connectivity check, publishes Swagger UI, writes structured Serilog request logs, and assigns a GUID `X-Correlation-ID` to each request. A valid incoming correlation ID is preserved.
- Unhandled exceptions return ProblemDetails with the correlation ID and enter a bounded in-memory `Channel<ErrorLog>`. A hosted worker writes logs to PostgreSQL in batches through independently created EF Core contexts; persistence failures are reported through Serilog without changing the request response. Error records contain a generic message and route template, not request bodies, authorization headers, query strings, exception messages, or raw paths. `ERROR_LOG_RETENTION_DAYS` controls background deletion of older entries and defaults to 30.
- The `ErrorLog` table stores UTC timestamps, severity, correlation ID, exception type and stack trace, HTTP method, route template, status code, and optional order and payment-provider keys. Timestamp and correlation ID are indexed.
- The frontend uses Vite, React, TypeScript, React Router, TanStack Query, and react-i18next. English (`en-US`) and Spanish (`es`) are configured without adding translated product content in this foundation milestone.
- Docker uses separate frontend build, .NET publish, and ASP.NET runtime stages. Compose exposes the app on port 8080 and gates startup on PostgreSQL health. An end-to-end Compose profile is left for milestone 9 rather than adding placeholder test services now.
- The currency, payment, shipping, inventory, catalog, and import policies remain for their respective implementation milestones; this scaffold does not introduce behavior or defaults for them.

## Alternatives considered

- **Composition root:** keeping API dependent only on Application would require an indirect or reflective registration mechanism for the database. The API therefore references Infrastructure only to register EF Core at startup; Infrastructure still depends inward on Application and never on API.
- **Frontend hosting:** a separate frontend container and reverse proxy would provide independent scaling, but serving the static Vite build from the API keeps this milestone to the requested `db` and `app` services.
- **Migration startup:** manual migration commands during deployment give operators more rollout control. Automatic migrations are used for this single-instance challenge setup so a fresh Compose deployment starts end to end; production multi-instance deployments should move migrations to a controlled deployment step.
- **Database readiness:** retry loops in application code duplicate orchestration and complicate failure reporting. Compose health-gates API startup; API startup fails explicitly if the database or migration is unavailable.
- **Error persistence:** a PostgreSQL Serilog sink is convenient for general-purpose log shipping, but it does not provide the application-specific error schema, bounded asynchronous handoff, or isolated batch writer required here. Writing errors through the request's primary `StoreDbContext` would couple logging success to request scope and database transactions; a channel-backed worker with its own contexts keeps persistence decoupled from request handling.
