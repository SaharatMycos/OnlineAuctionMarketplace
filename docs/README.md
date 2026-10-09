# Online Auction Marketplace: Docs

Specification and design drafts for a **location-first**, multi-mode (C2C, B2C, B2B) online auction marketplace.

| Doc | What it covers |
|---|---|
| [01 Product spec](01-product-spec.md) | Vision, modes, goals, personas, release scope, business model, metrics, NFRs, compliance, open questions, **decision log** and change log |
| [02 Feature design](02-feature-design.md) | Accounts/orgs, listings, auction formats, bidding rules, auction close, **deal agreement and settlement**, shipping/pickup, problem reports, **location-based discovery**, notifications, seller/buyer tools, procurement, live, admin, feature matrix |
| [03 System design](03-system-design.md) | Architecture and **stack (C#/.NET 10 + Next.js)**, **architecture decision (modular monolith)**, bidding engine, **location and geo search**, state machines, data model, API sketch, security, observability, testing |
| [04 Project setup](04-project-setup.md) | How the codebase was created, step by step: toolchain, scaffold commands, code layout, Docker, verification, Claude Code skill |
| [05 Architecture, design and structure](05-architecture.md) | How the system is shaped and why: context and container diagrams, modules and how they talk, data/consistency/security design, repository and in-module structure, naming conventions, and the **23 principles** used, with trade-offs |
| [06 Web frontend (Next.js)](06-web-frontend.md) | How `web/` is set up: App Router structure, BFF proxy and cookies, runtime config, SignalR, running with Docker or `next dev`, from-scratch steps, Docker image, recipes and gotchas |

## Key decisions so far
- **Location-first:** "Near you" by default, with radius, distance, map and pickup promoted. Exact addresses are private.
- **No escrow and no in-app payment.** After a win, a **deal agreement** (fixed price + handover/payment terms) is accepted by both sides. Payment happens outside the app, and both confirm "Deal paid", then the buyer marks Received.
- **Modular monolith** (`api`, `realtime`, `worker`). Extract services only when a measured signal calls for it.

Full list with reasons: [Product spec §10](01-product-spec.md#10-decision-log).

## Clickable prototype
[../mockup/](../mockup/index.html) (vanilla HTML/CSS/JS, no build step). Run `pwsh -File mockup/serve.ps1` and open http://localhost:5173, or just open `mockup/index.html` in a browser. Maps load Leaflet and OpenStreetMap tiles from the internet. Offline, everything else still works. Other bidders and the seller's responses are simulated.

Status: Draft v0.2 (2026-10-09).
