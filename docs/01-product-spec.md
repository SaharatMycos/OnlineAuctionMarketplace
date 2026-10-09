# Online Auction Marketplace: Product Specification

| | |
|---|---|
| Status | Draft v0.2 |
| Date | 2026-10-09 |
| Owner | Product owner (TBD) |
| Related | [Feature design](02-feature-design.md) · [System design](03-system-design.md) |

## 1. Vision

A single, **location-first** auction marketplace where **consumers, businesses and procurement teams** can buy and sell through fair, transparent auctions. By default buyers see what's **near them** (distance, radius, map), and local pickup is promoted, while items that ship stay available from farther away. The same bidding engine and trust layer serve three trading modes:

| Mode | Who sells | Who buys | Typical goods | What matters most |
|---|---|---|---|---|
| **C2C** | Individuals | Individuals | Used electronics, collectibles, fashion, hobby items | Easy listing, a clear deal agreement, reputation |
| **B2C** | Verified businesses (retailers, refurbishers, estate/auction houses) | Individuals | New/refurb stock, returns, overstock, curated sales | Storefronts, bulk listing, tax invoices, return policies |
| **B2B** | Verified businesses | Verified businesses | Liquidation lots, surplus inventory, machinery, vehicles, scrap, procurement | Org accounts, private/sealed auctions, deposits, invoicing, net terms, reverse auctions |

## 2. Goals and non-goals

**Goals (first 12 months)**
1. Launch C2C + B2C timed auctions with Buy Now, a deal agreement for every win, and buyer-to-seller payment outside the app that both sides confirm (MVP).
2. Add B2B: organisation accounts, verified-business gating, private and sealed-bid auctions, lot sales.
3. Add reverse auctions (procurement), so buyers post a demand and suppliers bid the price down.
4. Earn trust: under 1% of completed orders disputed, and shill bidding detected before close.

