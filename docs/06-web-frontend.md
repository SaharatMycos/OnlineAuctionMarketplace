# 06 Web frontend (Next.js): setup and structure

How the `web/` app is set up, how it talks to the backend, and how to run, extend and ship it. For the system as a whole, see [05 Architecture](05-architecture.md). For the backend setup, see [04 Project setup](04-project-setup.md).

## 1. At a glance

| Item | Choice |
|---|---|
| Framework | Next.js 15 (App Router), React 19, TypeScript 5 (strict) |
| Styling | One plain stylesheet, `app/globals.css`, using CSS custom properties ported from the mockup. No Tailwind and no UI kit. |
| Data fetching | Server components call the api directly. Client components go through the `/api/proxy` BFF route. |
| Auth | Dev JWT kept in an httpOnly cookie (`mp_token`). The browser never sees it, except for the SignalR handshake. |
| Realtime | `@microsoft/signalr`, connecting directly to the realtime process (`/hubs/auctions`) |
| Build output | `output: "standalone"`, a self-contained `node server.js` used by the Docker image |
| Port | 3000 |

Only four runtime dependencies are used: `next`, `react`, `react-dom` and `@microsoft/signalr`.

## 2. How the web app fits in

```
Browser ──HTML/RSC──► web (Next.js :3000) ──HTTP + Bearer──► api (:5080)
   │                     ▲  /api/proxy/*  (client calls)
   │                     └  /api/session  (sign-in, token for SignalR)
   └──WebSocket (SignalR, ?access_token=)──► realtime (:5081)
```

- **Server-side calls.** Pages are server components. They read the cookies and call `API_URL` with `lib/api.ts`. In Docker, that's `http://api:8080` over the compose network.
- **Client-side calls.** Interactive parts (bidding, the deal agreement, the sell form) call `/api/proxy/v1/...` with `lib/client.ts`. The route handler adds `Authorization: Bearer <mp_token>` and forwards the request to the api. This is the backend-for-frontend (BFF) pattern: the token stays in an httpOnly cookie that page JavaScript can't read.
- **Realtime.** The browser connects straight to `REALTIME_PUBLIC_URL`. SignalR can't send headers on WebSockets, so the token goes in the `access_token` query parameter, fetched from `GET /api/session` just before connecting. The realtime host allows this query parameter only on `/hubs`, and allows the web origin through CORS (`Cors__Origins__0`).

## 3. Folder structure

```
web/
├── app/                          # App Router: one folder per route
│   ├── layout.tsx                # Header (brand, location chip, search, nav), footer, live notifications
│   ├── globals.css               # Design tokens + all styles
│   ├── page.tsx                  # /            Home feed: Near you, Ships to you, Ending soon
│   ├── search/page.tsx           # /search      Filters, radius, sort
│   ├── signin/                   # /signin      Dev sign-in (pick a seeded user)
│   ├── item/[id]/                # /item/:id    Listing + BidPanel (live bidding)
│   ├── sell/                     # /sell        "Open an auction" form
│   ├── orders/[id]/              # /orders/:id  Deal agreement card
│   ├── me/page.tsx               # /me          "My auctions": deals, bidding, auctions I opened
│   └── api/                      # Route handlers (server only)
│       ├── proxy/[...path]/route.ts   # BFF proxy to the api
│       └── session/route.ts           # POST sign-in, GET token, DELETE sign-out
├── components/                   # Shared UI: ListingCard, Countdown, LocationChip, UserMenu,
│                                 #            LiveNotifications, LocalTime
├── lib/
│   ├── config.ts                 # Runtime env (API_URL, REALTIME_PUBLIC_URL), cookie names, defaults
│   ├── api.ts                    # SERVER ONLY: api(), apiOrNull(), getSessionUser(), getBuyerLocation()
│   ├── client.ts                 # CLIENT ONLY: callApi() via proxy, location cookie, idempotency keys
│   ├── realtime.ts               # CLIENT: one shared SignalR connection + useRealtime() hook
│   ├── format.ts                 # money(), dates, labels
│   └── types.ts                  # TypeScript shapes of api responses
├── next.config.ts                # reactStrictMode, output: "standalone"
├── tsconfig.json                 # strict, "@/*" path alias to web/
├── .env.example                  # Copy to .env.local for local dev
├── Dockerfile, .dockerignore
└── package.json, package-lock.json
```

