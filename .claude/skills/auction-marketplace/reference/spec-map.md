# Spec map

Where to look before changing a module. All paths are relative to the repo root.

| Module | Feature design (`docs/02-feature-design.md`) | System design (`docs/03-system-design.md`) |
|---|---|---|
| Identity | §1 Accounts, trust tiers, orgs | §6 data model, §8 security |
| Location | §9 Location-based discovery | §4 Location and geo search |
| Catalog | §2 Listings (item location, delivery) | §6 data model |
| Auctions | §3 Formats, §4 Bidding engine, §5 Auction close | §3 Bidding engine, §5 state machines |
| Orders | §5.2 Agreement window, §6 Deal agreement and settlement, §7 Shipping and pickup | §5 order state machine, §6 `deal_agreement`, `deal_event`, §7 agreement API |
| Billing | §6.4 Fees invoiced monthly | §6 `fee_invoice` |
| Reviews | §8 Problem reports, reviews | §6 `problem_report` |
| Messaging | (buyer-seller messages) | §6 |
| Notifications | §10 Notifications | §2 outbox, worker |
| Search | §9 discovery, §12 buyer tools (saved searches) | §4 OpenSearch geo queries |
| Admin | §15 Admin | §8 audit, §9 observability |
| (later) RFQ / Live | §13 Reverse auctions, §14 Live auctions | n/a |

Cross-cutting:
- Architecture explained (diagrams, module communication, project structure, target in-module layout, principles): `docs/05-architecture.md`
- Architecture decision (modular monolith, when to extract services): system design §2
- Stack decision: system design §1 and product spec §10
- NFRs (latency, fairness, privacy): product spec §7
- Testing strategy (property tests, concurrency, clock, geo privacy, deal flow): system design §10
- Feature-by-mode matrix: feature design §16
