# Online Auction Marketplace: System Design (draft)

| | |
|---|---|
| Status | Draft v0.2 |
| Date | 2026-10-09 |
| Related | [Product spec](01-product-spec.md) · [Feature design](02-feature-design.md) |

## 1. Architecture overview

A **modular monolith**: one codebase and one database, with strict module boundaries, deployed as a few processes. See [§2](#2-architecture-decision-modular-monolith-not-microservices) for why.

```
 Web (SSR) / Mobile apps / Seller API clients
                │  HTTPS + WebSocket
        ┌───────┴────────┐
        │  API gateway   │  auth, rate limits, WAF
        └───────┬────────┘
 ┌──────────────┼───────────────────────────────────────────┐
 │ Identity & Orgs │ Catalog & Listings │ Auctions & Bidding │
 │ Deals (agreement → paid → received) │ Fees & Billing      │
 │ Reviews & Reports │ Messaging │ Notifications │ Admin     │
 └──────────────┬───────────────────────────────────────────┘
                │ transactional outbox → event bus
   PostgreSQL (system of record) · Redis (live state, pub/sub, rate limits)
   OpenSearch (search) · Object storage + CDN (media) · Job scheduler
   External: KYC vendor, fee billing (card on file), carriers, email/SMS/push, fraud scoring
   (Later: marketplace PSP if escrow is added)
```

**Stack (decided 2026-10-09):**
- **Backend:** C# on .NET 10 (ASP.NET Core minimal APIs). Real-time uses SignalR. The worker is a .NET generic host. Data access uses **EF Core 10, code-first** (Npgsql provider + NetTopologySuite for PostGIS, snake_case naming): one `DbContext` per module, owning one schema, with its own migrations, migrations history table and outbox table. Hot paths such as bidding can still use raw SQL (`FromSql … FOR UPDATE`) through the same context.
- **Web:** Next.js (App Router, TypeScript), running as its own process and calling the API.
- **Infrastructure:** PostgreSQL + PostGIS, Redis, OpenSearch, and a managed queue (SQS/Kafka) when the outbox relay needs one.

The skeleton lives in the repo root (`src/`, `tests/`, `web/`). See the root README. There's no payment processing for sales in the MVP. Seller fee billing can use a standard payment provider's subscriptions/invoicing.

## 2. Architecture decision: modular monolith, not microservices

| | |
|---|---|
| Status | Accepted (2026-10-09) |
| Scope | MVP (Phase 1) and B2B (Phase 2). Revisit before Phase 3 with real load data. |

### Context
- The MVP is small: one bidding engine, listings with location search, orders with a deal agreement (four statuses, no escrow, no payment processing), notifications and admin.
- Bidding needs **strong consistency per auction**: proxy-bid resolution, soft-close extension and the close job must update one auction atomically.
- The team is small, and there's no need yet for independent deployments by separate teams.
- The scale target (500 bids/s peak, 100k concurrent viewers, see [Product spec §7](01-product-spec.md#7-non-functional-requirements)) fits one well-built application plus Redis and a WebSocket gateway.

### Decision
Build a **modular monolith**: one repository, one PostgreSQL database, deployed as **three processes from the same codebase**:

| Process | Runs | Scales by |
|---|---|---|
| `api` | REST API, admin API, all domain modules (the Next.js web app is a separate front-end process) | CPU / request rate |
| `realtime` | WebSocket gateway (auction channels, user notifications) | Concurrent connections |
| `worker` | Auction-close scheduler + sweeper, notification sending, search indexing, outbox relay | Queue depth |

Module rules, so a module can later move out without changing its callers:
1. **Modules:** Identity & Orgs · Location · Catalog & Listings · Auctions & Bidding · Orders & Deals · Fees & Billing · Reviews & Reports · Messaging · Notifications · Search · Admin.
2. **Each module owns its tables.** No module reads or writes another module's tables. Use one database schema per module to make this visible.
3. **Cross-module calls go through the module's public interface** (in-process function calls), never through its internals.
4. **Side effects across modules go through events** written to the module's own transactional outbox table (`<schema>.outbox_message`, same `SaveChanges` as the state change) (e.g. `AuctionClosed` → Orders & Deals creates the order and deal agreement, and Notifications sends it to both parties).
5. Lint/architecture tests enforce the boundaries in CI.

### Alternatives considered
| Option | Why not now |
|---|---|
| Microservices from day one | Turns a few modules into many deployments, networks and databases. Bidding and order creation would need sagas or distributed locks, bringing more ways to lose or double-accept a bid. It also adds DevOps cost (service discovery, tracing, versioned APIs, many pipelines) before there's a problem to solve. |
| Single process with no module boundaries | Fastest at first, but it becomes hard to split later and hard for more than a few people to work in. |

### Consequences
- **Good:** simple transactions for bidding and order creation, one deploy pipeline, easy local dev, and fast iteration on the MVP.
- **Bad:** one database is a shared point of failure and scaling limit. A bad deploy affects all modules, which is mitigated by health checks, blue/green deploys and feature flags.
- **Follow-up:** set up monitoring from day one for the signals below, so a split is driven by data.

### When to extract a service
| Candidate | Extract when… | Likely order |
|---|---|---|
| Real-time gateway | Already a separate process. Becomes its own service when connection count or fan-out outgrows it. | 1st |
| Notifications | Email/SMS/push volume or provider retries start slowing workers | 2nd |
| Search | Indexing load or query features need their own scaling/cluster | 3rd |
| Bidding engine | Hot-auction lock contention, or bid accept p99 > 300 ms. Then use partitioned bid workers keyed by `auction_id` (see §3). | When measured |
| Payments / escrow | Only if escrow is added. Needs compliance isolation (PCI/PSP scope). | If added |
| Procurement (reverse auctions) | A separate team owns it with its own release pace | Phase 3+ |

## 3. Bidding engine design

**Requirements:** strict ordering per auction, no lost bids, fast, and auditable.

- **Single writer per auction.** Each bid runs in a DB transaction that does `SELECT … FOR UPDATE` on the auction row (MVP). At higher scale, partition auctions across bid workers by `auction_id` (one in-memory actor per hot auction), with the DB as the durable log.
- **Steps for one bid:**
  1. Authenticate, run rate-limit and eligibility checks (cached).
  2. Begin the transaction and lock the auction.
  3. Validate status, end time (server clock) and the minimum bid.
  4. Run the proxy-bid resolution ([Feature design §4.1](02-feature-design.md#41-proxy-automatic-bidding-p1)).
  5. Insert the `bid` row(s) (including auto-generated proxy bids) with a monotonic `seq`.
  6. Update the auction's `current_price`, `leader_id`, `bid_count`, and `end_at` (soft close).
  7. Write an outbox event `BidPlaced` / `Outbid` / `AuctionExtended`, then commit.
- **Idempotency:** the client sends an `Idempotency-Key` per bid attempt, so retries never double-bid.
- **Fan-out:** outbox relay → event bus → realtime gateway pushes to auction channel subscribers (WebSocket) and to the notification service.
- **Close job:** a scheduler wakes at `end_at`, re-reads the auction under lock (end_at may have moved), and transitions it to `closed_*` idempotently. A sweeper catches any missed closes every minute.

## 4. Location and geo search

The product is location-first ([Feature design §9](02-feature-design.md#9-location-based-discovery-p1)), so geo is a core capability, not just a filter.

- **Storage:** PostgreSQL + **PostGIS**. Each `location` has an exact `geo` point (private, used only for the winner's pickup address and internal fraud checks) and a `public_geo` point, snapped to a ~1 km grid cell and fixed at creation so repeated queries can't average it out.
- **Search index:** OpenSearch `geo_point` on `public_geo`. Queries use a `geo_distance` filter for the radius, plus `should` clauses for shipping items outside it. Sorting uses `_geo_distance` (nearest) or a `function_score` with gaussian distance decay blended with ending time and text relevance.
- **Distance shown to users:** computed from `public_geo`, rounded (≥ 1 km steps, "< 1 km" floor), never from the exact point.
- **Buyer location:** browser/device geolocation (with consent) or geocoded city/postcode, with an IP-based city as fallback. Only a public-grade point is stored, plus the radius.
- **Geocoding:** an external vendor behind `/v1/geo/geocode`, cached. Address validation happens when the seller saves a location.
- **Map view:** tiles from a map provider. The API returns clustered `public_geo` areas per bounding box, drawn as circles, not pins.
- **Alerts:** a saved search stores a point + radius. When a listing is published, the `worker` matches it against saved searches with a geo query and queues notifications.
- **Module ownership:** a `Location` module (inside the monolith, see §2) owns `location`/`user_location` and geocoding. Catalog and Search consume it through its interface.

## 5. State machines

**Auction**
```
draft → scheduled → live ⇄ (extended) → closing → closed_sold
                     │                        └→ closed_unsold
                     └→ cancelled (admin/seller before bids, or admin with reason)
```

**Order**
```
agreement_pending → agreed → deal_paid → received (complete)
  (needs buyer +    (needs buyer "paid" +
   seller accept)    seller "received" confirmations)
       └→ cancelled (acceptance window or handover date + grace passed)
problem_reported is a flag on the order, not a state: the platform holds no funds (MVP)
```

## 6. Core data model

| Entity | Key fields |
|---|---|
| `user` | id, email, phone, name, locale, trust_tier, status |
| `organization` | id, legal_name, reg_no, tax_id, country, kyc_status, type (individual_seller / business) |
| `membership` | user_id, org_id, role, spend_limit, approval_threshold |
| `listing` | id, seller_org_id, category_id, title, description, condition, attributes (jsonb), media[], ship_options, return_policy, status, **location_id, delivery (pickup/both/ship)** |
| `location` | id, owner (user/org), label, address (encrypted), **geo (PostGIS geography point, exact, private)**, **public_geo (fuzzed ~1 km)**, area_name, city, postcode, country |
| `user_location` | user_id, source (gps/manual/ip), public-grade geo, area_name, radius_km, updated_at |
| `lot` | id, listing_id, manifest_url, est_retail_value, quantity |
| `auction` | id, listing_id, format (english/sealed_fp/sealed_sp/dutch/multi_unit/reverse), mode (c2c/b2c/b2b), visibility, start_at, end_at, original_end_at, start_price, reserve_price, buy_now_price, increment_table_id, soft_close_min, max_extension_min, buyer_premium_pct, deposit_amount, currency, current_price, leader_id, bid_count, status, version |
| `auction_invite` | auction_id, org_id / user_id, status |
| `bid` | id, auction_id, bidder_id, org_id, amount, max_amount (proxy; encrypted/private), kind (manual/proxy/sealed), seq, created_at, status (valid/retracted/voided), ip/device ref |
| `offer` | id, listing_id, buyer_id, amount, parent_offer_id, status, expires_at |
| `order` | id, auction_id/listing_id, buyer_org_id, seller_org_id, item_total, buyer_premium, shipping, tax, total, status, accept_by |
| `deal_agreement` | id, number (AG-…), order_id, version, agreed_price (locked), handover (pickup/shipping), handover_date, payment_method, notes, pickup_location_id, pdf_url, buyer_accepted_at, seller_accepted_at, buyer_paid_at, seller_paid_at |
| `deal_event` | id, agreement_id, version, actor_id, action (created/terms_changed/accepted/confirmed_paid/confirmed_received/marked_received/cancelled), at (append-only) |
| `fee_invoice` | id, seller_org_id, period, amount, status (monthly final-value fees) |
| `deposit` | id, auction_id, bidder_org_id, amount, status (held/applied/released/forfeited) |
| `shipment` | id, order_id, carrier, tracking_no, label_url, status, delivered_at |
| `problem_report` | id, order_id, reporter_id, reason (not_received/not_as_described/damaged/counterfeit/no_show/payment_disagreement), status, resolution, evidence[] |
| `review` | id, order_id, author_id, subject_id, ratings, text, visible_at |
| `watch`, `saved_search`, `notification`, `message_thread`, `message` | standard |
| `audit_log` | actor_id, action, target, reason, before/after, at |
| `rfq_event`, `rfq_bid` [P3] | reverse-auction specific |

## 7. API sketch (REST + WebSocket)

```
POST   /v1/listings                      create draft
POST   /v1/listings/{id}/publish
GET    /v1/auctions/{id}                 state + min next bid + server_time
POST   /v1/auctions/{id}/bids            {max_amount} + Idempotency-Key
POST   /v1/auctions/{id}/sealed-bids     {amount} (one per bidder, editable until close)
POST   /v1/auctions/{id}/buy-now
POST   /v1/listings/{id}/offers          {amount}
POST   /v1/offers/{id}/accept|counter|decline
POST   /v1/auctions/{id}/deposits
GET    /v1/me/bidding?status=winning|outbid|won|lost
GET    /v1/orders/{id}/agreement         deal agreement (buyer + seller only; exact address after both accept)
PATCH  /v1/orders/{id}/agreement         {handover?, handover_date?, payment_method?, notes?}  (resets other side's acceptance)
POST   /v1/orders/{id}/agreement/accept  {version}
POST   /v1/orders/{id}/confirm-paid      buyer: "I paid" · seller: "I received payment"
GET    /v1/orders/{id}/agreement.pdf
POST   /v1/orders/{id}/mark-received
POST   /v1/orders/{id}/problems
GET    /v1/search?q=&category=&format=&ends_before=&seller_type=
              &lat=&lng=&radius_km=&delivery=pickup|ship&include_shipping=true&sort=nearest
              → each hit has area_name, distance_km (rounded, from public_geo), delivery
GET    /v1/feed/nearby?lat=&lng=&radius_km=      "Near you" + "Ships to you" sections
GET    /v1/search/map?bbox=&...                  clustered public_geo areas for the map view
PUT    /v1/me/location                           {lat,lng | city/postcode, radius_km}
GET    /v1/geo/geocode?q=                        city/postcode → point (proxy to geocoding vendor)
WS     /v1/realtime  → subscribe auction:{id}, user:{id}
       events: bid.placed, auction.extended, auction.closed, user.outbid, user.won
Webhooks (Seller API): agreement.created, agreement.accepted, deal.paid, order.received, order.problem_reported
```

**Implemented in the MVP core loop (2026-10-09):**
- **Built as sketched:** listings (create with `publish: true`, publish), auctions (get, bids with `Idempotency-Key`, buy-now), `/v1/me/bidding`, the agreement endpoints (get/patch/accept/confirm-paid/mark-received), `/v1/feed/nearby`, `/v1/search`, `/v1/me/location` and `/v1/geo/geocode` (gazetteer for now).
- **Added:**
  - `GET /v1/categories`, `GET /v1/listings/{id}`, `PUT /v1/listings/{id}` (drafts) and `GET /v1/me/listings`
  - `GET /v1/auctions/by-listing/{listingId}` and `GET /v1/auctions/{id}/bids` (masked history)
  - `GET /v1/orders/{id}`, `GET /v1/orders/by-auction/{auctionId}`, `POST /v1/orders/{id}/cancel` (seller, non-completion) and `GET /v1/me/orders`
  - `GET /v1/me` and `GET /v1/time`
  - Dev only (`Auth:DevTokens`): `POST /v1/dev/token` and `GET /v1/dev/users`
- **Realtime:** SignalR hub `/hubs/auctions` (realtime process) instead of `WS /v1/realtime`. Clients call `Watch(auctionId)`, and signed-in users join `user:{id}` automatically. Events: `bid.placed`, `auction.extended`, `auction.closed`, `user.outbid`, `user.won`, `auction.ended` (seller) and `order.updated`.
- **Errors:** RFC 7807 problem details with a stable `code` (e.g. `bid_too_low`, `stale_version`, `not_a_party`).
- **Not yet:** sealed bids, offers, deposits, problems, `agreement.pdf`, `/v1/search/map` and webhooks.

## 8. Security and anti-fraud

- MFA for sellers, org admins and staff. Step-up auth for high-value bids and for accepting high-value deal agreements.
- Rate limits per user/IP/auction. Bot detection on the bid endpoint.
- Device fingerprint + fraud score. Linking graph (devices, IPs, phones, addresses, locations) feeds shill-bid detection and repeat-scam detection (e.g. a seller with many "payment disagreement" reports).
- Proxy max amounts are stored encrypted and readable only by the bid engine. Exact item locations are encrypted and readable only by the Location module.
- Deal agreements are readable only by the two parties (and staff mediating a report). Accept and confirm calls carry the agreement `version` to prevent accepting stale terms.
- Append-only audit, bid and deal-event tables. Admin destructive actions need a reason code (and four-eyes approval for bulk suspensions or data deletion).

## 9. Observability

- Metrics: bid latency, rejected-bid reasons, close-job lag, WebSocket connections, notification lag, geo-search latency, deal funnel (created → agreed → paid → received) and time in each step.
- Tracing across bid → event → push.
- Alerts: close-job lag > 5 s, bid error rate, deal-agreement email failures, agreements nearing their acceptance deadline.

## 10. Testing strategy highlights

- Property-based tests for proxy-bid resolution (invariants: the leader always holds the highest max, the price never exceeds the second-highest max + increment, and ties go to the earliest bid).
- Concurrency tests: N parallel bids on one auction produce a correct serial outcome.
- Clock tests around end time and soft-close extension.
- Geo tests: radius edges, nearest-first sort, shipping items outside the radius, and **privacy tests** (no API response or page ever exposes the exact `geo` or unrounded distance, except the winner's accepted deal agreement).
- Deal flow tests: agreed only after both acceptances, a terms change resets the other side's acceptance, the price is immutable, deal_paid only after both confirmations, deadline → cancelled, and only the two parties can read the agreement.