### Conventions
- **Server by default.** `page.tsx` files are async server components that fetch data and pass it as props. A component gets `"use client"` only when it needs state, effects, the browser or realtime. Examples: `BidPanel.tsx`, `AgreementCard.tsx`, `SellForm.tsx`, `SignInForm.tsx`, `LocationChip.tsx`.
- **Co-located client parts.** A route's interactive piece lives next to its page (`item/[id]/BidPanel.tsx`). Shared ones go in `components/`.
- **Don't mix `lib/api.ts` and `lib/client.ts`.** `api.ts` uses `next/headers` cookies and only works on the server. `client.ts` uses `document` and `fetch("/api/proxy")` and only works in the browser.
- **Always dynamic.** Pages that depend on the user or location set `export const dynamic = "force-dynamic"`, and every fetch uses `cache: "no-store"`. Auction data is live, so nothing is statically cached.
- **Errors.** The api returns RFC 7807 problem JSON with a stable `code`. On the server this becomes `ApiError`, and in the browser `ProblemError`. Show `message` (the problem's `detail`) to the user, and branch on `code` or `status` (for example `409` means a stale agreement version).
- **Dates.** Format timestamps only in the browser, with `<LocalTime>` or `<Countdown>`. Formatting on the server too causes a hydration mismatch (React error #418), because the server doesn't know the viewer's time zone.
- **Imports.** Use `@/lib/...` and `@/components/...`.

## 4. Cookies and session

| Cookie | Set by | httpOnly | Contents | Used for |
|---|---|---|---|---|
| `mp_token` | `POST /api/session` | yes | JWT from the api's `POST /v1/dev/token` | Bearer for server calls and the proxy; SignalR via `GET /api/session` |
| `mp_user` | `POST /api/session` | no | `{handle, displayName}` | Header and "signed in?" checks (display only, not trusted) |
| `mp_loc` | `LocationChip` (client) | no | `{lat, lng, area, radiusKm}` | Feed and search centre and radius. Defaults to Chicago, 25 km. |

Signing out (`DELETE /api/session`) clears `mp_token` and `mp_user`. The token lifetime is 12 hours.

> When a real identity provider replaces dev auth, only `app/api/session/route.ts` and `signin/` change: they'd run the OIDC code flow and store the access token in the same `mp_token` cookie. Pages, the proxy and realtime stay as they are.

## 5. Configuration

The values are read **at request time on the server** (`lib/config.ts`), not baked in at build time. That's why there are no `NEXT_PUBLIC_*` variables: the same image works in every environment. Server components pass `REALTIME_PUBLIC_URL` down to client components as a prop.

| Variable | Default | In Docker Compose | Meaning |
|---|---|---|---|
| `API_URL` | `http://localhost:5080` | `http://api:8080` | Where the Next.js **server** reaches the api |
| `REALTIME_PUBLIC_URL` | `http://localhost:5081` | `http://localhost:5081` | Where the **browser** reaches realtime, so it must be a public URL |

The backend has to allow the web origin. Set `Cors__Origins__0=http://localhost:3000` on `realtime` (needed for SignalR) and on `api`.

## 6. Running it

### Option A: everything in Docker (recommended)
```bash
docker compose up -d --build
```
Open http://localhost:3000. After you edit a file in `web/`, rebuild only the web image:
```bash
docker compose up -d --build web
```

### Option B: `next dev` with hot reload, backend in Docker
Requires Node 22 LTS (`winget install OpenJS.NodeJS.LTS`).
```bash
docker compose up -d postgres redis api realtime worker
```
```bash
cd web
```
```bash
npm ci
```
```bash
cp .env.example .env.local
```
```bash
npm run dev
```
Then open http://localhost:3000. Stop the `web` container first if it's running, because both use port 3000:
```bash
docker compose stop web
```

### Seed demo data
```bash
pwsh -File scripts/seed-demo.ps1
```
Sign in at `/signin` as alice, bob or carol. Use two browsers (or a private window) to bid against each other.

### Checks
```bash
npm run typecheck
```
```bash
npm run build
```
Without a local Node install, run them in a container instead:
```bash
docker run --rm -v "${PWD}/web:/app" -w /app node:22-alpine sh -c "npm ci && npm run typecheck"
```

## 7. How it was set up (from scratch)

The repo's `web/` was written by hand, not generated. These steps reproduce it:

1. **Create the app** (equivalent generator):
   ```bash
   npx create-next-app@latest web --ts --app --eslint --no-tailwind --no-src-dir --import-alias "@/*" --use-npm
   ```
   Then delete the sample page and styles.
2. **`next.config.ts`:** set `reactStrictMode: true` and `output: "standalone"`.
3. **`tsconfig.json`:** set `"strict": true` and `"paths": { "@/*": ["./*"] }`.
4. **Add the realtime client:**
   ```bash
   npm install @microsoft/signalr
   ```
5. **Runtime config:** create `lib/config.ts` (env + cookie names) and `.env.example`.
6. **Data access:** create `lib/api.ts` (server), `lib/client.ts` (browser) and `lib/types.ts` (mirrors the api's JSON; enums are camelCase strings).
7. **BFF routes:** create `app/api/proxy/[...path]/route.ts`, which forwards `content-type`, `idempotency-key` and `accept`, adds the bearer token, and exports GET/POST/PUT/PATCH/DELETE. Then create `app/api/session/route.ts`.
8. **Realtime:** create `lib/realtime.ts` with one shared `HubConnection` per tab, `withAutomaticReconnect()`, and a `useRealtime(url, handlers, auctionId?)` hook that calls `Watch`/`Unwatch` on the hub and re-joins after reconnecting.
9. **Layout and styles:** create `app/layout.tsx` (header, nav, `<LiveNotifications>`) and `app/globals.css`, porting the tokens from `mockup/styles.css`.
10. **Pages:** home, search, sign-in, item, sell, orders and me (see §3).
11. **Lock file without local Node:**
    ```bash
    docker run --rm -v "${PWD}/web:/app" -w /app node:22-alpine npm install --package-lock-only
    ```
12. **Docker:** `web/Dockerfile` (below) and the `web` service in `docker-compose.yml`.

## 8. Docker image

`web/Dockerfile` is a three-stage build:

| Stage | Does |
|---|---|
| `deps` | `node:22-alpine`, `npm ci`, with the npm cache in a BuildKit cache mount |
| `build` | Copies the source and runs `npm run build`, which produces `.next/standalone` and `.next/static` |
| `runtime` | Copies only the standalone server and static files, runs as the non-root `node` user, `CMD ["node", "server.js"]` on `0.0.0.0:3000` |

`web/.dockerignore` keeps `node_modules`, `.next`, `out` and every `.env*` file except `.env.example` out of the build context.

## 9. Recipes

### Add a page
1. Create `app/<route>/page.tsx` as an async server component with `export const dynamic = "force-dynamic"`.
2. Fetch with `api<T>()` (or `apiOrNull<T>()` and `notFound()`), and add response types to `lib/types.ts`.
3. If the page requires sign-in: `if (!(await getSessionUser())) redirect("/signin?next=/<route>")`.
4. Put interactive parts in a co-located `"use client"` component that calls `callApi<T>()`.
5. Add a nav link in `app/layout.tsx` if the page needs one.

### Call a mutating endpoint from the browser
```ts
const result = await callApi<Auction>(`/v1/auctions/${id}/bids`, {
  method: "POST",
  body: JSON.stringify({ maxAmount }),
  idempotencyKey: newIdempotencyKey(), // retries with the same key don't double-bid
});
```
Catch `ProblemError` and show `e.message`.

### Listen to a realtime event
```ts
useRealtime(realtimeUrl, {
  "bid.placed": (e: { seq: number }) => { if (e.seq > auction.seq) refresh(); },
}, auction.id); // pass an auctionId to join the auction:{id} group
```
Events for the signed-in user (`user.outbid`, `user.won`, `order.updated`) arrive without `auctionId`, because the hub puts each connection in its `user:{id}` group. Treat realtime as an enhancement: when an event arrives, refetch the state over HTTP, and use `seq` to drop stale updates.

## 10. Gotchas

- **Hydration mismatch (#418)** from dates or `Date.now()` in server-rendered output. Use `<LocalTime>` or `<Countdown>`, which render after mount.
- **The countdown uses server time.** `BidPanel` computes `serverTime - Date.now()` once and counts down with that offset, so a skewed browser clock doesn't show the wrong end time.
- **`REALTIME_PUBLIC_URL` must be reachable from the browser.** It can't be a compose service name like `http://realtime:8080`.
- **Port 3000 clash** between `next dev` and the `web` container.
- **The api is down.** The home page catches the error and shows a "can't reach the marketplace" message instead of crashing.
- **Don't import `lib/api.ts` in a client component.** `next/headers` fails at build time.

Related: [04 Project setup](04-project-setup.md) · [05 Architecture](05-architecture.md) · [03 System design](03-system-design.md)
