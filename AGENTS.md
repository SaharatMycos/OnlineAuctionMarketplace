# Repository guidance for Codex

## Project and sources

Online Auction Marketplace is a location-first C2C/B2C/B2B auction platform built as a .NET 10 modular monolith with a Next.js App Router web app.

Read README.md for setup and commands, docs/05-architecture.md for structure, and the relevant product and system specifications in docs/ before changing behavior. Distinguish implemented features from planned work. Update the affected documentation when behavior or architecture changes; record product decisions in docs/01-product-spec.md's decision log.

## Layout

- src/Marketplace.Api: HTTP API (5080); the only process that applies migrations.
- src/Marketplace.Realtime: SignalR (5081), hub /hubs/auctions.
- src/Marketplace.Worker: outbox relay and background jobs.
- src/Marketplace.Bootstrap: module catalogue and application composition.
- src/Marketplace.SharedKernel: shared persistence, events, auth, clock, geo and jobs.
- src/Modules/Marketplace.Modules.<Name>: Domain, Application, Persistence, Endpoints and event handlers. Public cross-module contracts live in separate *.Contracts projects.
- tests/: architecture, unit, integration and API smoke test projects.
- web/: Next.js app, BFF proxy and SignalR client. mockup/ is a separate vanilla JavaScript prototype.

## Architecture and domain rules

- Each module owns one PostgreSQL schema and one DbContext derived from ModuleDbContext. Do not map or query another module's tables.
- Modules reference other modules only through *.Contracts projects. Keep implementation types internal; Bootstrap composes the modules.
- Publish cross-module side effects through the transactional outbox in the same SaveChanges as the state change. Use the existing inbox-backed IntegrationEventHandler base for consumers; it owns SaveChanges.
- Keep pure domain rules in Domain, orchestration and transactions in Application, mappings and migrations in Persistence, and HTTP handling in Endpoints.
- Use IClock for domain time; pass time into pure domain methods. JWT validation uses real system time.
- Preserve per-auction consistency, proxy-bid rules and soft close. Lock contested writes using the existing transaction and row-lock patterns.
- Never expose private addresses, exact coordinates, other users' proxy maxima or buyer-visible reserve prices through public responses or realtime events.
- Sales payments happen outside the platform. Orders record deal agreements and confirmations; do not introduce escrow or payment processing without a product change.

## Database and configuration

- Use EF Core code-first migrations in each module's Persistence/Migrations directory. Generate and review migrations when models change; do not rewrite applied migrations. Architecture tests enforce model/migration consistency.
- Restore EF tooling with: dotnet tool restore
- Migration template: dotnet ef migrations add <Intent> --project src/Modules/Marketplace.Modules.<Name> --startup-project src/Marketplace.Api --context <Name>DbContext --output-dir Persistence/Migrations
- Normal development uses native PostgreSQL with PostGIS on the host. Integration tests use a disposable Testcontainers PostGIS database. Do not add a PostgreSQL service to Compose.
- scripts/init-env.ps1 initializes .env and regenerates ignored host appsettings.Development.json files. Preserve existing secrets; do not use -Force unless secret regeneration is requested.
- Never commit credentials or print secret configuration. Document new configuration with placeholders in .env.example and wire it through the existing configuration flow.

## Style and implementation

- Follow .editorconfig: UTF-8, LF, final newline, two-space indentation except four spaces for C#, and file-scoped C# namespaces.
- Keep .NET package versions in Directory.Packages.props; PackageReference entries omit Version. Respect global.json.
- Follow existing endpoint authorization, ICurrentUser and ProblemException conventions.
- Web server components use web/lib/api.ts; client calls use web/lib/client.ts through the BFF proxy. Keep tokens in the existing httpOnly-cookie flow.
- Use the existing LocalTime and Countdown patterns to avoid date/time hydration mismatches.
- Keep web/package-lock.json synchronized with package.json; use npm ci for reproducible installs.
- Preserve unrelated working-tree changes and keep edits focused on the requested task.

## Validation

Run commands from the repository root unless noted, choosing checks appropriate to the change:

- Backend build: dotnet build Marketplace.slnx
- Architecture and migration drift: dotnet test tests/Marketplace.ArchitectureTests
- Domain rules: dotnet test tests/Marketplace.UnitTests
- API smoke tests: dotnet test tests/Marketplace.Api.Tests
- Full backend suite: dotnet test Marketplace.slnx (Docker must be running for integration tests).
- Web, from web/: npm ci, then npm run typecheck and npm run build.

Add regression coverage for changed behavior using the existing test projects. Integration tests use the shared MarketplaceFixture, FakeClock and explicit outbox draining. Keep time moving forward and create independent test data. For documentation-only edits, verify paths and commands without starting services.

Report what changed, which checks actually ran, and any checks blocked by the environment. Never present a previous run as current validation.
