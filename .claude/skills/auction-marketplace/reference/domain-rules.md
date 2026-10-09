# Domain rules

These rules are short. The full versions are in `docs/02-feature-design.md` and `docs/03-system-design.md`. If they conflict, the docs win; fix this file.

## Bidding (feature design §3–§4, system design §3)

- **Single writer per auction:** a bid runs in one transaction that takes `SELECT … FOR UPDATE` on the auction row. Every bid carries an `Idempotency-Key`, and replays return the original result.
- Every bid gets the server timestamp from `IClock` and a per-auction sequence number. Ties go to the earliest bid.
- **Proxy bidding:** each bidder has a max (M).
  - The new bidder beats the leader (M_new > M_lead): price = min(M_new, M_lead + inc(M_lead)).
  - The leader holds (M_new ≤ M_lead): price = min(M_lead, M_new + inc(M_new)).
  - Once any max reaches the reserve, the price jumps to the reserve.
  - Invariants for property tests: the leader holds the highest max; the price never exceeds the second-highest max plus an increment; ties go to the earliest bid.
- **Increment table** (price below → increment):

  | Below | 1 | 5 | 25 | 100 | 250 | 500 | 1,000 | 2,500 | 5,000 | above |
  |---|---|---|---|---|---|---|---|---|---|---|
  | Increment | 0.05 | 0.25 | 0.50 | 1 | 2.50 | 5 | 10 | 25 | 50 | 100 |

- **Soft close:** a bid accepted in the last 2 minutes moves `end_at` to now + 2 minutes.
- **Close job:** idempotent, re-checks `end_at` (it may have been extended), and a sweeper catches missed closes. Closing emits `AuctionClosed` through the outbox.
- Other formats: sealed bid (first/second price), Dutch clock, fixed price + Make Offer, reverse auction / RFQ (weighted score), all in feature design §3 and §13.

## Deal agreement: no escrow, no in-app payment (feature design §5.2, §6)

- A win, Buy Now or accepted offer creates an order plus a `deal_agreement` (number `AG-xxxx`).
- Order states: `agreement_pending` → `agreed` (buyer and seller both accept, which locks the agreement) → `deal_paid` (buyer confirms "deal paid" **and** seller confirms "payment received") → `received` (buyer marks the item received, after which both can leave feedback).
- Agreed price = winning bid + buyer's premium + shipping, if any. It's fixed in P1. Handover method, handover date, payment method and notes can be edited before both accept.
- A change to the terms **resets the other party's acceptance**. Accept and confirm calls carry the agreement `version` and fail if it's stale. The exact pickup address is revealed once both have accepted.
- **Non-completion (§5.2):** the winner has 48 h to accept (C2C/B2C), or a seller-defined window for B2B. If the agreement isn't accepted in time, or isn't confirmed paid by the handover date plus a grace period, the seller can cancel (`cancelled`) and offer the item to the next bidder. The non-completing party gets a strike.
- If the two sides disagree on payment (e.g. the seller says it wasn't received), it becomes a problem report (§8), not a state change.
- `deal_event` is append-only: every acceptance, edit and confirmation is recorded with the actor and time.
- The platform never stores payment or bank data. Seller fees are invoiced monthly (Billing).

## Location (feature design §9, system design §4)

- Buyer location comes from GPS (with consent), a chosen city, or an IP fallback. Radius options: 5, 10, **25 (default)**, 50, 100 km, or Anywhere.
- The home feed shows "Near you" and "Ships to you". The sort can be nearest first, and there's a map view. Delivery options: `pickup`, `ship`, `both`.
- **Privacy:** `location.geo` (exact) is private. Public responses use `public_geo`, fuzzed to about 1 km, plus the area name and a rounded distance. The exact pickup address is revealed only to the buyer and only after both parties accept the agreement. Never send the buyer's GPS position to sellers.
- Storage: PostGIS `geography(Point, 4326)`. Search uses OpenSearch `geo_point` with `geo_distance`.

## Implementation choices (2026-10-09)

These are decisions the spec left open. The code and tests follow them.
- **Bid rows:** `BidCount` counts every bid row, including automatic proxy rows (a challenger's bid plus the leader's proxy response = 2). When a new leader takes over, the history shows the old leader's proxy reaching their max.
- **Soft close:** any accepted bid in the last 2 minutes extends, including a leader raising their own max. The extension is capped at the original end + 30 minutes.
- **Buy Now:** available only while `BidCount == 0`. Using it closes the auction as sold with reason `buy_now`.
- **Terms change:** clears **both** acceptances (the editor re-accepts the new version). Either party may change terms while the agreement is pending.
- **Pickup address:** shown to both parties once the status is agreed or later (the seller knows it anyway), and only if the handover is pickup.
- **Cancel (seller only):** allowed when pending past `accept_by` (48 h), or when agreed but unpaid past the handover date + 3 days.
- **Demo durations:** 5 min and 1 h are allowed besides 1/3/5/7/10 days.
- **Public point:** the cell centre of a 0.01° latitude grid, with the longitude cell widened by 1/cos(lat). Displayed distance is whole km, and 0 means "< 1 km".

## Accounts and trust (feature design §1)

- Trust tiers: T1 is a verified phone plus ID check (needed before a first sale). T2 is earned through completed deals.
- B2B uses org accounts with roles, approval limits and deposits (Phase 2).
