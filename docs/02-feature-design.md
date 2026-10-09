# Online Auction Marketplace: Feature Design

| | |
|---|---|
| Status | Draft v0.2 |
| Date | 2026-10-09 |
| Related | [Product spec](01-product-spec.md) · [System design](03-system-design.md) |

Phase tags: **[P1]** MVP, **[P2]** B2B release, **[P3]** procurement/live.

---

## 1. Accounts, identity and organisations

### 1.1 Account types
| Type | Can buy | Can sell | Verification | Phase |
|---|---|---|---|---|
| Individual | Yes | Yes (C2C) | Email + phone. Sellers verify ID before a first sale. | P1 |
| Business seller | Yes | Yes (B2C/B2B) | Business registration, tax ID (via a KYC/KYB vendor or manual review) | P1 |
| Business buyer | Yes (B2B auctions) | Optional | Business registration + tax ID | P2 |

One person can belong to several organisations and switch context ("buying as Acme Ltd").

### 1.2 Organisation accounts [P2]
- **Roles:** Owner, Admin, Buyer, Seller/Lister, Finance (deal agreements, invoices, platform fees), Viewer.
- **Spending limits** per member, per auction and per month.
- **Approval workflow:** a bid or purchase above a threshold needs approval. For live auctions, approval must happen *before* the bid (pre-approved max), because bids are binding.
- Shared watchlists, saved searches and addresses. Tax-exemption certificates are stored on the org.
- Audit log of who bid what on behalf of the org.

### 1.3 Trust levels
Every account has a trust tier that gates what it can do:

| Tier | How earned | Unlocks |
|---|---|---|
| T0 New | Sign-up | Browse, watch, bid up to a low cap |
| T1 Verified | Phone + ID verified | Normal bidding, C2C selling (limited listings) |
| T2 Established | N completed deals (confirmed paid by both sides), good ratings, no open problem reports | Higher limits, more listings |
| T3 Business verified | Business KYC passed | B2C storefront, B2B auctions |

Sellers can also require "verified bidders only", a minimum feedback score, or a deposit on any auction.

---

## 2. Listings

### 2.1 Listing creation [P1]
- Guided flow: category → title → photos (up to 24, plus video in P2) → item specifics (category-driven attributes) → condition → description → **item location + delivery options** (pickup / shipping, see §9.2) → format and pricing → shipping → returns → preview.
- **Smart assist:** suggested category and title from photos, a price guide from comparable sold items, and a missing-info checklist.
- Drafts autosave. Listings can be scheduled to start at a set time.
- **Bulk tools** for businesses: CSV/Excel import, templates, a Seller API [P2], and relisting of unsold items.

### 2.2 Lots and manifests [P2]
- A **lot** groups many items sold as one (e.g. a pallet of returns), with an attached manifest (CSV), condition grading and estimated retail value.
- An **auction event / catalogue** groups many lots with staggered closing times (e.g. lot 1 closes at 14:00:00, lot 2 at 14:00:30 …).
- Inspection window and pickup location for heavy goods.

### 2.3 Moderation
- Automated checks run before going live: prohibited keywords/categories, image classifiers, counterfeit signals, and price anomalies.
- Risky listings go to the manual review queue. Rights-owner reporting program for IP takedowns.

---

## 3. Selling formats

| Format | How it works | Modes | Phase |
|---|---|---|---|
| **English (ascending)** | Open bids rise by increments, and the highest bid at close wins. Proxy bidding is supported. | C2C, B2C, B2B | P1 |
| **Buy Now** | Fixed price alongside an auction. Usually disappears after the first bid (or once the reserve is met). | C2C, B2C | P1 |
| **Fixed price + Make Offer** | No auction. Buyers send offers, and the seller can accept, counter or decline (auto-rules allowed). | All | P1 |
| **Sealed-bid first-price** | Each bidder submits one hidden bid, and the highest pays their own bid. | B2B | P2 |
| **Sealed-bid second-price (Vickrey)** | Hidden bids, and the highest bidder pays the second-highest bid (plus increment). | B2B | P2 |
| **Dutch / descending clock** | Price starts high and drops on a schedule, and the first to accept wins. Good for perishable or time-sensitive stock. | B2C, B2B | P2 |
| **Multi-unit (uniform price)** | Seller offers Q identical units. The top Q bids win, and all pay the lowest winning bid. | B2C, B2B | P2 |
| **Reverse auction** | A buyer posts a requirement, and suppliers bid the price *down*. The buyer awards to the lowest bid or the best weighted score. | B2B | P3 |
| **Live (webcast)** | An auctioneer runs lots in real time. Online and floor bidders compete. | B2B, B2C | P3 |

