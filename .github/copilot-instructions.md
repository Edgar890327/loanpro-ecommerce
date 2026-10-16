# Project Guidelines

## Architecture

- Keep the backend in Clean Architecture layers: `Store.Domain` contains business entities and value objects; `Store.Application` contains use cases, validation, contracts, and ports; `Store.Infrastructure` implements persistence and external-service ports; `Store.Api` contains HTTP endpoints, middleware, authentication, and composition-root wiring.
- Keep dependencies pointing inward. Application and Domain must not reference Infrastructure or Api. API endpoint handlers must not query `StoreDbContext` or implement business rules; put use cases behind Application contracts and implement persistence in Infrastructure.
- Organize backend code by feature first and responsibility second (for example `Application/Catalog/Queries`, `Infrastructure/Catalog`, `Api/Endpoints/Catalog`). Keep entities, request/response contracts, validators, and persistence configurations in their respective files and folders.
- Put no more than one top-level type (class, record, interface, enum, or struct) in a source file. Name each file after its single type. Do not combine unrelated declarations for convenience.
- Keep migrations generated and located in `Store.Infrastructure/Migrations`; do not hand-edit snapshots except when required by an intentional schema change.

## Frontend

- Organize `web/src` by feature. Keep app bootstrap and routing in `app/`, reusable API/auth/i18n utilities in `shared/`, and feature-specific API hooks, types, and UI together under `features/<feature>/`.
- Build UI with atomic composition: reusable primitives in `components/atoms`, composed controls in `components/molecules`, feature sections in `components/organisms`, shared page layouts in `components/templates`, and route-level screens in `pages`. Promote a component only when it has a clear reuse or composition role.
- Keep one React component or one primary exported type per file. Use descriptive file names, colocate feature-specific tests, and keep shared components independent of feature implementations.
- All user-facing text must use the existing `react-i18next` translations in English (`en-US`) and Spanish (`es`). Render user data as text; never use `dangerouslySetInnerHTML`.

## Conventions

- Write code and documentation in English. Do not add source-code comments.
- Keep secrets in environment variables; update `.env.example` when adding configuration and never commit real credentials.
- Preserve existing behavior and public API contracts during structural refactors. Update namespaces/imports and directly related documentation together with file moves.
- Add or update focused tests for behavior changes. Finish a milestone with unit, integration, and frontend tests plus applicable lint/build checks passing before committing.
- Use small Conventional Commits, such as `refactor: organize catalog by feature`.

## Build and Test

- Backend unit tests: `dotnet test tests/Store.UnitTests/Store.UnitTests.csproj -p:CollectCoverage=true -p:Threshold=80 -p:ThresholdType=line -p:ThresholdStat=total` with repository-standard `ExcludeByFile` entries documented in `README.md`.
- Backend integration tests: `dotnet test tests/Store.IntegrationTests/Store.IntegrationTests.csproj` (requires Docker for PostgreSQL Testcontainers).
- Frontend checks from `web/`: `npm run test:run`, `npm run lint`, and `npm run build`.
- Keep the README architecture and testing guidance synchronized with intentional structural or workflow changes.
