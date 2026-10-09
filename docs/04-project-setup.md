# Online Auction Marketplace: How this project was created

| | |
|---|---|
| Status | v0.1 (2026-10-09) |
| Related | [System design §1–§2](03-system-design.md) · [Root README](../README.md) · [Claude skill](../.claude/skills/auction-marketplace/README.md) |

This guide records how the repo got to its current state, so it can be rebuilt or copied for a similar project. The files in the repo are the source of truth. When a step says "write file X", copy it from the repo.

## Phase 0: Design first (no code)

1. **Spec**: [01 Product spec](01-product-spec.md), [02 Feature design](02-feature-design.md) and [03 System design](03-system-design.md).
2. **Clickable mockup** in `mockup/` (vanilla HTML/CSS/JS, hash router, simulated bidders) to try the flows before building them. It's served by `mockup/serve.ps1` on port 5173.
3. **Decisions** were made one at a time and recorded in [product spec §10](01-product-spec.md#10-decision-log), in this order:
   1. No escrow.
   2. A deal agreement instead of in-app payment.
   3. Modular monolith.
   4. Location-first.
   5. Stack: C#/.NET 10 + Next.js.

## Phase 1: Toolchain (Windows)

```bash
winget install Microsoft.DotNet.SDK.10
```

```bash
winget install Docker.DockerDesktop
```

```bash
winget install Git.Git
```

```bash
winget install OpenJS.NodeJS.LTS
```

Check with `dotnet --list-sdks` (10.0.x), `docker info` (Docker Desktop must be running) and `node --version`.

## Phase 2: Scaffold the .NET solution

Run these from an empty folder. Each step was checked with .NET SDK 10.0.401.

**2.1 Repo and root config**

```bash
git init -b main
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature
dotnet new gitignore
dotnet new editorconfig
dotnet new buildprops
dotnet new sln -n Marketplace
```

On .NET 10, `dotnet new sln` creates `Marketplace.slnx`, the XML solution format.

**2.2 Platform and host projects**

```bash
dotnet new classlib -n Marketplace.SharedKernel -o src/Marketplace.SharedKernel
dotnet new classlib -n Marketplace.Bootstrap    -o src/Marketplace.Bootstrap
dotnet new web      -n Marketplace.Api          -o src/Marketplace.Api
dotnet new web      -n Marketplace.Realtime     -o src/Marketplace.Realtime
dotnet new worker   -n Marketplace.Worker       -o src/Marketplace.Worker
```

**2.3 Modules: one class library per module (PowerShell)**

```powershell
$modules = 'Identity','Location','Catalog','Auctions','Orders','Billing','Reviews','Messaging','Notifications','Search','Admin'
foreach ($m in $modules) {
  dotnet new classlib -n "Marketplace.Modules.$m" -o "src/Modules/Marketplace.Modules.$m"
  dotnet add "src/Modules/Marketplace.Modules.$m" reference src/Marketplace.SharedKernel
  dotnet add src/Marketplace.Bootstrap reference "src/Modules/Marketplace.Modules.$m"
}
```

To add a module later, use the skill script instead: `pwsh -File .claude/skills/auction-marketplace/scripts/new-module.ps1 -Name <Name>`. It also writes the module class and the schema migration and registers the module.

**2.4 Project references.** Hosts reference only Bootstrap. Modules reference only SharedKernel.

```powershell
dotnet add src/Marketplace.Bootstrap reference src/Marketplace.SharedKernel
foreach ($h in 'Api','Realtime','Worker') { dotnet add "src/Marketplace.$h" reference src/Marketplace.Bootstrap }
```

**2.5 Test projects**

```bash
dotnet new xunit -n Marketplace.ArchitectureTests -o tests/Marketplace.ArchitectureTests
dotnet new xunit -n Marketplace.Api.Tests         -o tests/Marketplace.Api.Tests
dotnet add tests/Marketplace.ArchitectureTests reference src/Marketplace.Bootstrap
dotnet add tests/Marketplace.Api.Tests reference src/Marketplace.Api
```

**2.6 Add everything to the solution.** On .NET 10, `dotnet new` already adds most projects, and this command catches the rest. It reports "already contains" for those that are in.

```powershell
dotnet sln Marketplace.slnx add (Get-ChildItem -Recurse -Filter *.csproj).FullName
```

**2.7 Central package management, last.** Write `Directory.Packages.props` (copy it from the repo), then **remove every `Version="…"` from the `PackageReference`s in the csproj files**.

> [!NOTE]
> Order matters. If `Directory.Packages.props` exists before you run `dotnet new xunit`, the template still writes versions into the csproj, and restore fails with **NU1008** ("Projects using Central Package Management must define a Version value on a PackageVersion item"). This was reproduced while writing this guide.

Packages used: `Microsoft.AspNetCore.OpenApi`; EF Core code-first (`Microsoft.EntityFrameworkCore`, `.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `…PostgreSQL.NetTopologySuite`, `EFCore.NamingConventions`); and for tests `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.NET.Test.Sdk`, `xunit` (v2) and `xunit.runner.visualstudio` (3.x). Then run:

```bash
dotnet list Marketplace.slnx package --outdated
```

Keep ASP.NET packages on the latest 10.0.x patch. 10.0.0 pulled a `Microsoft.OpenApi` with a known high-severity vulnerability (NU1903).

## Phase 3: Write the code

Delete the template files (`Class1.cs`, `UnitTest1.cs`, the template `Worker.cs`), then write these, copying from the repo:

| Step | Files | What it does |
|---|---|---|
| 3.1 | `Directory.Build.props` | net10.0, nullable, implicit usings |
| 3.2 | `src/Marketplace.SharedKernel/` | The csproj has `FrameworkReference Microsoft.AspNetCore.App` plus EF Core, Npgsql EF, Npgsql EF NetTopologySuite and EFCore.NamingConventions. Contents: <ul><li>`IModule` (Name = schema, `AddServices`, `MapEndpoints`) and `IClock`</li><li>`Persistence/ModuleDbContext.cs` (default schema, outbox, auto-applied configurations)</li><li>`Persistence/PersistenceExtensions.cs` (`AddModuleDbContext<T>`, history table in the module schema, NTS, snake_case)</li><li>`Persistence/ModuleDesignTimeFactory.cs`</li><li>`Persistence/DatabaseMigrator.cs` (advisory lock, `MigrateAsync` per context)</li><li>`Outbox/OutboxMessage.cs`</li><li>`Health/PostgresHealthCheck.cs`</li></ul> |
| 3.3 | `src/Modules/*/` | Generated by `new-module.ps1`: `<Name>Module.cs` (`Schema` const, registers the DbContext), `Persistence/<Name>DbContext.cs` and `Persistence/<Name>DbContextFactory.cs` |
| 3.4 | `src/Marketplace.Bootstrap/` | `ModuleCatalog.All` (not named `Modules`, which collides with the namespace) and `MarketplaceSetup`: `AddMarketplace` with a lazy `NpgsqlDataSource` and NetTopologySuite, `MapMarketplaceModules`, `MigrateDatabaseAsync`. The Api csproj also references `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) as the startup project for `dotnet ef`. |
| 3.4b | Migrations | `dotnet new tool-manifest`, then `dotnet tool install dotnet-ef --version 10.0.12`, then one `dotnet ef migrations add InitialCreate --project src/Modules/Marketplace.Modules.<Name> --startup-project src/Marketplace.Api --context <Name>DbContext --output-dir Persistence/Migrations` per module |
| 3.5 | `src/Marketplace.Api/` | `Program.cs`: modules, OpenAPI, ProblemDetails, CORS, `/health/live`, `/health/ready`, `/v1/modules`, migrate on startup when `Database:MigrateOnStartup`. Also `appsettings*.json` and port 5080. |
| 3.6 | `src/Marketplace.Realtime/` | SignalR `AuctionHub` at `/hubs/auctions`, health checks, CORS with credentials, port 5081 |
| 3.7 | `src/Marketplace.Worker/` | `OutboxRelay` background service (stub) |
| 3.8 | `tests/…` | Architecture tests (no module→module references, unique lowercase names, first migration creates the module's own schema, migrations only touch their own schema) and API smoke tests (environment `Testing`, no database) |
| 3.9 | `docker-compose.yml` | `postgis/postgis:17-3.5`, `redis:7.4-alpine`, `opensearchproject/opensearch:2.19.1` (profile `search`) |
| 3.10 | `.gitattributes`, `.gitignore` additions, `README.md` | Housekeeping |

## Phase 4: Web app

The repo's `web/` was written by hand: `package.json`, `tsconfig.json`, `next.config.ts`, `app/layout.tsx`, `app/page.tsx` (status page), `app/globals.css`, `lib/api.ts` and `.env.example`. The generator equivalent is:

```bash
npx create-next-app@latest web --ts --app --eslint --no-tailwind --no-src-dir --import-alias "@/*" --use-npm
```

The `create-next-app` command above hasn't been run. The hand-written app builds and runs in Docker (see Phase 5b). `web/package-lock.json` was generated without a local Node install:

```bash
docker run --rm -v "${PWD}/web:/app" -w /app node:22-alpine npm install --package-lock-only
```

## Phase 5a: Containers

| File | What it does |
|---|---|
| `Dockerfile` (root) | Multi-stage build: `sdk:10.0` publish, then `aspnet:10.0` runtime as a non-root user on port 8080. One recipe for all three hosts via `--build-arg PROJECT=Marketplace.Api` (or Realtime, Worker). The NuGet cache mount uses `sharing=locked`; without it, compose's parallel builds failed with "Could not find file …/npgsql/…". |
| `.dockerignore` (root) | Keeps `bin/`, `obj/`, `web/`, `docs/`, `mockup/` and `.git/` out of the .NET build context |
| `web/Dockerfile`, `web/.dockerignore` | `node:22-alpine` with `npm ci`, `next build` (`output: "standalone"` in `next.config.ts`) and a `node server.js` runtime as the `node` user |
| `docker-compose.yml` | Adds `api`, `realtime`, `worker` and `web` next to postgres/redis. Settings come from environment variables: `ConnectionStrings__Postgres` (`Host=postgres`), `Database__MigrateOnStartup` (api only), `Cors__Origins__0` and `API_URL=http://api:8080`. |

## Phase 5: Verify

```bash
dotnet test Marketplace.slnx
```

```bash
docker compose up -d --wait postgres redis
```

```bash
dotnet run --project src/Marketplace.Api
```

Expected results, as checked on 2026-10-09:
- `dotnet build` passes with 0 errors.
- 50 of 50 tests pass: 48 architecture tests (4 module rules + 4 persistence checks × 11 modules) and 2 API tests.
- `/health/live` and `/health/ready` return 200 `Healthy`, and `/v1/modules` lists 11 modules.
- Each of the 11 schemas has `outbox_message` and `__ef_migrations_history` (one `InitialCreate` each), and the `postgis` extension is 3.5.2. A restart applies nothing.
- A drift probe works: adding an entity without a migration makes `Migrations_exist_and_match_the_model` fail with the `dotnet ef` command to run.
- Realtime: `POST /hubs/auctions/negotiate?negotiateVersion=1` returns 200 with WebSockets available.
- The worker logs "Outbox relay started".

## Phase 5b: Verify in Docker

```bash
docker compose up -d --build
```

Checked on 2026-10-09:
- All 4 images build (Next 15.5.27, React 19.3, TypeScript 5.9).
- The api and realtime return `/health/ready` 200 and `/v1/modules` lists 11 modules.
- Realtime `negotiate` returns 200, and the worker starts.
- http://localhost:3000 renders the status page with API ok, Database ok and all 11 modules.

## Phase 6: Claude Code skill

`.claude/skills/auction-marketplace/` holds the project skill: architecture rules, workflows, domain rules, a spec map and the `new-module.ps1` scaffolder. See its [README](../.claude/skills/auction-marketplace/README.md).

## Phase 7: MVP core loop (features)

Built in ordered vertical slices, each with migrations, endpoints, pages and tests:

| Slice | What was added |
|---|---|
| 0. Platform | `*.Contracts` projects (`new-module.ps1 -Contracts`). Integration events plus per-module inbox, `OutboxProcessor`, `IBackgroundJob` + worker `JobRunner`. Redis bridge to realtime. Dev JWT auth (`ICurrentUser`, `DevTokenIssuer`). `ProblemException` mapped to problem details. |
| 1. Identity + Location | Users and `POST /v1/dev/token`. Gazetteer places, item locations (private address and exact point, public point snapped to ~1 km), user location with radius. |
| 2. Catalog + Search | Categories (seeded), listings with delivery and location, publish emits `ListingPublished`. `search.listing_card` read model with PostGIS feed/search. |
| 3. Auctions | `Auction` aggregate (proxy bidding, reserve jump, soft close, Buy Now, close), bid service with row lock and idempotency, close job, realtime events. |
| 4. Orders | Order + deal agreement + append-only events, row-locked mutations, address reveal after both accept. |
| 5. Web, seed and docs | Pages: home, search, item (live), sell, order, me, sign-in. BFF proxy. `scripts/seed-demo.ps1`. Docs and skill updated. |

Migrations added per module with `dotnet ef migrations add <Intent> …` (e.g. `AddAuctionsAndBids`). Modules without features got `AddInbox`.

Problems hit and fixed while building:
- EF can't sort on a member of a positional-record projection. Use a member-initialised class (`CardWithDistance`).
- Child entities added through a navigation with code-generated `Guid` keys need `ValueGeneratedNever()`, or EF issues an UPDATE (`DbUpdateConcurrencyException`).
- The JWT issuer must use real time, not the test's fake domain clock, or tokens are "not yet valid".
- Dates formatted during SSR caused a hydration mismatch (React #418). Use the client-only `LocalTime` component.

Verified on 2026-10-09:
- `dotnet test` passes 412 tests (350 unit/property, 50 architecture, 10 integration, 2 API).
- `docker compose up -d --build` plus the seed script.
- Two-user browser walkthrough: live outbid toast, a soft-close extension from 16:59:03 to 17:00:06, close to `AG-1001`, a v2 terms change, a stale v1 accept rejected with 409, address revealed after both accepted, paid → received.
- No console errors and no horizontal scroll at 375 px.

## What's next

1. Make the first commit, set up a remote, and add CI (build, all tests including integration with Docker, web build).
2. Next features from the MVP scope:
   - fixed price + Make Offer;
   - watchlist and saved searches with alerts;
   - Notifications (email/push) from the existing events;
   - reviews and problem reports;
   - admin moderation;
   - seller fee invoices;
   - map view and photo uploads.
3. Replace dev auth with a real identity provider; add bid rate limiting (Redis) and an admin audit log.
