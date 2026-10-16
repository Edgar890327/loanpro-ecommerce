# LoanPro E-Commerce

An enterprise e-commerce application foundation built with .NET 10, PostgreSQL, and React. This milestone establishes a runnable application shell; product, import, checkout, and payment business logic are intentionally not included yet.

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

Vite serves the development UI (normally at <http://localhost:5173>); the API and Swagger UI are available at port 8080. Set `ADMIN_PASSWORD`, `JWT_SECRET`, `JWT_EXPIRY_MINUTES`, and `RESERVATION_TTL_MINUTES` in the environment when configuring those later application features.

## Tests and quality checks

```sh
dotnet test tests/Store.UnitTests/Store.UnitTests.csproj -p:CollectCoverage=true -p:Threshold=80 -p:ThresholdType=line -p:ThresholdStat=total -p:ExcludeByFile="**/Program.cs%2c**/StoreDbContextFactory.cs%2c**/Migrations/*.cs"
dotnet test tests/Store.IntegrationTests/Store.IntegrationTests.csproj
cd web
npm ci
npm run lint
npm run test:run
npm run build
```

The PostgreSQL integration tests use Testcontainers and require a working Docker daemon. The unit-test command enforces an 80% line-coverage threshold over unit-testable middleware and database-registration code; it excludes the process entry point, design-time context factory, and generated migrations. GitHub Actions runs the backend build, unit and PostgreSQL integration tests, frontend lint, frontend smoke test, and production frontend build.

## Architecture and decisions

- The solution contains `Store.Domain`, `Store.Application`, `Store.Infrastructure`, and `Store.Api`, plus unit and integration test projects. `Application` depends on `Domain`; `Infrastructure` depends on `Application`. The API references `Application` and `Infrastructure`; its Infrastructure reference is limited to composition-root registration of the database.
- Nullable reference types are enabled and compiler warnings are treated as errors repository-wide.
- EF Core and Npgsql are wired through `StoreDbContext`. A baseline migration is applied automatically at API startup. Since this milestone has no domain entities, the migration initializes EF Core's migration history without introducing business tables.
- The API returns ProblemDetails for unhandled errors and non-success status codes, exposes `/health` with a PostgreSQL connectivity check, publishes Swagger UI, writes structured Serilog request logs, and assigns a GUID `X-Correlation-ID` to each request. A valid incoming correlation ID is preserved.
- The frontend uses Vite, React, TypeScript, React Router, TanStack Query, and react-i18next. English (`en-US`) and Spanish (`es`) are configured without adding translated product content in this foundation milestone.
- Docker uses separate frontend build, .NET publish, and ASP.NET runtime stages. Compose exposes the app on port 8080 and gates startup on PostgreSQL health. An end-to-end Compose profile is left for milestone 9 rather than adding placeholder test services now.
- The currency, payment, shipping, inventory, catalog, and import policies remain for their respective implementation milestones; this scaffold does not introduce behavior or defaults for them.

## Alternatives considered

- **Composition root:** keeping API dependent only on Application would require an indirect or reflective registration mechanism for the database. The API therefore references Infrastructure only to register EF Core at startup; Infrastructure still depends inward on Application and never on API.
- **Frontend hosting:** a separate frontend container and reverse proxy would provide independent scaling, but serving the static Vite build from the API keeps this milestone to the requested `db` and `app` services.
- **Migration startup:** manual migration commands during deployment give operators more rollout control. Automatic migrations are used for this single-instance challenge setup so a fresh Compose deployment starts end to end; production multi-instance deployments should move migrations to a controlled deployment step.
- **Database readiness:** retry loops in application code duplicate orchestration and complicate failure reporting. Compose health-gates API startup; API startup fails explicitly if the database or migration is unavailable.
