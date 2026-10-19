# LoanPro E-Commerce

An enterprise e-commerce application built with .NET 10, PostgreSQL, and React. The application provides a public product catalog with searchable filters and product details, role-based accounts (Customer and Admin) with cookie sessions, product administration, asynchronous persistent error logging, auditable CSV product imports, guest checkout with expiring inventory reservations, and extensible simulated/PayPal payment capture.

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

The checked-in `.env.example` values are local development placeholders. Replace `ADMIN_EMAIL`, `ADMIN_PASSWORD`, and the JWT secret in `.env` before using those settings in a real environment. Compose connects to the database by its `db` service name; the localhost connection string in `.env.example` is for running the API from the host.

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
export ADMIN_EMAIL='admin@example.com'
export ADMIN_PASSWORD='replace-before-use'
export JWT_SECRET='replace-with-a-random-secret-of-at-least-32-characters'
export ASPNETCORE_URLS=http://localhost:8080
dotnet run --project src/Store.Api/Store.Api.csproj --no-launch-profile
```

Vite serves the development UI (normally at <http://localhost:5173>); the API and Swagger UI are available at port 8080. `ERROR_LOG_RETENTION_DAYS` controls how long error logs are retained and defaults to 30. `ADMIN_EMAIL` and `ADMIN_PASSWORD` seed the administrator account on first startup (an existing account is never modified). Configure a random `JWT_SECRET` of at least 32 bytes. `JWT_EXPIRY_MINUTES` defaults to 30 and is limited to 1–60 minutes. `AUTH_COOKIE_NAME` (default `store_session`) names the session cookie and `AUTH_RATE_LIMIT_PER_MINUTE` (default 10) limits attempts on `/auth` per IP and per email. Reservations expire after `RESERVATION_TTL_MINUTES` (default 15); `RESERVATION_TTL_SECONDS` is an optional override for short-lived test environments. `RESERVATION_SCAN_INTERVAL_SECONDS` defaults to 15. `Shipping__FlatRateUsd` seeds the initial flat USD shipping amount and defaults to 0.00; later changes are persisted in Settings. The simulated payment provider and flat-rate shipping strategy are active on first startup. Configure providers and shipping from **Admin → Settings**; secret fields are write-only and never returned by the API.

## Tests and quality checks

```sh
dotnet test tests/Store.UnitTests/Store.UnitTests.csproj -p:CollectCoverage=true -p:Threshold=80 -p:ThresholdType=line -p:ThresholdStat=total -p:ExcludeByFile="**/Program.cs%2c**/StoreDbContextFactory.cs%2c**/Migrations/*.cs%2c**/EfCoreErrorLogBatchStore.cs%2c**/Endpoints/**/*.cs%2c**/ProductImportService.cs%2c**/ProductCatalogReader.cs%2c**/ProductAdministration.cs%2c**/ErrorLogReader.cs%2c**/DatabaseHealthReader.cs%2c**/Orders/OrderService.cs%2c**/Orders/ReservationExpirationService.cs%2c**/Orders/FlatRateShippingStrategy.cs%2c**/Orders/ShippingStrategyRegistry.cs%2c**/Orders/ShippingQuoteService.cs%2c**/Payments/PaymentService.cs%2c**/Payments/PaymentProviderRegistry.cs%2c**/Payments/EfCoreXmlRepository.cs%2c**/Payments/DatabaseKeyManagementOptions.cs%2c**/Payments/PaymentProviderException.cs%2c**/Payments/SimulatedPaymentOptions.cs%2c**/Payments/PaymentCaptureView.cs%2c**/Payments/PaymentCreationView.cs%2c**/Payments/PaymentProviderSettingInput.cs%2c**/Payments/PaymentProviderView.cs%2c**/Payments/PaymentServiceResult.cs%2c**/Settings/ConfigurationFieldDescriptor.cs%2c**/Settings/ShippingCalculation.cs%2c**/Settings/ShippingContext.cs%2c**/Settings/ShippingItem.cs%2c**/Settings/ShippingStrategyConfiguration.cs%2c**/Settings/ShippingStrategyDescriptor.cs%2c**/Settings/ShippingStrategyView.cs%2c**/Settings/ShippingStrategySettingInput.cs%2c**/Settings/IShippingStrategyRegistry.cs%2c**/Settings/ShippingQuote.cs%2c**/Settings/IShippingQuoteService.cs%2c**/Payments/PaymentProviderContext.cs%2c**/Payments/PaymentProviderDescriptor.cs%2c**/Entities/ImportBatch.cs%2c**/Entities/StockMovement.cs%2c**/Products/Administration/*.cs%2c**/Catalog/CatalogCategoryResponse.cs%2c**/Catalog/CatalogPage.cs%2c**/Catalog/CatalogProductResponse.cs%2c**/ErrorLogging/ErrorLogPage.cs%2c**/ErrorLogging/ErrorLogQuery.cs%2c**/Products/Importing/ProductImportReport.cs%2c**/Products/Importing/ProductImportResult.cs%2c**/Products/Importing/ProductImportSummary.cs%2c**/Products/Importing/ProductImportRowReport.cs"
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
- EF Core and Npgsql are wired through `StoreDbContext`. Migrations are applied automatically at API startup. The catalog migration adds categories and products with decimal precision, a normalized unique SKU index, soft-delete filtering, and PostgreSQL `xmin` optimistic concurrency. The `CustomerOrders` migration cannot attach legacy guest orders to a customer, so it deletes existing orders, order items, payments, and order-linked stock movements and resets `Reserved` to zero before making `customer_id` mandatory.
- Identity uses a single `users` table with a `Customer` or `Admin` role. The administrator is seeded idempotently from `ADMIN_EMAIL` and `ADMIN_PASSWORD`; passwords are hashed with the ASP.NET Core `PasswordHasher`. `POST /auth/register` creates a customer and signs in, `POST /auth/login` is shared by both roles, `POST /auth/logout` clears the session, and `GET /me` returns id, name, role, and preferred locale. A duplicate email is detected through the unique index on the normalized email and returns 409. Login answers identically for an unknown email and a wrong password and verifies a dummy hash to equalize timing.
- The session is a short-lived JWT in an `HttpOnly`, `SameSite=Lax` cookie (`Secure` whenever the request is HTTPS; behind a TLS-terminating proxy, enable forwarded headers). Because the cookie is sent automatically, every `POST`, `PUT`, `PATCH`, and `DELETE` must carry `X-Requested-With: store`; otherwise the API answers 403 with a ProblemDetails body. The React HTTP client adds the header and the session context is built from `GET /me`.
- Policies: `AdminOnly` covers product CRUD, import, Settings, and the error log; `CustomerOnly` covers purchases. Catalog, search, and shipping quotes are public. `/auth` is rate limited per IP and per email (429 `RATE_LIMITED`). Emails, passwords, tokens, and addresses are never written to logs or to the error log. Auth error codes (`INVALID_CREDENTIALS`, `EMAIL_TAKEN`, `RATE_LIMITED`, `FORBIDDEN`, `SESSION_EXPIRED`) are stable and translated in the client.
- Product CRUD and the read-only `GET /api/admin/error-logs` endpoint require the `AdminOnly` authorization policy. Product writes validate names, SKU, category, price, stock, weight, and maximum field lengths in the Application layer.
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
- Checkout requires a signed-in Customer (guests and admins cannot buy). `POST /api/cart/validate` stays public and read-only. `POST /api/orders` takes the customer id from the session token, never from the body, plus an `Idempotency-Key` header that is unique per `(customer_id, key)`: two customers may reuse the same key and get two orders, while a retry by the same customer returns the original order. The reservation still runs in one PostgreSQL transaction with an atomic `UPDATE ... WHERE stock - reserved >= @q`.
- Every order gets an `order_number` from the PostgreSQL sequence `OrderNumberSequence`, formatted `ORD-YYYY-000123` and unique. The contact email and the shipping address (recipient, line 1, line 2, city, region, postal code, country) are frozen on the order, as are each item's product name and SKU, so later profile or catalog edits never change history. `GET /api/orders/{id}` and cancel are owner-only; another customer's order answers `404`, never `403`. The original idempotency key is stored only as a SHA-256 hash.
- Reserved orders default to a 15-minute TTL. A hosted service claims expired orders with `FOR UPDATE SKIP LOCKED`, marks them `Expired`, decrements `Reserved`, and records a `ReservationExpired` stock movement in the same transaction. Guest cancellation and payment failure use the same transactional release path and audit movement. Expiration and cancellation never decrement physical `Stock`.
- Shipping is behind the Application `IShippingStrategy` port and a DI-backed registry. Strategies receive product quantities and database-derived weights, subtotal, destination, and schema-validated configuration. `POST /api/shipping/quote` is public, reads product prices and weights from PostgreSQL, and returns an amount in USD; checkout re-evaluates the rate from the submitted delivery address when creating the order.
- `/api/admin/settings` requires the Admin JWT and returns payment-provider and shipping-strategy descriptors and schemas. Administrators can save configuration, enable implementations, and select one active implementation in each group. A PostgreSQL partial unique index and transaction-scoped advisory lock enforce one active strategy; the active strategy cannot be disabled. Secret configuration is Data Protection-encrypted, omitted from responses, and preserved when no replacement value is submitted. The React Settings page renders fields from descriptors in both languages; a new implementation registers its descriptor and component/strategy without hard-coded form markup.
- The selected shipping cost is computed inside order creation from server-side cart data and the guest destination, then persisted on `Order.Shipping`. Payment totals and PayPal breakdowns use this frozen amount, so later Settings changes affect subsequent quotes and orders only.
- The storefront now has a local-storage cart, inventory validation before checkout, structured shipping address form (recipient name and contact email prefilled from the account); anonymous visitors are redirected to `/login?returnUrl=/checkout` with the cart kept in local storage, and the cart is revalidated against stock when checkout opens, reservation countdown, and order status page. Its payment component registry selects the active provider key at runtime: simulated checkout captures through the API, while PayPal loads the JavaScript SDK using only the public client ID and creates/captures Orders API v2 payments through the server. Quantity changes are capped to the last known available stock; the server rechecks inventory atomically and reports a conflict if stock changes between validation and order creation.
- Orders use a single payment row keyed by order and permanently bind the payment attempt to the provider used to create it. `POST /api/orders/{id}/payment` and `/payment/capture` are `CustomerOnly` and look the order up by id and the token's customer id, so a foreign order answers `404`, an admin `403`, and a visitor `401`; capture checks that the reservation remains payable, validates the provider's USD amount against the frozen order total, and converts reserved stock into sold stock in the same database transaction. PayPal creates item and shipping breakdowns in USD, caches OAuth tokens, retries an expired token once, and uses stable `PayPal-Request-Id` values for create/capture idempotency. A failed payment marks the order failed and releases its reservation.
- `PaymentProviderSetting` persists public configuration and Data Protection-encrypted provider secrets. The ASP.NET Core Data Protection XML key ring is persisted in PostgreSQL as well, allowing secrets to be decrypted after restarts. A transaction-scoped PostgreSQL advisory lock serializes provider changes, and a partial unique index plus startup validation enforce exactly one active provider. Public checkout endpoints return descriptors and public configuration only; admin settings require the Admin JWT and never return secret values.
- `dryRun=true` returns the planned report without changing products, categories, import batches, or stock movements. Applied imports record a SHA-256 file hash, UTC creation time, row and summary counts, and the importing admin's user id (`ImportedByUserId`, a foreign key to `Users`; batches created before accounts existed keep it null) in `ImportBatches`; each applied product quantity is recorded as an `Import` `StockMovement` linked to its batch. An unconfirmed repeated hash returns HTTP 409 and the report; explicit confirmation creates a new auditable batch and applies the stock again.
- The checked-in CSV fixture is derived from the provided workbook. Its golden integration test verifies 95 data rows: 87 products created, 121,737 stock units applied, 6 warnings, 5 rejected rows, and 1 exact duplicate.
- The frontend uses Vite, React, TypeScript, React Router, TanStack Query, and react-i18next. `web/src/app` owns routing and bootstrap, `features/<feature>` owns route pages and feature types, `components/atoms|molecules|organisms|templates` contains composed UI building blocks, and `shared` contains API/auth/i18n/validation utilities. Screens compose shared atoms, molecules, and organisms instead of duplicating them. All user-facing UI text is available in English (`en-US`) and Spanish (`es`).
- The active UI language is restored from local storage; on first visit, the browser language selects Spanish when available and otherwise falls back to `en-US`. Currency, numbers, and dates use `Intl` with the active locale while all prices remain USD. API ProblemDetails include stable error codes, and the client translates known codes instead of displaying server prose; unexpected errors retain their correlation ID as a support reference.
- Admin order history is available at `GET /api/admin/orders` with bounded paging and a projection containing status, provider key, timestamps, and totals without customer contact details. The admin error-log screen reuses `GET /api/admin/error-logs` for date, level, and correlation-ID filters, paging, and an expandable diagnostic detail view. Both screens require the administrator JWT; available stock is displayed separately from physical stock and flagged when it is five units or fewer.
- Shared skeletons, a global render error boundary, and dismissible accessible notifications provide consistent loading, failure, and success feedback. The responsive shell supports keyboard navigation, a skip link, visible focus, semantic table and pagination landmarks, labeled controls, and reduced-motion preferences.
- Each C# source file declares one top-level type and is named after that type. Keep React route screens and reusable UI components in their own files; colocate feature tests with the feature and keep shared models in feature-owned type folders.
- Docker uses separate frontend build, .NET publish, and ASP.NET runtime stages. Compose exposes the app on port 8080 and gates startup on PostgreSQL health. The production runtime image runs as the non-root user with UID 1654.
- The currency is fixed to USD. Adding a backend provider requires an `IPaymentProvider` implementation registered through DI; adding a storefront checkout requires a React component registered by provider key. Both implementations consume provider-neutral order and capture models.

