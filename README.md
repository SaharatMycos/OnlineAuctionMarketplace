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

Postgres is **not** a container. It runs natively on your machine, and only the integration tests start a throwaway PostGIS container. Set it up once:

1. Install PostgreSQL and PostGIS for the same version. On Windows, use the EDB installer, then Stack Builder → Spatial Extensions → PostGIS.
2. Create `.env`: `pwsh -File scripts/init-env.ps1`
3. Point `.env` at an **existing** Postgres login. The script fills `POSTGRES_PASSWORD` with a random value and keeps `POSTGRES_USER=marketplace`, and no such login exists in your Postgres yet. Either set `POSTGRES_USER` and `POSTGRES_PASSWORD` to a login you have (for local development, the `postgres` superuser and the password you chose at install), or create a `marketplace` login with that password and `CREATEDB` rights. Then re-run `pwsh -File scripts/init-env.ps1` so the hosts' `appsettings.Development.json` pick up the change.
4. Nothing else. The database is code-first: on startup the api creates the database if it's missing and applies every module's EF Core migrations, including the `postgis` extension. That's why the login must be allowed to create databases and extensions.

To use a Postgres on another server instead, set `POSTGRES_HOST` (used by `dotnet run`, then rerun `init-env.ps1`) and `POSTGRES_HOST_FROM_DOCKER` (used by compose) in `.env` to that server's name or IP.

Then this builds and starts web, api, realtime, worker and Redis. By default the app containers reach your machine's Postgres at `host.docker.internal:5432`:

```bash
docker compose up -d --build
```

| Service | URL | Image / build |
|---|---|---|
| web (Next.js) | http://localhost:3000 | `web/Dockerfile` (standalone output) |
| api | http://localhost:5080 | root `Dockerfile`, `PROJECT=Marketplace.Api`. Applies migrations on start. |
| realtime | http://localhost:5081 (hub `/hubs/auctions`) | root `Dockerfile`, `PROJECT=Marketplace.Realtime` |
| worker | no port | root `Dockerfile`, `PROJECT=Marketplace.Worker` |
| redis | localhost:6379 | `redis:7.4-alpine` |
| postgres (host, not Docker) | localhost:5432 | your local PostgreSQL + PostGIS |

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

`docker compose down` never touches your data, because the database lives in your local Postgres. Add `--profile search` to `up` to also start OpenSearch.

Inside compose, the apps reach each other by service name (the web app calls `http://api:8080`), and the .NET hosts use `Host=$POSTGRES_HOST_FROM_DOCKER` (default `host.docker.internal`) for Postgres. If the api logs "connection refused" or "no pg_hba.conf entry", make sure Postgres listens beyond localhost (`listen_addresses = '*'`) and that `pg_hba.conf` allows the address Docker connects from, then restart the Postgres service. Those settings come from environment variables in `docker-compose.yml`, which override `appsettings*.json`. Secrets in `docker-compose.yml` are `${...}` references to `.env`; compose stops with a clear message if one is missing.

### Debug the containers in Visual Studio

Open `Marketplace.slnx`, choose **Docker Compose** as the startup item and press F5. `docker-compose.dcproj` and `Properties/launchSettings.json` start web and Redis, and run api, realtime and worker in their containers under the debugger. Visual Studio's fast mode reuses the `base` stage of the root `Dockerfile`. To change which services are debugged, use **Manage Docker Compose Launch Settings**. The dotnet CLI ignores the `.dcproj`.

## Run locally (hot reload)

Make sure the local Postgres service is running (on Windows, `Get-Service postgresql*`), then start Redis. If the app containers are running, stop them first with `docker compose stop web api realtime worker`, because they use the same ports.

```bash
docker compose up -d redis
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

- **Standard .NET configuration.** Settings are read in this order, and each later source overrides the earlier ones:
  1. `appsettings.json` (committed, no secrets)
  2. `appsettings.Development.json` (git-ignored, real local values)
  3. environment variables
- **Creating the secrets:** `pwsh -File scripts/init-env.ps1` does both of these:
  - Creates `.env` from [`.env.example`](.env.example) with random `POSTGRES_PASSWORD`, `REDIS_PASSWORD` and `AUTH_SIGNING_KEY`. Docker compose reads it for the `${...}` references in `docker-compose.yml`.
  - Writes each host's `appsettings.Development.json` (connection strings, signing key and dev settings) from those values.

  Change values in `.env` and re-run the script, because it regenerates the JSON files. `-Force` makes new secrets. Both files are git-ignored and kept out of Docker images by `.dockerignore`.
- **`dotnet ef`** reads `src/Marketplace.Api/appsettings*.json` (see `ModuleDesignTimeFactory`).
- Production has neither file. It sets environment variables from a secret store. The integration tests use their own throwaway container credentials.
- **Changing `POSTGRES_USER` or `POSTGRES_PASSWORD`:** set them in `.env` to an existing Postgres login, then re-run `init-env.ps1`. Nothing changes passwords inside Postgres.
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
