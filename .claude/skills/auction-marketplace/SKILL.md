---
name: auction-marketplace
description: Conventions and workflows for the Online Auction Marketplace ("Bidly") repo, a location-first C2C/B2C/B2B auction platform built as a .NET 10 modular monolith (api, realtime, worker) with a Next.js web app, PostgreSQL + PostGIS, Redis and OpenSearch. Use when changing code in this repo, adding a module, migration, endpoint or cross-module event, running or verifying the stack, or checking a product rule (proxy bidding, soft close, deal agreement, location privacy, no escrow).
---

# Auction Marketplace

A location-first auction marketplace for C2C, B2C and B2B. The specs in `docs/` are the source of truth for product behaviour. This skill covers how to change the code without breaking the architecture or the product rules.

- Product rules (bidding, deal agreement, location privacy): [reference/domain-rules.md](reference/domain-rules.md)
- Where each topic lives in the specs: [reference/spec-map.md](reference/spec-map.md)
- How the repo was created, step by step: [docs/04-project-setup.md](../../../docs/04-project-setup.md)

## Layout

| Path | What |
|---|---|
| `src/Marketplace.Api` | HTTP API, port 5080. The only process that runs migrations. |
| `src/Marketplace.Realtime` | SignalR, port 5081. Hub: `/hubs/auctions` (`Watch`/`Unwatch` → group `auction:{id}`; signed-in users auto-join `user:{id}`). `RedisRealtimeSubscriber` forwards pushes from Redis. |
| `src/Marketplace.Worker` | `OutboxRelay` (calls `OutboxProcessor`) and `JobRunner` (every `IBackgroundJob`, e.g. `auctions.close`). |
| `src/Marketplace.Bootstrap` | `ModuleCatalog.All`, `AddMarketplace()` (data source, event registry, outbox processor, modules), Redis publisher. The only project that references every module. |
| `src/Marketplace.SharedKernel` | <ul><li>`IModule`, `IClock`, `ProblemException`, `PostgresHealthCheck`</li><li>`Persistence/`: `ModuleDbContext` with `Publish()`, `AddModuleDbContext<T>`, design-time factory, `DatabaseMigrator`</li><li>`Outbox/`: outbox, inbox, `OutboxProcessor`</li><li>`Events/`: `[IntegrationEvent]`, `IntegrationEventHandler<TEvent,TContext>`, `IRealtimeEvent`, `RealtimeGroups`, `Handles.Mask`</li><li>`Auth/`: `ICurrentUser`, `DevTokenIssuer`, JWT setup</li><li>`Geo/`: `GeoPoints`, `PublicGrid`</li><li>`Jobs/`: `IBackgroundJob`</li></ul> |
| `src/Marketplace.Media` | Standalone image-processing library (`IImageProcessor`, `ProcessedImage`) used for listing photos. SkiaSharp and its native types stay internal. |
| `src/Modules/Marketplace.Modules.<Name>` | One project per module. Live: Identity, Location, Catalog, Auctions, Orders, Search. Shells: Billing, Reviews, Messaging, Notifications, Admin. Folders: `Domain/` `Application/` `Persistence/` (`DbContext`, configurations, `Migrations/`) `Endpoints/` + `<Name>Module.cs`. |
| `src/Modules/Marketplace.Modules.<Name>.Contracts` | Public events + query interfaces: Location (`ILocationService`), Catalog (`ListingPublished`, `IListingQueries`), Auctions (`AuctionStarted`, `BidPlaced`, `AuctionClosed`), Orders (`OrderUpdated`). |
| `dotnet-tools.json` | Pins `dotnet-ef` 10.0.12. Run `dotnet tool restore` once. |
| `tests/Marketplace.ArchitectureTests` | Enforces the module rules below |
| `tests/Marketplace.UnitTests` | Domain rules + proxy-bid property tests (`InternalsVisibleTo` from Auctions and Orders) |
| `tests/Marketplace.IntegrationTests` | `MarketplaceFixture`: api in-process on Testcontainers PostGIS, `FakeClock`, `CapturingRealtimePublisher`, `DrainOutboxAsync()`, `CloseDueAuctionsAsync()`, `SignInAsync(name)`; helpers in `Scenario.cs` |
| `tests/Marketplace.Api.Tests` | In-process API smoke tests (no database) |
| `tests/Marketplace.Media.Tests` | Image-processing checks for `Marketplace.Media` |
| `web/tests/*.cjs` | Node test runner tests for web helpers: `node --test tests/*.cjs` from `web/` |
| `.github/workflows/ci.yml` | CI: full backend suite (including Testcontainers) plus web typecheck, tests and build |
| `web/` | Next.js App Router, port 3000. Server components call `lib/api.ts`; client components call `lib/client.ts` → `/api/proxy/*` (BFF adds the token from the httpOnly cookie); realtime via `lib/realtime.ts` `useRealtime()`. |
| `scripts/seed-demo.ps1` | Demo data through the public API (`-ShortAuctions` for 5-minute auctions) |
| `docs/`, `mockup/` | Specs (v0.2) and the clickable vanilla-JS prototype (port 5173) |

