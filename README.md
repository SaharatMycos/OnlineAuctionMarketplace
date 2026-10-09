# Online Auction Marketplace

A location-first, multi-mode (C2C, B2C, B2B) auction marketplace. "Bidly" is a placeholder brand.

- **Specs:** [docs/](docs/README.md): product spec, feature design and system design.
- **Clickable prototype:** [mockup/](mockup/index.html) (vanilla HTML/CSS/JS).
- **Code:** a C#/.NET **modular monolith** (system design §2) plus a Next.js web app.
- **Architecture, design, structure and principles:** [docs/05-architecture.md](docs/05-architecture.md).
- **How it was created, step by step:** [docs/04-project-setup.md](docs/04-project-setup.md).
- **Claude Code skill:** [.claude/skills/auction-marketplace/](.claude/skills/auction-marketplace/README.md). It covers the project rules and workflows; run `/auction-marketplace` in Claude Code.

Status: **MVP core loop works end to end**:
1. Sign in (dev).
2. Set your location and browse "Near you", with search, radius and delivery filters.
3. List an item with a private address.
4. Bid live with proxy bidding and soft close, or use Buy Now.
5. The auction closes, which creates a deal agreement.
6. Both parties accept, both confirm payment, then the buyer marks the item received.

Deferred: fixed price and offers, watchlist and saved searches, notifications by email or push, reviews and problem reports, admin, fees, map view, photo uploads and a real identity provider.

## Try it in 2 minutes

Create your local `.env` once. It holds random secrets, is git-ignored, and both compose and `dotnet run` read it:

```bash
pwsh -File scripts/init-env.ps1
```

```bash
docker compose up -d --build
```

```bash
pwsh -File scripts/seed-demo.ps1
```

1. Open http://localhost:3000 and sign in as **Bob**.
2. Open an item and bid.
3. In another browser (or a private window), sign in as **Carol** and outbid Bob. Bob's page updates live.

Add `-ShortAuctions` to the seed command to make every auction end within about 5 minutes, so you can follow the close and the deal agreement.

## Layout

```
src/
  Marketplace.Api/            HTTP API process          http://localhost:5080
  Marketplace.Realtime/       SignalR process           http://localhost:5081  (hub: /hubs/auctions; pushes relayed via Redis)
  Marketplace.Worker/         Background process: outbox relay (event handlers) + jobs (auctions.close every second)
  Marketplace.Bootstrap/      Composes the app: module catalogue, shared registration, event registry, Redis publisher
  Marketplace.SharedKernel/   IModule, IClock, ModuleDbContext, outbox/inbox + processor, events, auth, geo privacy, jobs
  Modules/
    Marketplace.Modules.<Name>/            Domain/ Application/ Persistence/ Endpoints/ + <Name>Module.cs
    Marketplace.Modules.<Name>.Contracts/  public events and query interfaces (Location, Catalog, Auctions, Orders)
tests/
  Marketplace.ArchitectureTests/  module boundary rules + drift guard (run in CI)
  Marketplace.UnitTests/          domain rules and proxy-bid property tests
  Marketplace.IntegrationTests/   full loop against PostGIS in Docker (Testcontainers)
  Marketplace.Api.Tests/          in-process API smoke tests
web/                          Next.js (App Router)      http://localhost:3000  (BFF proxy, SignalR client)
scripts/init-env.ps1          Creates .env (git-ignored secrets) from .env.example
scripts/seed-demo.ps1         Demo data through the public API
docs/  mockup/                specs and prototype
Dockerfile                    One image recipe for api / realtime / worker (build arg PROJECT)
docker-compose.yml            Full stack: web, api, realtime, worker, Postgres + PostGIS, Redis, OpenSearch (profile "search")
```

The modules are Identity, Location, Catalog, Auctions, Orders, Billing, Reviews, Messaging, Notifications, Search and Admin.

To add a module, run:

```bash
pwsh -File .claude/skills/auction-marketplace/scripts/new-module.ps1 -Name <Name> -Summary "<what it owns>"
```

## Module rules

1. A module owns its schema (`auctions`, `orders`, …). It's modelled **code-first** by the module's `DbContext` (`Persistence/<Name>DbContext.cs`, EF Core). Migrations are generated into `Persistence/Migrations/`, and the history table lives in the module's own schema.
2. A module never references another module's project. When one module needs another, add a `Marketplace.Modules.<Name>.Contracts` project and reference only that.
3. Side effects across modules go through outbox events in the module's own `<schema>.outbox_message` table (same `SaveChanges` as the change), which the worker relays.
4. `tests/Marketplace.ArchitectureTests` checks rules 1 and 2, and also that every module's migrations match its model.
5. Time comes only from `IClock` (the server clock is the only time source).

## Prerequisites (Windows)

```bash
winget install Microsoft.DotNet.SDK.10
```

```bash
winget install OpenJS.NodeJS.LTS
```