### 3.1 Common auction settings
- Start price, **reserve price** (hidden; we show "Reserve not met / met"), Buy Now price.
- Duration (1, 3, 5, 7, 10 days) or a specific end time.
- Increment table (default below; sellers in B2B can set custom).
- Soft-close settings (§4.4).
- Visibility: public, verified bidders only, **private/invite-only** [P2].
- Bidder requirements: deposit, minimum feedback, region, business-only.
- Buyer's premium % (B2B, must be shown before bidding).

**Default increment table (example, currency-neutral):**
| Current price | Increment |
|---|---|
| 0 – 0.99 | 0.05 |
| 1 – 4.99 | 0.25 |
| 5 – 24.99 | 0.50 |
| 25 – 99.99 | 1 |
| 100 – 249.99 | 2.50 |
| 250 – 499.99 | 5 |
| 500 – 999.99 | 10 |
| 1,000 – 2,499.99 | 25 |
| 2,500 – 4,999.99 | 50 |
| 5,000+ | 100 |

---

## 4. Bidding engine

### 4.1 Proxy (automatic) bidding [P1]
The bidder enters a **maximum**. The system bids for them, one increment at a time, only as far as needed to stay ahead.

Rules when a new max bid `M_new` arrives against the current leader's max `M_lead` at visible price `P`:
1. If `M_new < P + increment(P)`, reject the bid as too low.
2. If `M_new > M_lead`, the new bidder leads, and visible price = `min(M_new, M_lead + increment(M_lead))`.
3. If `M_new <= M_lead`, the leader stays, and visible price = `min(M_lead, M_new + increment(M_new))`.
4. Equal maxima: the **earlier** bid wins.
5. If the reserve is not yet met and a max reaches the reserve, the price jumps to the reserve.

The leader can raise their own max without raising the visible price. Maxima are never shown to other users.

### 4.2 Bid validation
Before a bid is accepted, check that:
- the auction is live and the bid is before the end time (server clock),
- the bidder is eligible (trust tier, deposit, invite list, region, not the seller, not blocked by the seller),
- the bid is within the org spending limit or has pre-approval,
- the amount is ≥ the minimum next bid and has correct precision/currency,
- the bidder is within rate limits and passes the fraud score.

**Bids are binding.** The bidder confirms once, with a clear total that includes buyer's premium, tax estimate and shipping.

### 4.3 Bid retraction
Allowed only in narrow cases: an obvious typo (e.g. 1000 vs 100) right after bidding, the item description materially changed, or the seller cannot be contacted. Retractions are logged and count toward the bidder's risk score.

### 4.4 Anti-sniping (soft close) [P1]
If a bid is accepted in the last **X** minutes (default 2), the end time extends to **now + X** minutes. Optional maximum total extension per auction (e.g. 30 min). For lot catalogues, an extension of one lot does not delay the others.

### 4.5 Real-time experience
- Live price, bid count, time left (synced to the server clock offset), and "You're winning / outbid".
- Outbid push/email/SMS (user-configurable), "ending soon" reminders for watched items.
- Bid history with masked bidder handles (e.g. `a***9 (152)`).

### 4.6 Outages and fairness
If bidding is unavailable (platform incident) for more than N minutes during the final M minutes of an auction, affected auctions are **automatically extended**, and the incident is logged and shown on the listing.

### 4.7 Shill-bidding and collusion detection
Signals: bidder linked to the seller (device, IP, phone, address, location), bidder who repeatedly bids on one seller and never wins, bids that nudge just under hidden maxima, retractions. Actions: hold the auction result, review, cancel bids, suspend accounts.

---

## 5. Auction close and settlement