## Architecture rules

Don't work around them. Rules 1, 2 and 5 are enforced by `tests/Marketplace.ArchitectureTests`; rules 3, 4 and 6 and "implementation types stay internal" are not tested yet, so they depend on review.

1. **A module owns one Postgres schema**, named after `IModule.Name` (lowercase), modelled **code-first** by exactly one `<Name>DbContext : ModuleDbContext`. Every entity it maps lives in that schema. Never map another module's tables.
2. **A module never references another module's project.** If module A needs to call B, create `Marketplace.Modules.B.Contracts` (interfaces and DTOs only) and reference that.
3. **Side effects across modules go through outbox events.** Call `db.Publish(new SomethingHappened(...), clock.UtcNow)` before the same `SaveChanges` as the state change, and let the worker relay it. Example: `AuctionClosed` makes Orders create the order and deal agreement, Catalog end the listing, and Search mark the card sold. Consumers derive from `IntegrationEventHandler<TEvent, TContext>`, which gives inbox-backed exactly-once effects.
4. **Time comes from `IClock`** and never from `DateTime.Now` or `DateTimeOffset.UtcNow`. The server clock is the only time source for bids.
5. **The model and its migrations must match.** A test fails on drift. Generate migrations with `dotnet ef`, review them like code, and never edit one after it's been applied anywhere; add a new one instead. Only `api` applies migrations (`Database:MigrateOnStartup`).
6. Keep package versions in `Directory.Packages.props`. A csproj uses `<PackageReference Include="X" />` with no `Version`.

## Workflows

### Run and verify
**Secrets first:** run `pwsh -File scripts/init-env.ps1`. It creates `.env`, which compose reads, and regenerates each host's git-ignored `appsettings.Development.json` from it; `dotnet run` and `dotnet ef` read those files through standard .NET configuration (there is no `.env` loader in .NET). Never put a password or key in the committed `appsettings.json`, `docker-compose.yml` or code. To add a secret:
- add it to `.env.example` as `NAME=change-me`;
- reference it in compose as `${NAME:?...}`;
- write it into the hosts' JSON in `init-env.ps1`.

**Postgres is native, not Docker.** A local PostgreSQL with PostGIS runs on the host at 5432. The database is code-first end to end: `DatabaseMigrator` creates it if missing (`IRelationalDatabaseCreator`), then applies every module's migrations (PostGIS included), so the `.env` user needs CREATEDB plus the right to create the postgis extension (the `postgres` superuser locally). Don't add SQL setup scripts. On a fresh database EF logs `fail:` for the missing `__ef_migrations_history` tables before creating them; that's expected. The host is configurable for another server: `POSTGRES_HOST` (default `localhost`, written into `appsettings.Development.json` by `init-env.ps1`) and `POSTGRES_HOST_FROM_DOCKER` for compose (default `host.docker.internal`, i.e. this machine). Only the integration tests run Postgres in a container (Testcontainers). Never add a `postgres` service back to compose.