```bash
winget install Docker.DockerDesktop
```

```bash
winget install Git.Git
```

## Run everything in Docker

You only need Docker Desktop. After creating `.env` (`pwsh -File scripts/init-env.ps1`), this builds and starts web, api, realtime, worker, Postgres (with PostGIS) and Redis:

```bash
docker compose up -d --build
```

| Service | URL | Image / build |
|---|---|---|
| web (Next.js) | http://localhost:3000 | `web/Dockerfile` (standalone output) |
| api | http://localhost:5080 | root `Dockerfile`, `PROJECT=Marketplace.Api`. Applies migrations on start. |
| realtime | http://localhost:5081 (hub `/hubs/auctions`) | root `Dockerfile`, `PROJECT=Marketplace.Realtime` |
| worker | no port | root `Dockerfile`, `PROJECT=Marketplace.Worker` |
| postgres / redis | localhost:5432 / 6379 | `postgis/postgis:17-3.5`, `redis:7.4-alpine` |

Useful commands:

```bash
docker compose logs -f api worker
```

```bash
docker compose up -d --build api
```

```bash
docker compose down
```

Add `-v` to `docker compose down` to also delete the database volume. Add `--profile search` to `up` to also start OpenSearch.

Inside compose, the apps reach each other by service name: the web app calls `http://api:8080`, and the .NET hosts use `Host=postgres`. Those settings come from environment variables in `docker-compose.yml`, which override `appsettings*.json`. Secrets in `docker-compose.yml` are `${...}` references to `.env`; compose stops with a clear message if one is missing.

## Run locally (hot reload)

Start only the infrastructure. If the app containers are running, stop them first with `docker compose stop web api realtime worker`, because they use the same ports.

```bash
docker compose up -d postgres redis
```

Build and test (the integration tests start a throwaway PostGIS container, so Docker must be running):

```bash
dotnet test Marketplace.slnx
```

Run each process in its own terminal. The api applies migrations on startup in Development:

```bash
dotnet run --project src/Marketplace.Api
```

```bash
dotnet run --project src/Marketplace.Realtime
```

```bash
dotnet run --project src/Marketplace.Worker
```

Run the web app (needs Node; `web/package-lock.json` pins the versions):

```bash
cd web && npm ci && npm run dev
```

Check:

- http://localhost:5080/health/ready returns `Healthy` once Postgres is up.
- http://localhost:5080/v1/modules lists the modules.
- http://localhost:5080/openapi/v1.json has the OpenAPI document (Development only).
- http://localhost:3000 shows the platform status page.

## Configuration

- **Secrets live only in `.env`** (git-ignored, and kept out of Docker images by `.dockerignore`). [`.env.example`](.env.example) is the committed template, and `scripts/init-env.ps1` copies it with random values for `POSTGRES_PASSWORD`, `REDIS_PASSWORD` and `AUTH_SIGNING_KEY`. Use `-Force` to regenerate it.
  - **docker compose** reads `.env` automatically for the `${...}` references in `docker-compose.yml`.
  - **`dotnet run` and `dotnet ef`** load it through `DotEnv.Load()` (`Marketplace.SharedKernel/Configuration/DotEnv.cs`). That provides `ConnectionStrings__Postgres`, `ConnectionStrings__Redis` and `Auth__SigningKey` for localhost.
  - Real environment variables always win, so production sets them from a secret store and has no `.env`.
  - `appsettings*.json` contain no secrets. The integration tests use their own throwaway container credentials.
- **Changing `POSTGRES_PASSWORD` with an existing database volume:** Postgres only reads it when the volume is first created. Either reset the volume with `docker compose down -v`, which deletes the data, or set it on the running database: `docker compose exec postgres psql -U marketplace -d marketplace -c "ALTER USER marketplace PASSWORD '<new>'"`.
- `Database:MigrateOnStartup` controls whether the api applies pending EF Core migrations. Only the api runs them, and an advisory lock prevents races.
- `Auth:SigningKey` signs the dev JWTs. `Auth:DevTokens` turns on `POST /v1/dev/token`, which signs in as any email. Both are on only in Development and compose; replace them with a real identity provider before launch.
- `ConnectionStrings:Redis` is used by the worker (publish) and realtime (subscribe). Redis requires `REDIS_PASSWORD`.
- `Marketplace:Currency` defaults to `USD`.
- Web: `API_URL` is used by the Next.js server, and `REALTIME_PUBLIC_URL` by the browser for SignalR. Both are read at runtime.

## Database changes (code-first)

Change the entities or configuration in a module, then generate a migration. `dotnet-ef` is pinned in `dotnet-tools.json`, so restore it first:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add AddAuctionAndBid --project src/Modules/Marketplace.Modules.Auctions --startup-project src/Marketplace.Api --context AuctionsDbContext --output-dir Persistence/Migrations
```

The api applies pending migrations on its next start. If you forget the migration, `dotnet test` fails and prints the command to run.