**Non-goals (for now)**
- **Escrow and in-platform payment (deferred).** In the MVP the platform never touches the money. After a win, the platform creates a deal agreement, the parties settle payment between themselves, and both confirm it in the app. Escrow through a licensed payment service provider (PSP) can come later (see [Feature design §6.3](02-feature-design.md#63-later-escrow-via-a-psp)).
- Running our own logistics. We integrate carriers and allow freight/pickup for B2B.
- Crypto/NFT auctions, real estate, regulated goods (firearms, alcohol, pharmaceuticals). These are excluded by policy at launch.

## 3. Personas

| Persona | Mode | Needs |
|---|---|---|
| **Casual seller "Mai"**: sells a used camera | C2C | List from phone in under 3 minutes, suggested price, sell to nearby buyers, a clear deal agreement and cash at handover |
| **Collector "Ken"**: hunts rare items | C2C/B2C | Saved searches, proxy bidding, anti-sniping, authenticity |
| **Store manager "Lina"**: refurbished-phone retailer | B2C | Bulk upload (CSV/API), storefront, promoted listings, analytics, VAT invoices |
| **Liquidator "Somchai"**: sells returned pallets | B2B | Lot listings with manifests, bidder deposits, buyer premium, pickup scheduling |
| **Procurement buyer "Anna"**: buys packaging materials | B2B | Reverse auction / RFQ, approved-supplier lists, approval workflow, PO and invoice |
| **Org admin "Tom"**: runs a company account | B2B | Invite users, set roles and spending limits, tax-exempt documents |
| **Trust & Safety agent** | Internal | Review flagged listings and bidders, mediate problem reports using the deal-agreement history, audit trail |
| **Finance ops** | Internal | Invoice seller fees, collect them, tax reports |

## 4. Scope by release

### MVP (Phase 1): C2C + B2C core
- Accounts: individual sign-up, email/phone verification, light KYC for sellers (ID check before a first sale).
- Business seller verification and basic storefront.
- Listings: photos, category attributes, condition, **item location**, delivery options (pickup / shipping).
- **Location-based discovery:** buyer location (GPS or city), radius, "Near you" home feed, distance on every listing, nearest-first sort, map view, and exact addresses kept private (see [Feature design §9](02-feature-design.md#9-location-based-discovery-p1)).
- Auction formats: **English (ascending) with proxy bidding**, reserve price, Buy Now, **fixed-price** listings, **Make Offer**.
- Anti-sniping soft close.
- Real-time bid updates, outbid notifications, watchlist, saved searches.
- **Deal agreement** created after a win, Buy Now or accepted offer: the agreed price plus handover and payment terms. Buyer and seller both accept it, pay outside the app (cash on handover, transfer…), then **both confirm the deal is paid**. Flow: **Agreement → Agreed → Deal paid → Received** (see [Feature design §6](02-feature-design.md#6-deal-agreement-and-settlement)).
- Shipping labels/tracking integration (one or two carriers).
- Ratings and reviews, "report a problem" on orders (platform mediates, but holds no funds).
- Admin console: moderation, user management, problem-report queue, fee configuration.

### Phase 2: B2B
- Organisation accounts with members, roles, spending limits and approval workflows.
- Verified-business gating (business registration and tax ID checks).
- **Sealed-bid** auctions (first-price and second-price), **private/invite-only** auctions.
- **Lots** with manifests, multi-quantity (Dutch/uniform-price) auctions.
- **Bidder deposits**, buyer premium, deal agreements that double as pro-forma invoices (PO number, tax lines), and payment terms between the parties (e.g. net 30) recorded in the agreement. The platform does not extend credit.
- Freight/pickup logistics, inspection windows.
- Seller API and bulk tools.

### Phase 3: Procurement and live
- **Reverse auctions** and RFQ for procurement buyers.
- **Live (webcast) auctions** with an auctioneer console.
- Native mobile apps, authentication service for luxury items, advanced analytics, and pricing recommendations.

## 5. Business model

| Revenue line | Applies to | Draft default (to validate) |
|---|---|---|
| Insertion fee | C2C/B2C | Free for first N listings/month, then a small flat fee |
| Final value fee | All | % of hammer price, tiered by category |
| Buyer's premium | B2B (optional per seller) | % added to the winning bid, disclosed before bidding |
| Store subscription | B2C/B2B sellers | Monthly tiers with lower fees and more tools |
| Promoted listings | All sellers | Pay-per-click or % uplift |
| Value-added services | All | Authentication, inspection, freight brokerage, financing referral |

## 6. Success metrics

- **Liquidity:** sell-through rate (auctions that end in a deal confirmed paid by both sides), average bids per auction, unique bidders per auction, and **local liquidity**: live listings within 25 km of the median buyer, and the share of orders that are local pickup.
- **Trust:** problem-report rate, **deal completion rate** (agreements that reach "Deal paid"), agreement acceptance time, reported fraud cases per 1,000 deals, time to resolve reports.
- **Growth:** GMV by mode (C2C/B2C/B2B), active buyers/sellers, repeat purchase rate.
- **Experience:** time to first listing, bid latency p99, notification delivery time.

## 7. Non-functional requirements

| Area | Requirement |
|---|---|
| Fairness | Server clock is the only time source. Every bid gets a server timestamp and sequence number, and ties go to the earliest bid. |
| Consistency | Bids on one auction are strictly serialised. No lost or double-accepted bids. |
| Latency | Bid accept/reject p99 < 300 ms. Real-time price push to watchers < 1 s. |
| Availability | 99.9% for browse/bid. Auctions affected by an outage are auto-extended (policy in [Feature design §4.6](02-feature-design.md#46-outages-and-fairness)). |
| Scale (initial target) | 100k concurrent viewers, 500 bids/s peak, and hot auctions with 50+ bids/s in the last minute |
| Audit | Append-only bid log and deal-agreement history (every acceptance and paid/received confirmation). Every admin action is logged with actor and reason. |
| Security | No payment or bank data stored in the MVP (payment happens outside the app). Deal agreements readable only by the two parties, with an append-only acceptance and confirmation history. MFA for sellers/org admins, rate limiting on bids. |
| Location privacy | Exact item addresses are never public. Show the area name, a fuzzed ~1 km map area and a rounded distance. The exact address goes only to the winning buyer. Buyer GPS is used only with consent and isn't shown to sellers. |
| Privacy | Bidder identities masked publicly. Comply with local data-protection law (GDPR/PDPA, depending on launch market). |
| Accessibility | WCAG 2.2 AA. Bid flows work with keyboard and screen readers. |
| Localisation | Multi-currency display, local tax (VAT/GST) handling, multi-language UI |

## 8. Compliance and policy considerations

- **Payments:** in the MVP the platform doesn't handle sale money, which avoids payment licensing. If escrow is added later, use a PSP with marketplace/split-payment support so the PSP carries seller KYC/AML and fund holding.
- **Consumer law:** B2C sales may carry cooling-off and return rights that differ from C2C and auction-specific rules. Per-market legal review is needed.
- **Tax:** marketplace-facilitator tax rules may make the platform collect VAT/GST on B2C sales. B2B needs tax-ID validation and reverse-charge invoicing.
- **Prohibited items policy**, IP/counterfeit takedown (notice-and-action) process.
- **Auction law:** some jurisdictions regulate auctioneers, buyer premiums and shill bidding explicitly.

> [!WARNING] Unverified
> The legal and tax points above are a checklist of areas to review, not legal advice. Requirements depend on the launch country.

## 9. Open questions

1. **Launch market(s) and currency?** This drives tax, consumer law, language and the common local payment methods buyers use to pay sellers.
2. Is MVP **web-only**, or web plus mobile apps?
3. Which **categories** at launch? (Narrow categories make liquidity easier to build.)
3a. Which **launch city/cities**? A location-first marketplace needs enough local listings, so launching city by city is usually easier than going national.
4. Does the platform **authenticate** high-value goods, or only rely on seller reputation?
5. For B2B, is recording payment terms (e.g. net 30) in the agreement enough, or will buyers expect credit/financing through a partner later?
6. Should we offer **live auctions** early (strong for B2B liquidation), or is timed-only enough?
7. Brand and name? ("Bidly" in the mockup is a placeholder.)
8. Should the agreed price be **negotiable after the win** (e.g. a damaged item), or always locked to the auction result?
9. Is a **click-to-accept** deal agreement legally binding as a sale record in the launch market? (Legal review.)
10. Units: **km or miles**? Follows the launch market.

## 10. Decision log

| Date | Decision | Why | Where |
|---|---|---|---|
| 2026-10-09 | Three trading modes (C2C, B2C, B2B) on one bidding engine, released in 3 phases | Shared engine and trust layer; B2C/C2C first for liquidity | §1, §4 |
| 2026-10-09 | **No escrow and no in-app payment** in the MVP | Simpler and avoids payment licensing. Escrow via a PSP is a possible later upgrade. | §2, [Feature design §6.3](02-feature-design.md#63-later-escrow-via-a-psp) |
| 2026-10-09 | **Deal agreement after every win**: fixed price + handover/payment terms. Both sides accept, pay outside the app, both confirm "Deal paid", then the buyer marks Received. | A written record both parties accept, and a stronger signal than a buyer-only "paid" | §4, [Feature design §6](02-feature-design.md#6-deal-agreement-and-settlement) |
| 2026-10-09 | **Location-first**: "Near you" feed, radius, distance, map, pickup promoted, shipping still allowed. Exact addresses private. | Local pickup with cash at handover suits a no-escrow model. Launch city by city. | §1, §4, §7, [Feature design §9](02-feature-design.md#9-location-based-discovery-p1) |
| 2026-10-09 | **Modular monolith**, not microservices (3 processes: `api`, `realtime`, `worker`) | Small MVP, per-auction transactions, small team | [System design §2](03-system-design.md#2-architecture-decision-modular-monolith-not-microservices) |
| 2026-10-09 | **Stack: C#/.NET 10 backend + Next.js web**, PostgreSQL + PostGIS, Redis, OpenSearch | Team's choice. ASP.NET Core, SignalR and the generic host cover all three processes in one language. | [System design §1](03-system-design.md#1-architecture-overview) |
| 2026-10-09 | **Persistence: EF Core code-first**, replacing SQL-first scripts. One `DbContext`, schema, migrations history table and outbox table per module. | Team's choice. The C# model is the source of truth, migrations are generated, and an architecture test fails when the model and migrations drift. Raw SQL is still available for hot paths. | [System design §1](03-system-design.md#1-architecture-overview), [05 Architecture §2.3](05-architecture.md#23-data-design--now) |

| 2026-10-09 | **Dev authentication first:** JWT from a dev token endpoint (switch `Auth:DevTokens`); a real identity provider later, by configuration only | Lets the whole loop be built and tested now; modules depend only on `ICurrentUser` | [05 Architecture §2.2a](05-architecture.md#22a-runtime-mechanics--now) |
| 2026-10-09 | **Search read model in PostGIS** (`search.listing_card`) built from events; OpenSearch deferred | Enough for city-by-city launch volumes; same endpoints can move to OpenSearch | [System design §4](03-system-design.md#4-location-and-geo-search) |
| 2026-10-09 | **Events:** per-module outbox + inbox, relayed by the worker; browser pushes go worker → Redis → realtime (SignalR) | Exactly-once effects without a broker; realtime stays a separate, scalable process | [05 Architecture §2.2a](05-architecture.md#22a-runtime-mechanics--now) |
| 2026-10-09 | **Demo defaults:** USD (`Marketplace:Currency`), km, Chicago region; auction lengths also allow 5 min and 1 h for demos and testing | Launch market still open (§9 Q1, Q10); short auctions make the close and deal flow easy to try | Feature design §3.1 lists 1, 3, 5, 7, 10 days |

## Change log

- **Code (2026-10-09):** MVP core loop implemented: dev sign-in, location-first feed and search, listings, English auctions with proxy bidding, soft close, Buy Now and the close job, deal agreement flow, live updates, web pages.

- **v0.2 (2026-10-09):** location-first discovery, deal agreement instead of in-app payment (escrow removed), modular-monolith decision, consistency pass across all three docs.
- **v0.1 (2026-10-09):** first draft.