**Whole stack in Docker** (web 3000, api 5080, realtime 5081, worker, redis; Postgres on the host):
```bash
docker compose up -d --build
```
After changing a service, rebuild just that one with `docker compose up -d --build <service>`. Read logs with `docker compose logs -f <service>`. The .NET images come from the root `Dockerfile` (build arg `PROJECT`), and the web image from `web/Dockerfile` (Next.js standalone). If you add a new host project, add a compose service with its `PROJECT` arg.

**Visual Studio "Docker Compose" launch:** `docker-compose.dcproj` (VS only; the dotnet CLI skips it) plus `Properties/launchSettings.json` at the repo root, which debugs api, realtime and worker in their containers. Each host csproj has `DockerComposeProjectPath` and `DockerfileFile` pointing at the shared root `Dockerfile`; VS fast mode builds its `base` stage and mounts the build output. A new host project needs the same csproj properties and a `serviceActions` entry. Keep one Dockerfile: don't accept VS's per-project generated Dockerfiles.

**Local hot reload:** stop the app containers first, because they use the same ports (`docker compose stop web api realtime worker`). Then:
```bash
docker compose up -d --wait redis
dotnet run --project src/Marketplace.Api
```
Run `dotnet run` for `src/Marketplace.Realtime` and `src/Marketplace.Worker` in their own terminals, and `npm run dev` in `web/`, as needed. Check `http://localhost:5080/health/ready` (should return `Healthy`) and `/v1/modules`. If Docker reports that `dockerDesktopLinuxEngine` can't be found, start Docker Desktop and wait for `docker info` to succeed. To see what's applied, run `docker compose logs api | Select-String Migrated`, or query one module's history:
`psql -h localhost -U marketplace -d marketplace -c "SELECT * FROM auctions.__ef_migrations_history"`

### Add a module
```bash
pwsh -File .claude/skills/auction-marketplace/scripts/new-module.ps1 -Name Disputes -Summary "Disputes: ... (feature design §N)."
```
The script:
- creates the project, the `<Name>Module` class (with a `Schema` const), `Persistence/<Name>DbContext` and its design-time factory;
- adds the project to the solution and Bootstrap, and registers it in `ModuleCatalog.All`;
- generates the `InitialCreate` migration (schema + outbox).

Then run `dotnet test`. Also add the module to system design §2 if it's a real new bounded context.

### Change the database (code-first)
1. Add or change the entity: a plain class, usually in `Domain/`.
2. Map it in `Persistence/Configurations/<Entity>Configuration.cs` (`IEntityTypeConfiguration<T>`, picked up automatically). Use `ToTable("singular_snake")`; columns become snake_case by convention. Add a `DbSet<T>` to the module's DbContext if you query it directly.
3. Generate the migration:
   ```bash
   dotnet ef migrations add <PascalCaseIntent> --project src/Modules/Marketplace.Modules.<Name> --startup-project src/Marketplace.Api --context <Name>DbContext --output-dir Persistence/Migrations
   ```
4. Review the generated `Up`/`Down`, then run `dotnet test` (the drift test must pass).
5. The api applies it on its next start: `docker compose up -d --build api`, or `dotnet run`.

Geo columns use NetTopologySuite `Point` with `HasColumnType("geography (point, 4326)")`. PostGIS is already enabled. For row locks (bidding), use `context.Auctions.FromSql($"SELECT * FROM auctions.auction WHERE id = {id} FOR UPDATE")` inside `Database.BeginTransactionAsync()`.

To undo the last migration before it's applied anywhere, run `dotnet ef migrations remove` with the same `--project`, `--startup-project` and `--context` flags.