## End-to-end tests

The Playwright suite contains 16 serial scenarios, each named with its catalog ID: E2E-01 admin login, E2E-02 product CRUD, E2E-03 catalog search/filter/shareable pagination, E2E-04 simulated purchase, E2E-05 declined payment, E2E-06 PayPal with stubs, E2E-07 active provider switching, E2E-08 shipping changes and frozen order totals, E2E-09 bilingual locale persistence, E2E-10 CSV golden import, E2E-11 duplicate SKU and confirmed reimport, E2E-12 stock limits, E2E-13 reservation expiry, E2E-14 last-unit concurrency, E2E-15 safe rendering of malicious-looking text, and E2E-16 admin order history.

### Run the complete suite in Docker

From the repository root:

```sh
npm --prefix e2e run docker:e2e
```

This builds and starts the E2E application, isolated PostgreSQL service, and local PayPal API stub, then runs Playwright in the matching browser image. The E2E overlay sets a 12-second reservation TTL and enables `POST /e2e/reset`; the endpoint is not mapped by the normal Compose configuration. Each test resets and reseeds the database, so do not point the E2E profile at data that must be kept. Playwright runs serially because tests share that database.

To run only one scenario inside Docker after the first `docker:e2e` invocation has brought the stack up:

```sh
docker compose -f docker-compose.yml -f docker-compose.e2e.yml --profile e2e run --rm e2e sh -c 'node wait-for-app.mjs && npm test -- --grep "E2E-06"'
```