### 5.1 Close
At end time (after any extension), the system determines the outcome:
- **Sold:** highest valid bid ≥ reserve. Creates an order and its **deal agreement** (§6) for the winner at the final price (+ buyer's premium).
- **Unsold:** no bids, or reserve not met. The seller may send a **Second Chance / offer to the top bidder**, or relist.
- Sealed-bid auctions reveal results only at close. Bidders see their own rank/outcome and the clearing price, if the seller allows it.

### 5.2 Agreement window
- The winner must accept the deal agreement (§6) within **48 h** (C2C/B2C) or a seller-defined window (B2B, often 3–5 business days).
- Non-completion flow: reminder → case opened → after the window (or if the deal isn't confirmed paid by the agreed handover date + grace period), the seller can cancel and offer to the next bidder. The non-completing party gets a strike, and repeated strikes restrict bidding or selling.
- For B2B, a **bidder deposit** is held before bidding and is forfeited for non-completion (per published terms) or credited to the purchase.

---

## 6. Deal agreement and settlement

**MVP decisions (2026-10-09):**
- **No escrow and no in-app payment.** The platform never collects, holds or moves buyer money.
- **After a win, the platform creates a deal agreement.** Buyer and seller both accept it, settle payment directly between themselves, and then **both confirm the deal is paid**.

### 6.1 Deal flow (all modes) [P1]
| Status | Who acts | What happens |
|---|---|---|
| **Agreement created** | System, at auction close / Buy Now / accepted offer | A **deal agreement** (numbered, e.g. `AG-1002`) is generated and sent to both parties (email + in-app). It records the item, parties, **agreed price** (winning bid + buyer's premium + shipping, if any), handover method (local pickup / shipping), handover date, payment method (cash on handover, bank transfer, mobile/QR, other) and notes. |
| **Agreed** | Buyer **and** seller each tap "Accept agreement" | The buyer can adjust handover method, date, payment method and notes before accepting. The **price is fixed** by the auction result. Each acceptance is timestamped (click-to-accept record). Once both accept, the exact pickup address is revealed to the buyer and the agreement is locked. |
| **Deal paid** | Buyer taps "Confirm deal paid", **and** seller taps "Confirm payment received" | Payment happens off-platform, as agreed. The deal is marked paid only when **both** confirm. If they disagree (e.g. the seller says not received), it becomes a reported problem (§8). |
| **Received** | Buyer ("Mark as received") | The order is complete, and both sides can leave feedback. |

Rules:
- Every acceptance and confirmation is timestamped and kept in the agreement history (append-only audit). The agreement can be downloaded as a PDF.
- If either side changes terms after the other has accepted, the other side's acceptance resets (re-accept needed). The price can't change [P1]. Negotiated price changes could come in P2 (with a reason, e.g. a damaged item).
- If the agreement isn't accepted in time, or not confirmed paid by the handover date + grace period, the non-completion flow applies (§5.2).
- No bank or payment details are stored or shown by the platform. Parties exchange them in messages if needed.
- **Open options:** add a "Shipped + tracking" step for shipped deals? Auto-complete N days after "Deal paid" if the buyer never marks Received? Legal review of whether click-to-accept is enough as a binding sale record in the launch market.

> [!WARNING] Unverified
> Whether a click-to-accept agreement is legally binding as a sale contract depends on the launch country. This needs legal review.

### 6.2 B2B extras [P2]
- The deal agreement doubles as a pro-forma: tax and buyer's premium itemised, PO number field, reverse-charge VAT note where applicable. The seller issues the final tax invoice.
- An org admin can be required to co-accept agreements above a member's limit (reuses the approval workflow, §1.2).

### 6.3 Later: escrow via a PSP
Possible upgrade once there's volume: buyer pays through checkout, a PSP holds funds, the seller ships, and funds are released after delivery plus an inspection window. Disputes would then freeze funds. This needs a marketplace PSP (seller KYC, split payments), so it is out of MVP scope.

### 6.4 Fees
- Configurable fee schedules by mode, category and seller tier.
- Because the platform doesn't touch the sale money, **final value fees are invoiced to sellers monthly** (card on file or bank transfer).
- Seller statements and downloadable fee/tax reports.

---

## 7. Shipping and fulfilment

| Option | Modes | Phase |
|---|---|---|
| Seller-calculated or flat-rate shipping | All | P1 |
| Platform shipping labels (carrier integration) + tracking | C2C, B2C | P1 |
| Local pickup (cash or transfer on pickup) | All | P1 |
| Freight quotes / LTL, pickup appointment scheduling | B2B | P2 |
| International shipping with duties estimate | B2C | P3 |

Pickup: at handover, buyer and seller both confirm "Deal paid" (cash or transfer), and the buyer marks Received. Local pickup is promoted in the location-first experience (§9.5).

Shipping: shipped deals follow the same agreement flow. The seller can add tracking to the order. A formal "Shipped" step is an open option (§6.1).

---

## 8. Post-sale: reviews, returns, problem reports

- **Reviews:** two-way ratings after completion (buyer→seller detailed ratings, seller→buyer positive/neutral/issue). Reviews stay hidden until both sides post or 14 days pass, to reduce retaliation.
- **Returns:** seller return policy (none / 14 / 30 days). B2C defaults follow local consumer law.
- **Report a problem:** item not received, not as described, damaged, counterfeit, the other party didn't accept or show up, or a **payment disagreement** (one side confirmed paid, the other says not received). The platform holds no funds in the MVP, so it mediates: it contacts the seller, records evidence (photos, chat, payment slip), and can apply strikes, suspend sellers and show the case on seller reputation. Refunds are between buyer and seller.
- **Fraud risk without escrow:** fake sellers taking payment and not shipping. Mitigations: verified phone/ID before a first sale, a written deal agreement both sides accept, deal paid only when **both** confirm, cash-on-handover promoted for local deals (§9.5), new-seller limits (trust tiers §1.3), and fast takedown of reported sellers.

---

## 9. Location-based discovery [P1]

**Product decision (2026-10-09): location-first.** The default home feed and search show what's near the buyer. Shipping is still allowed, but local pickup is promoted.

### 9.1 Buyer location
- Set on first visit: "Use my location" (browser/device GPS, with consent), or enter a city/postcode. If neither, fall back to an approximate IP-based city.
- Always visible in the header ("📍 Chicago · 25 km") and changeable from any page.
- **Search radius:** 5 / 10 / 25 (default) / 50 / 100 km / Anywhere. Units follow locale (km or miles).
- Saved per account, plus "saved places" (home, work) [P2].

### 9.2 Listing location
- Seller sets the item location when listing, defaulting to their profile address. Business sellers can set a location per warehouse/store.
- **Delivery options:** Pickup only · Pickup or shipping · Shipping only.
- **Privacy:** the exact address is stored but **never shown publicly**. Buyers see an area name ("Wicker Park, Chicago") and an approximate distance, and maps show a fuzzed area (~1 km circle), not a pin. Distance is computed from the fuzzed point and rounded, to prevent trilateration. The exact pickup address goes only to the winning buyer, once both sides accept the deal agreement.

### 9.3 Home feed
1. **Near you:** live listings within the radius, ranked by a blend of distance, ending time and relevance.
2. **Ships to you:** listings beyond the radius that offer shipping.
3. Ending soon, category and B2B collections, each showing distance on every card.

Pickup-only listings outside the buyer's radius are hidden from the feed.

### 9.4 Search
- Filters: **distance radius**, **delivery (pickup / shipping)**, category, price, format, ending time, condition, seller type, verified-only.
- Sort: **nearest first** (default when no keyword), ending soonest, price, most bids, best match (default with a keyword: relevance boosted by proximity).
- Results include out-of-radius items that ship, unless the buyer turns off "Include items that ship".
- **Map view** toggle: approximate listing areas, the buyer's radius circle, and tap-through to listings.

### 9.5 Local pickup and meetups
- Pickup listings show "Pickup in <area>". After the win, the deal agreement sets the handover date. Once both accept, it shows the exact address (or a suggested **safe meetup spot**, e.g. a police-station exchange zone [P2]) and an "Arrange pickup time" message thread.
- Pickup deals use the same Agreement → Agreed → Deal paid → Received flow. Cash on handover is the default payment method.

### 9.6 Alerts and other discovery
- Saved searches include location + radius ("Road bikes within 10 km"). New matching listings trigger alerts.
- "Ending soon near you" push for watchers within the radius.
- Category pages, the auction calendar, "no bids yet" collections, recently viewed and recommendations (proximity is a ranking signal).
- SEO-friendly listing and city/category landing pages ("Used furniture auctions in Chicago"). Sold-price history (opt-in per category).

### 9.7 B2B
- Lots show the pickup/warehouse location. Buyers can filter by distance to their own warehouses. Freight quotes use origin/destination.

---

## 10. Communication and notifications

- Buyer↔seller messaging tied to a listing or order. Contact details are masked before sale, and links/phone numbers are scanned.
- Q&A on listing (public answers optional).
- Channels: in-app, push, email, SMS (critical only: outbid near close, won, agreement deadline). User preferences per event type.
- Key events: outbid, winning, ending soon, auction won/lost, reserve not met, offer received/countered, deal agreement created, other party accepted, agreement deadline approaching, other party confirmed paid, buyer marked received, problem reports.

---

## 11. Seller tools

- Seller hub dashboard: active/scheduled/unsold listings, **agreements to accept**, deals awaiting payment confirmation, upcoming handovers/pickups, offers, messages, performance metrics (deal completion rate, problem reports, late handovers).
- Storefront (B2C/B2B): branded page, categories, about/policies, follow store.
- Promotions: promoted listings, store coupons, featured auction events.
- Analytics: views, watchers, bidders, sell-through, average final price vs start price.
- Seller API [P2]: listings, inventory, orders, shipping, webhooks.

## 12. Buyer tools

- "My auctions": bidding (winning/outbid), won, lost, watching, offers, and purchases with their deal-agreement status (Agreement pending / Agreed / Paid / Received).
- Location: change location and radius from anywhere. Saved searches with radius alerts (§9).
- Bid-snipe protection is built in (soft close). Users set a max bid and walk away.
- Org buyer [P2]: approval requests, budget view, invoice download, PO matching.

## 13. Procurement: reverse auctions [P3]

1. The buyer creates an **RFQ/event**: specs, quantity, delivery terms, timeline, attachments, ceiling price (optional), invited/approved suppliers.
2. Suppliers prequalify (documents, certifications).
3. Live reverse-auction window: suppliers see their **rank** (and optionally the best price), and bids must go down by a minimum decrement. Soft close applies.
4. Award: lowest price, or a **weighted score** (price + delivery + quality score). Partial awards are allowed across suppliers.
5. The award produces a **deal agreement** (acting as the PO) between buyer and supplier, with quantity, unit price, delivery and payment terms. Payment happens outside the platform per those terms, and both sides confirm it as in §6.

## 14. Live auctions [P3]

- Auctioneer console: open/close lot, "fair warning", accept floor/phone bids, increment override.
- Bidder view: video stream, current lot, one-tap bid at next increment, max-bid pre-set.
- Latency target: online bid reaches the auctioneer in < 500 ms. A clerk confirms floor bids.

---

## 15. Admin and operations console

- User/org management, KYC status, trust tier overrides, suspensions with reason codes.
- Listing moderation queue, prohibited-item rules, category and attribute management.
- Auction tools: cancel/void auction, remove bids (with logged reason), extend end time.
- Problem-report queue with SLA timers. Read-only view of the deal-agreement history (versions, acceptances, confirmations) to mediate. Strikes and suspensions. (No refunds: the platform holds no funds.)
- Finance: fee schedules, monthly seller fee invoices, deal status reports, tax exports.
- Location tools: fix bad geocodes, review listings with suspicious locations.
- Content: homepage collections, featured events, banners.
- Role-based access for staff with full audit log.

---

## 16. Feature matrix by mode

| Feature | C2C | B2C | B2B |
|---|:-:|:-:|:-:|
| English auction + proxy | ✅ | ✅ | ✅ |
| Buy Now / Make Offer | ✅ | ✅ | ✅ |
| Sealed bid / Dutch / multi-unit | – | ⚪ | ✅ |
| Reverse auction | – | – | ✅ |
| Private / invite-only auctions | – | ⚪ | ✅ |
| Lots & manifests | ⚪ | ✅ | ✅ |
| Bidder deposits | – | ⚪ | ✅ |
| Buyer's premium | – | ⚪ | ✅ |
| Deal agreement + both-side confirmations (Agreement → Agreed → Deal paid → Received) | ✅ | ✅ | ✅ |
| Escrow via PSP (later) | ⚪ | ⚪ | – |
| Storefront | – | ✅ | ✅ |
| Org accounts, roles, approvals | – | – | ✅ |
| Tax invoices / reverse charge | – | ✅ | ✅ |
| Freight / pickup scheduling | ⚪ | ⚪ | ✅ |
| Location-first discovery (Near you, radius, map) | ✅ | ✅ | ✅ (warehouse distance) |

✅ standard · ⚪ optional/seller choice · – not offered