### Give a module real logic
Follow the target layout in `docs/05-architecture.md` §3.3:
- `Domain/` holds pure rules (no I/O; time is passed in).
- `Application/` holds use cases (transaction, lock, domain call, `SaveChanges` with the outbox).
- `Persistence/` holds the DbContext, `Configurations/` and `Migrations/`.
- `Endpoints/` holds HTTP.
- `Events/` holds handlers for other modules' events.
- Add tests to the shared projects in a `<Name>/` folder: `tests/Marketplace.UnitTests` for domain rules, `tests/Marketplace.IntegrationTests` for persistence, events and HTTP. Don't create per-module test projects.
- Everything stays `internal` except the module class and `*.Contracts`.

### Add an integration event and its handler
1. Add the record to the publisher's `*.Contracts` (create the project with `new-module.ps1 -Name <Name> -Contracts`):
   ```csharp
   [IntegrationEvent("orders.deal_paid.v1")]
   public sealed record DealPaid(Guid OrderId, ...) : IIntegrationEvent;
   ```
   Implement `IRealtimeEvent` if browsers should hear about it, with public data only.
2. Publish it inside the state change: `db.Publish(new DealPaid(...), clock.UtcNow);`, then `SaveChanges`.
3. Consumer module: reference the Contracts project (`dotnet add src/Modules/Marketplace.Modules.X reference src/Modules/Marketplace.Modules.Y.Contracts/...csproj`). Write a handler:
   ```csharp
   internal sealed class DoSomething(XDbContext db, IClock clock) : IntegrationEventHandler<DealPaid, XDbContext>(db, clock)
   { protected override async Task HandleAsync(DealPaid e, CancellationToken ct) { /* change Db, maybe Db.Publish(...) */ } }
   ```
   Register it in `AddServices`: `services.AddIntegrationEventHandler<DealPaid, DoSomething>();`.