To start the stack without running the suite, use `npm --prefix e2e run docker:up`. The Docker runner waits for `/health` before executing. Test results and the HTML report are mounted back to `e2e/test-results` and `e2e/playwright-report`; open the latter with `npm --prefix e2e run report`.

For Playwright UI mode in Docker, start the E2E stack and expose the UI at <http://localhost:9323>:

```sh
npm --prefix e2e run docker:up
npm --prefix e2e run docker:ui
```

### Run Playwright on the host

The same E2E Docker profile provides the API, database, fixture reset endpoint, and PayPal stub. Start it first:

```sh
npm --prefix e2e run docker:up
cd e2e
npm ci
npx playwright install chromium
E2E_BASE_URL=http://127.0.0.1:8080 npm test
```

Run one scenario by ID, open Playwright UI mode, and view the generated report with:

```sh
npm run test:scenario -- "E2E-06"
npm run ui -- --grep "E2E-06"
npm run report
```

The tests intercept the PayPal JavaScript SDK in the browser and send backend Orders API calls to the local stub; no real PayPal account or sandbox credentials are needed. A manual real-sandbox test requires PayPal sandbox credentials entered through Admin → Settings, with the sandbox option enabled and the real PayPal API URL left configured.

The Playwright config records traces, screenshots, and videos only for failed tests, and writes an HTML report. `CI=true` enables one retry and forbids focused tests. E2E-only environment variables are `E2E_MODE` (must be true to map the reset endpoint), `E2E_ADMIN_PASSWORD`, `E2E_JWT_SECRET`, `E2E_BASE_URL`, and `PAYPAL_API_BASE_URL` (the E2E overlay points it to the stub). The checked-in Compose defaults are local-only placeholders, not production credentials.

