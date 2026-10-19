# Production experience preview

`mockup/` is the existing static prototype (there is no separate `mock/` directory). Open `index.html` directly, or serve it from the repository root:

```powershell
pwsh -File mockup/serve.ps1
```

Browse to `http://localhost:5173`. No backend, database, AI key or dependency install is required. Leaflet maps require internet access and display a fallback when unavailable.

## Try the AI preview

The mock starts signed out. Use **Sign in** or **Sign up** in the header before trying bids or listing creation.

1. Type part of a seeded auction title in the header search; use the browser's native autocomplete list or submit your own query. Topic queries such as `photography` also match locally inferred item topics.
2. Open **Open auction** and start with photos: choose or drag in JPEG/PNG/WebP images, or select a camera, bicycle or chair demo image. Up to 24 images of 10 MB each are supported; remove photos or choose a cover before publishing.
3. Adding a photo automatically prepares a mock title, description, category and tags. Review the result and choose **Use these suggestions**, then edit any field. Existing text is never overwritten automatically. The first recognizable filename (cover first) drives the simulated subject; an unknown filename gets a generic draft requiring seller input.
4. Complete location and pricing, then publish. At least one photo is required in this image-first mock flow. Uploaded photos appear on auction cards and the listing gallery; thumbnail buttons switch the main image. Find your new listing under **My auctions**.

The header, discovery sections, account overview, image-first photo placement and English-auction fields follow the production web app. The mock additionally previews photo-based assistance and requires a photo; production photos remain optional and production AI is not implemented. Primary discovery excludes fixed-price, sealed and Dutch listings; business mode and procurement navigation are hidden. Existing richer item, map, bidding and deal simulations remain prototype behavior. Authentication is simulated locally, and server-side photo processing is not simulated here.

## Sign in, sign up and sign out

- Guests can browse and search. Opening `/sell`, `/me` or a deal route requires sign-in. Bidding and Buy Now also require sign-in. Return paths stay inside the mock and are preserved between sign-in and sign-up.
- **Sign in** accepts any demo email. Expand **Hint: trying the demo?** for Alex, Bob and Carol shortcuts. Unknown emails create a local account with an email-derived display name, matching the production development-account behavior.
- **Sign up** collects a display name and email, creates a local account and signs in automatically. Existing emails sign into the existing account without renaming it. There are no passwords, email verification, real sessions or requests to the backend.
- The header shows the current name and **Sign out**. Sign-out returns to browsing, closes dialogs, clears private runtime state and invalidates pending AI results. A new account starts with no deals; Alex has seeded demo deals.
- Account names/emails persist in local storage, and sign-in persists in session storage for the current tab. Use demo details only. Storage failures fall back to in-memory behavior.
- Changing accounts resets simulated auctions, bids, deals, drafts and uploaded photos as a fresh page visit would; it does not model a shared persistent marketplace. Account registrations remain saved locally.

AI is deterministic keyword matching with a short simulated delay, not a model call. The photo assistant uses known demo images or uploaded filenames, not visual recognition. It avoids invented brand, model, condition and accessory claims. Arbitrary uploads with generic filenames receive a generic draft. Images are decoded for preview validation and retained only through temporary browser object URLs, with no upload to a server; removal releases those URLs. Built-in demo illustrations are local SVG fixtures. Auctions and uploaded photos reset on reload. Use demo text for private addresses; those addresses are discarded on publication. No listing or image content is sent to an AI service.

Implementation: `auth.js` owns local accounts, session state and account screens; `ai.js` contains the local topic matcher and production-aligned preview pages; `app.js` retains the existing router, location, auction and deal simulation. Production seller assistance remains planned in [feature design §2](../docs/02-feature-design.md); bidder suggestions were cut from the production scope.

## Validation

Run from the repository root using Node.js:

```powershell
node --check mockup/ai.js
node --check mockup/auth.js
node --check mockup/app.js
node --test mockup/ai.test.cjs
```

The regression tests load the actual scripts with minimal DOM adapters and controlled timers. They verify guest navigation, sign-in/up/out, return paths, account isolation, session restoration, storage fallback, rendering, late-result invalidation, photo type/size/count validation, failed image decoding, cover selection, image-first publication, photo retention in cards, explicit suggestion application, private-address exclusion and escaping. They do not replace visual browser checks, actual browser image decoding or native file-picker/autocomplete popup checks.