4. Throw to retry later (e.g. data from another event hasn't arrived yet). Never call `SaveChanges` in the handler; the base class does it with the inbox row.
5. Test it in `Marketplace.IntegrationTests` with `app.DrainOutboxAsync()`.

### Add a background job
Implement `IBackgroundJob` (`Name`, `Interval`, `RunOnceAsync`), claim rows with `FOR UPDATE SKIP LOCKED`, keep it idempotent, and register it with `services.AddBackgroundJob<T>()`. Only the worker runs jobs; tests call `RunOnceAsync`.

### Add an endpoint
Add a static `Endpoints/<Name>Endpoints.Map(app)` and call it from the module's `MapEndpoints`. Group under `/v1/<resource>` as in the API sketch in system design §7, and add `.WithTags("<Name>")`.
- Use `.RequireAuthorization()` for signed-in endpoints, and inject `ICurrentUser` (`current.RequireId()`, `current.Handle`).
- Rule violations: `throw ProblemException.BadRequest/Forbidden/NotFound/Conflict("stable_code", "Message")`. The api maps these to problem details.
- Writes that two users can race on lock the row first (see `AuctionsDbContext.LockAuctionAsync`, `OrderService.MutateAsync`).
- Keep implementation types `internal`; only the module class and `Contracts` are public.
- Never return private data: proxy maxima other than the caller's own, the reserve price to buyers, exact coordinates or addresses.

### Add tests
- Pure domain logic goes in `tests/Marketplace.UnitTests`. Add `[assembly: InternalsVisibleTo("Marketplace.UnitTests")]` to the module if needed. Use seeded `Random` for property-style tests so failures reproduce.
- Anything touching Postgres, events or HTTP goes in `tests/Marketplace.IntegrationTests` (`[Collection(MarketplaceCollection.Name)]`, which shares one PostGIS container and one `FakeClock`).
  - Use `app.SignInAsync("alice")`, `app.PublishAsync(client, ...)`, `client.BidAsync(...)`, `app.EndAuctionAsync(...)` and `response.ReadAsync()` / `ProblemCodeAsync(...)`.
  - Time only moves forward (`Clock.Set` to the future), and every test makes its own listings.

## Definition of done
- `dotnet build` adds no warnings beyond the existing baseline (currently EF1002 in `OutboxProcessor.cs`; warnings aren't errors, so compare the output yourself), and `dotnet test Marketplace.slnx` is green. CI runs the same suite.
- The behaviour matches the spec section (see the spec map). If the code had to differ, update the spec in the same change and add a row to the product spec's §10 decision log.
- Location privacy holds: no exact address or exact coordinates in any public response.

## Gotchas
- **EF child entities with code-generated Guid keys** (e.g. `DealEvent` appended to `Order.Events`) need `Property(x => x.Id).ValueGeneratedNever()`. Otherwise EF treats them as existing rows, issues an UPDATE and throws `DbUpdateConcurrencyException`.
- **EF can't sort by a member of a positional record** in a projection. Project into a member-initialised class instead (see `SearchEndpoints.CardWithDistance`).
- **Raw SQL with locks:** `FromSql($"… FOR UPDATE").ToListAsync()` with no further LINQ, so EF doesn't wrap it in a subquery. The reserved table name needs quotes: `orders."order"`.
- **Auth tokens use real time**, not `IClock`. The JWT validator uses the system clock, and a fake test clock would make tokens "not yet valid".
- **Web hydration:** don't format dates or times during SSR in client components (server time zone ≠ browser). Use `components/LocalTime.tsx`, and `Countdown` renders "…" until mounted.
- **Handlers are internal**, so the outbox processor calls them through the `IIntegrationEventHandler<T>` interface by reflection. Don't use `dynamic` across assemblies.
- **EF Core design time:** `dotnet ef` uses the module's internal `<Name>DbContextFactory`, which reads `ConnectionStrings:Postgres` from the Api's `appsettings.json` + `appsettings.Development.json`, then the environment. Adding a migration needs no database; `database update` does. The Api is the startup project because it references `Microsoft.EntityFrameworkCore.Design`.
- `UseNetTopologySuite()` makes every module's migration ensure the `postgis` extension (idempotent). Don't add `HasPostgresExtension("postgis")` yourself.
- Each module has its own `__ef_migrations_history` in its schema, so migration names only need to be unique within a module.
- **Central package management vs templates:** `dotnet new xunit` (and other templates) write `Version=` on PackageReference. With CPM on, that fails with NU1008. Move the version into `Directory.Packages.props`.
- **Don't name a type `Modules`** anywhere under `Marketplace.*`, because it collides with the `Marketplace.Modules` namespace. That's why the catalogue is `ModuleCatalog`.
- `Microsoft.AspNetCore.OpenApi` 10.0.0 pulled a vulnerable `Microsoft.OpenApi`. Keep ASP.NET packages on the latest 10.0.x patch (`dotnet list package --outdated`).
- `dotnet new` on .NET 10 creates `.slnx` solutions and auto-adds new projects to a solution in the current folder.
- Configuration in compose comes from environment variables (`ConnectionStrings__Postgres`, `Cors__Origins__0`, `API_URL`). Inside the network, use service names (`redis`, `api:8080`), never `localhost`. Postgres is on the host: `host.docker.internal`. If containers can't connect, check the host's `listen_addresses` and `pg_hba.conf`.
- The NuGet cache mount in the `Dockerfile` needs `sharing=locked`. Parallel compose builds corrupt a shared cache otherwise.
- The web image uses `npm ci`, so update `web/package-lock.json` whenever `package.json` changes. Without local Node, run: `docker run --rm -v "${PWD}/web:/app" -w /app node:22-alpine npm install --package-lock-only`.
- The Testing environment has no connection string. `NpgsqlDataSource` is resolved lazily, so only code that touches the database needs it.