## Test commands

- Unit tests: `dotnet test tests/Store.UnitTests/Store.UnitTests.csproj`
- PostgreSQL integration tests: `dotnet test tests/Store.IntegrationTests/Store.IntegrationTests.csproj` (requires Docker for Testcontainers)
- Frontend checks: from `web/`, run `npm ci`, `npm run lint`, `npm run test:run`, and `npm run build`
- End-to-end suite: `npm --prefix e2e run docker:e2e`, or use the host Playwright commands above

## Architecture extensions

To add a payment provider, implement the provider-neutral `IPaymentProvider` contract in Infrastructure, provide its descriptor and configuration schema, register it in dependency injection, and add one checkout component registered under the same provider key in the React checkout registry. Keep secrets in the provider's secret configuration fields so Settings encrypts them and public responses omit them.

To add a shipping strategy, implement `IShippingStrategy`, describe its schema, register it in dependency injection, and persist selection/configuration through the existing Settings registry. The generic Settings UI renders the schema; order creation passes server-derived item weights, quantities, subtotal, and destination, then freezes the returned USD charge on the order.

## Known limitations

The application has no customer accounts/authentication, tax calculation, payment webhooks, multi-currency support, or refunds. PayPal E2E coverage uses deterministic local doubles rather than a live sandbox.

