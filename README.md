# LoanPro E-Commerce

An enterprise e-commerce application built with .NET 10, PostgreSQL, and React. The current milestones provide a public product catalog with searchable filters and product details, JWT-protected product administration, asynchronous persistent error logging, and auditable CSV product imports. Checkout and payment business logic remain for later milestones.

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
export ADMIN_PASSWORD='replace-before-use'
export JWT_SECRET='replace-with-a-random-secret-of-at-least-32-characters'
export ASPNETCORE_URLS=http://localhost:8080
dotnet run --project src/Store.Api/Store.Api.csproj --no-launch-profile
```

Vite serves the development UI (normally at <http://localhost:5173>); the API and Swagger UI are available at port 8080. `ERROR_LOG_RETENTION_DAYS` controls how long error logs are retained and defaults to 30. The admin login compares against `ADMIN_PASSWORD`; configure a random `JWT_SECRET` of at least 32 bytes. `JWT_EXPIRY_MINUTES` defaults to 15 and is limited to 1–60 minutes. `RESERVATION_TTL_MINUTES` applies to the later checkout milestone.

## Tests and quality checks

```sh
dotnet test tests/Store.UnitTests/Store.UnitTests.csproj -p:CollectCoverage=true -p:Threshold=80 -p:ThresholdType=line -p:ThresholdStat=total -p:ExcludeByFile="**/Program.cs%2c**/StoreDbContextFactory.cs%2c**/Migrations/*.cs%2c**/EfCoreErrorLogBatchStore.cs%2c**/Endpoints/**/*.cs%2c**/ProductImportService.cs%2c**/ProductCatalogReader.cs%2c**/ProductAdministration.cs%2c**/ErrorLogReader.cs%2c**/DatabaseHealthReader.cs%2c**/Entities/ImportBatch.cs%2c**/Entities/StockMovement.cs%2c**/Products/Administration/*.cs%2c**/Catalog/CatalogCategoryResponse.cs%2c**/Catalog/CatalogPage.cs%2c**/Catalog/CatalogProductResponse.cs%2c**/ErrorLogging/ErrorLogPage.cs%2c**/ErrorLogging/ErrorLogQuery.cs%2c**/Products/Importing/ProductImportReport.cs%2c**/Products/Importing/ProductImportResult.cs%2c**/Products/Importing/ProductImportSummary.cs%2c**/Products/Importing/ProductImportRowReport.cs"
dotnet test tests/Store.IntegrationTests/Store.IntegrationTests.csproj
cd web
npm ci
npm run lint
npm run test:run
npm run build
```

The PostgreSQL integration tests use Testcontainers and require a working Docker daemon. The unit-test command enforces an 80% line-coverage threshold; it excludes the process entry point, design-time context factory, generated migrations, HTTP and database adapters covered by integration tests, and transport/persistence data shapes. GitHub Actions runs the backend build, unit and PostgreSQL integration tests, frontend lint, frontend smoke test, and production frontend build.

## Architecture and decisions

- The solution contains `Store.Domain`, `Store.Application`, `Store.Infrastructure`, and `Store.Api`, plus unit and integration test projects. Backend files are grouped by layer, feature, and responsibility: entities and text helpers in Domain, catalog/error-log/health/product use-case contracts and validation in Application, persistence and feature adapters in Infrastructure, and endpoint/auth/middleware adapters in Api. `Application` depends on `Domain`; `Infrastructure` depends inward on `Application`; API handlers depend on Application contracts and do not access `StoreDbContext`. The API references Infrastructure only for composition-root registration and startup migrations.
- Nullable reference types are enabled and compiler warnings are treated as errors repository-wide.
- EF Core and Npgsql are wired through `StoreDbContext`. Migrations are applied automatically at API startup. The catalog migration adds categories and products with decimal precision, a normalized unique SKU index, soft-delete filtering, and PostgreSQL `xmin` optimistic concurrency.
- The admin API issues short-lived JWTs through `POST /auth/login`; the configured administrator password is checked using a fixed-time comparison. Product CRUD and the read-only `GET /api/admin/error-logs` endpoint require the Administrator authorization policy. Product writes validate names, SKU, category, price, stock, weight, and maximum field lengths in the Application layer.
- SKU uniqueness uses a stored normalized key: trim surrounding whitespace and uppercase invariantly. Categories are shared by a similarly normalized name. Product updates submit the current `version` (`xmin`) and receive a clear 409 conflict if another update has already changed the row. Product deletion sets `IsDeleted` and never physically removes the product.
- The public catalog is available at `GET /api/catalog` with text search over name, SKU, and description, category/price/stock filters, relevance/price/name sorting, and bounded paging. `GET /api/catalog/categories` returns visible product counts and `GET /api/catalog/{id}` returns public product details. All queries inherit the soft-delete filter, and deterministic secondary sort keys prevent items from moving between pages when sort values are equal.
- Search text and normalized product names are stored when a product is created or updated. The shared .NET normalizer removes Unicode combining marks and applies invariant uppercase normalization, so searches such as `cafe` match `Café`. User search text is escaped for SQL `LIKE` wildcard semantics and remains a parameterized EF Core query. The `SearchText` column uses a PostgreSQL `pg_trgm` GIN index to accelerate substring `ILIKE` queries; very short search terms may not benefit as much from trigram indexing. The migration uses PostgreSQL `unaccent` once to backfill existing products.
- The storefront supports shareable URL filters, debounced search, category counts, stock labels, paging, and product details in English (`en-US`) and Spanish (`es`). Product names and descriptions are rendered as normal React text, not HTML.
- Admin error-log queries support UTC date bounds, level, correlation ID, and bounded page sizes. The React admin includes a bilingual login, route guard, paginated catalog, create/edit form, delete confirmation, automatic session removal and redirect on 401, and a translated conflict message for stale edits.
- The API returns ProblemDetails for unhandled errors and non-success status codes, exposes `/health` with a PostgreSQL connectivity check, publishes Swagger UI, writes structured Serilog request logs, and assigns a GUID `X-Correlation-ID` to each request. A valid incoming correlation ID is preserved.
- Unhandled exceptions return ProblemDetails with the correlation ID and enter a bounded in-memory `Channel<ErrorLog>`. A hosted worker writes logs to PostgreSQL in batches through independently created EF Core contexts; persistence failures are reported through Serilog without changing the request response. Error records contain a generic message and route template, not request bodies, authorization headers, query strings, exception messages, or raw paths. `ERROR_LOG_RETENTION_DAYS` controls background deletion of older entries and defaults to 30.
- The `ErrorLog` table stores UTC timestamps, severity, correlation ID, exception type and stack trace, HTTP method, route template, status code, and optional order and payment-provider keys. Timestamp and correlation ID are indexed.
- Admins can upload products through `POST /api/admin/products/import` using multipart fields `file` and `dryRun`; `confirmReimport=true` explicitly authorizes adding stock again for a previously imported file. The CSV must be UTF-8 (with or without BOM), contain the exact columns `name,sku,description,category,price,stock,weight_kg`, and be at most 10 MiB and 10,000 data rows. CsvHelper handles escaped quotes, commas, and line breaks inside fields.
- Imports return a row-by-row status and messages, plus counts for products created, stock units applied, warnings, rejected rows, and exact duplicate rows. Invalid rows are reported and skipped. A `$` in a valid price is removed with a warning; empty categories become `Uncategorized`, and empty weights become null, both with warnings. A duplicate normalized SKU in one file adds its stock and retains the first row's product fields; exact copies are counted separately, while differences are reported. Existing products are matched by normalized SKU and receive an atomic stock increment only; their other fields are not overwritten. Categories are created using normalized names.
- `dryRun=true` returns the planned report without changing products, categories, import batches, or stock movements. Applied imports record a SHA-256 file hash, UTC creation time, row and summary counts, and admin identity in `ImportBatches`; each applied product quantity is recorded as an `Import` `StockMovement` linked to its batch. An unconfirmed repeated hash returns HTTP 409 and the report; explicit confirmation creates a new auditable batch and applies the stock again.
- The checked-in CSV fixture is derived from the provided workbook. Its golden integration test verifies 95 data rows: 87 products created, 121,737 stock units applied, 6 warnings, 5 rejected rows, and 1 exact duplicate.
- The frontend uses Vite, React, TypeScript, React Router, TanStack Query, and react-i18next. `web/src/app` owns routing and bootstrap, `features/<feature>` owns route pages and feature types, `components/atoms|molecules|organisms|templates` contains composed UI building blocks, and `shared` contains API/auth/i18n/validation utilities. Screens compose shared atoms, molecules, and organisms instead of duplicating them. All user-facing UI text is available in English (`en-US`) and Spanish (`es`).
- Each C# source file declares one top-level type and is named after that type. Keep React route screens and reusable UI components in their own files; colocate feature tests with the feature and keep shared models in feature-owned type folders.
- Docker uses separate frontend build, .NET publish, and ASP.NET runtime stages. Compose exposes the app on port 8080 and gates startup on PostgreSQL health. An end-to-end Compose profile is left for milestone 9 rather than adding placeholder test services now.
- The currency is fixed to USD. Checkout, shipping, inventory reservation, and payment policies remain for their respective implementation milestones; this scaffold does not introduce behavior or defaults for them.

## Alternatives considered

- **Composition root:** keeping API dependent only on Application would require an indirect or reflective registration mechanism for persistence and hosted workers. The API references Infrastructure only in its composition root to register adapters and apply migrations; route handlers consume Application ports, and Infrastructure still depends inward on Application and never on API.
- **Frontend hosting:** a separate frontend container and reverse proxy would provide independent scaling, but serving the static Vite build from the API keeps this milestone to the requested `db` and `app` services.
- **Migration startup:** manual migration commands during deployment give operators more rollout control. Automatic migrations are used for this single-instance challenge setup so a fresh Compose deployment starts end to end; production multi-instance deployments should move migrations to a controlled deployment step.
- **Database readiness:** retry loops in application code duplicate orchestration and complicate failure reporting. Compose health-gates API startup; API startup fails explicitly if the database or migration is unavailable.
- **Error persistence:** a PostgreSQL Serilog sink is convenient for general-purpose log shipping, but it does not provide the application-specific error schema, bounded asynchronous handoff, or isolated batch writer required here. Writing errors through the request's primary `StoreDbContext` would couple logging success to request scope and database transactions; a channel-backed worker with its own contexts keeps persistence decoupled from request handling.
- **Concurrency token:** an application-managed integer version would work across database providers, but PostgreSQL's `xmin` already changes on each row update and avoids maintaining a second counter. The API exposes it as an opaque unsigned `version` and uses it as EF Core's concurrency token.
- **SKU uniqueness:** relying on PostgreSQL collation for case-insensitive comparison is locale-dependent, and trimming only on input does not prevent inconsistent stored values. A normalized invariant-uppercase key and unique index make case/outer-whitespace behavior explicit and portable.
- **Catalog search:** PostgreSQL full-text search provides linguistic tokenization and ranking, but its token matching is not the requested substring search across names, SKUs, and descriptions. A plain `ILIKE` query has the desired behavior but can scan the entire product table. Persisted accent-normalized search text with `pg_trgm` GIN indexing retains substring semantics while accelerating typical searches. `unaccent` is used only for migration backfill; application writes use the same .NET normalizer as incoming queries, avoiding per-query extension calls and expression-index coupling.
- **CSV parsing and import:** a hand-written CSV parser would avoid a dependency but risks mishandling escaped quotes and multiline fields, so CsvHelper is used with strict headers, UTF-8 validation, and explicit file and row limits. Rejecting an entire file on any invalid row would make large catalog uploads harder to recover; instead, valid rows are planned and reported individually while invalid rows are skipped. Existing product rows are updated with an atomic SQL stock increment rather than read-modify-write, preventing lost updates when imports run concurrently. A file hash provides idempotency detection, but repeated imports require explicit confirmation because they intentionally add stock again. Import batches and stock movements provide an audit trail without mixing inventory history into product fields.
- **Admin authentication:** HTTP Basic authentication would resend the long-lived password on each request, while persisting admin credentials would introduce another secret store. A configured password exchanged once for a short-lived signed JWT keeps the challenge's single-admin setup small without storing the password in the database.