## Sample CSV provenance

The checked-in CSV fixture was derived from the challenge workbook provided with the project. The original download date was not recorded; the fixture was added to the repository on 2026-10-15. Its golden integration test verifies 95 data rows: 87 products created, 121,737 stock units applied, 6 warnings, 5 rejected rows, and 1 exact duplicate.

## AI use

AI assistance was used to implement and review portions of this challenge. The final code, tests, architecture choices, and documentation were reviewed and validated in the repository.

## Alternatives considered

| Concern | Chosen approach | Alternative considered | Reason |
|---|---|---|---|
| Error persistence | Bounded `Channel<ErrorLog>` and batch worker with independent contexts | PostgreSQL Serilog sink or writing through the request `DbContext` | Keeps the required schema and request path independent of batch persistence failures and request transactions. |
| Product concurrency | PostgreSQL `xmin` optimistic token | Application-managed integer version | PostgreSQL already changes `xmin` on updates, avoiding a second counter. |
| Catalog search | Accent-normalized substring text with `pg_trgm` GIN index | Full-text search or unindexed `ILIKE` | Preserves substring matching across name/SKU/description while indexing typical searches. |
| CSV parsing | CsvHelper with strict headers and explicit limits | Hand-written parser or rejecting the whole file | Handles quoting and multiline values correctly and reports invalid rows individually. |
| Product imports | Atomic SQL stock increments, file hash, batch and movement audit | Read-modify-write or silent duplicate reimport | Prevents lost updates and makes repeat stock additions explicit and auditable. |
| Admin authentication | Configured password exchanged for a short-lived JWT | HTTP Basic or persisted admin credentials | Avoids resending the long-lived password and avoids another credential store. |
| Inventory reservation | Conditional updates in one transaction with `SKIP LOCKED` expiry worker | Read stock then write, or a single uncoordinated timer | Prevents overselling, partial reservations, and duplicate expiry processing across instances. |
| Shipping | Database-backed `IShippingStrategy` registry and schema-driven Settings | Hard-coded flat rate or environment-only values | Allows runtime configuration and future weight/destination logic; the quote is frozen on each order. |
| Payments | Provider-neutral Strategy contract, DI registry, and React component registry | Provider-specific branches in order endpoints or checkout pages | A provider can be added as one server class and one keyed UI component without changing order logic. |
| Payment settings | Encrypted database settings and persisted Data Protection keys | Environment variables or ephemeral key ring | Enables provider switching at runtime and preserves decryption across restarts. |
| PayPal | Browser SDK for approval and server-owned Orders API v2 calls | Direct card handling or hosted redirect | Limits payment-data exposure while retaining embedded approval. |
| API errors and locale | Stable ProblemDetails codes translated by the client with `Intl` formatters | Server-localized prose and hard-coded formatting | Keeps the API language-neutral and formatting consistent across locales. |
| E2E reset | Endpoint mapped only when `E2E_MODE=true`, backed by a fixture | General-purpose reset route or shared live sandbox | Isolates repeatable tests without exposing destructive reset behavior in normal execution. |
| E2E PayPal | Browser SDK interception plus local backend HTTP stub | Live PayPal sandbox for automated runs | Makes CI deterministic and independent of external credentials and network state. |

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
- **Reservation safety:** reading stock into application memory and later writing the new reserved total can oversell under concurrent checkouts. Per-product conditional SQL updates inside one transaction serialize concurrent claims at PostgreSQL and ensure no partial reservation survives a failed cart. The `xmin` token continues to protect admin edits, while a database check constraint protects the invariant from all write paths.
- **Idempotency coordination:** relying only on a unique index would detect a duplicate request after it had already attempted reservation and would require recovering from a failed transaction. A transaction-scoped PostgreSQL advisory lock serializes requests sharing a hashed key before checking the persisted order; the unique hash index remains as a final integrity constraint.
- **Expiry processing:** a single in-memory timer or an uncoordinated query could have multiple API instances release the same order. `FOR UPDATE SKIP LOCKED` claims bounded batches safely across instances and performs state change, reservation release, and audit writes atomically.
- **Shipping policy and settings:** embedding a flat shipping constant in checkout or reading only environment variables keeps deployment simple, but requires code changes or restarts for live configuration and cannot account for a destination or item weights. A database-backed `IShippingStrategy` registry and public schema-driven Settings UI allow runtime changes and new weight/destination strategies without changing order rules. A dedicated shipping table keeps its active selection and encrypted settings separate from payment-provider settings; the single-active partial index and startup invariant prevent an invalid store configuration. Shipping is quoted from server-owned product data and frozen on each order rather than recalculated during payment.
- **Payment extensibility:** branching on provider names inside order endpoints or checkout pages couples payment behavior to transport and UI code. The `IPaymentProvider` Strategy contract, DI registry, provider-neutral models, and React checkout registry let a new provider be added as a server class and a matching UI component without changing order rules. PayPal credentials are stored encrypted in the database instead of environment-specific code/config files so an administrator can switch the active provider at runtime; the simulated provider is the safe default for local development. A single payment row per order makes create/capture idempotency and provider binding explicit; supporting multiple attempts would require a separate attempt-history model and defined retry/refund semantics.
- **Payment settings and key persistence:** keeping only one provider's settings in environment variables is simple for deployment automation, but changing providers would require a restart and deployment. Settings are persisted with one-active-provider enforcement; Data Protection keys are persisted in PostgreSQL to keep encrypted values readable after restart. A production deployment with a shared external key ring or managed key vault could replace this repository without changing the provider contract.
- **PayPal checkout:** collecting card data directly would expand PCI scope and expose sensitive payment data to the store. The PayPal JavaScript SDK handles payer approval, while the server owns OAuth credentials, order creation, and capture. A hosted redirect checkout is a possible alternative, but the SDK preserves the challenge's embedded checkout experience.
- **Locale and API errors:** hard-coded currency/date patterns would drift between screens and locales, so browser `Intl` formatters are shared and keyed by the persisted UI language. Server-localized prose would couple API responses to a user's language and make clients parse message text; stable ProblemDetails codes keep the API language-neutral and let the UI translate known cases. Browser-language detection is only the initial default and never overrides a saved language.
- **Admin history and diagnostics:** returning full order entities could expose guest contact data and couple the UI to persistence, so the endpoint returns a small paged projection. The existing authenticated error-log query is reused instead of creating a second diagnostics store; stack traces are fetched only for administrators. A dedicated telemetry platform could provide richer search and alerting at larger scale, but would add operational services beyond the challenge's database-backed logging requirement.
