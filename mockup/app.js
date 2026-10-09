'use strict';

// ---------- helpers ----------
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const r2 = n => Math.round(n * 100) / 100;
const money = n => {
  const v = r2(n);
  return '$' + v.toLocaleString('en-US', { minimumFractionDigits: Number.isInteger(v) ? 0 : 2, maximumFractionDigits: 2 });
};
const pick = arr => arr[Math.floor(Math.random() * arr.length)];
const rand = (a, b) => a + Math.floor(Math.random() * (b - a + 1));

const FORMAT_LABEL = { english: 'Auction', sealed: 'Sealed bid', dutch: 'Dutch clock', fixed: 'Buy now' };
const MODE_LABEL = { c2c: 'C2C', b2c: 'B2C', b2b: 'B2B' };
const FEES = { c2c: 0.10, b2c: 0.08, b2b: 0.05 };
// No escrow in the MVP: the buyer pays the seller directly and updates the status.
const ORDER_STAGES = [
  ['Deal agreement created', 'Price and terms from the winning bid, ready for both sides to review.'],
  ['Agreement accepted', 'You and the seller both accept the price and terms.'],
  ['Deal paid', 'You and the seller both confirm the payment was made.'],
  ['Received', 'Mark as received once you have the item.'],
];
const ORDER_PILL = ['Agreement pending', 'Agreed', 'Paid', 'Received'];
const orderPillClass = o => o.stage === 3 ? 'good' : o.stage === 0 ? 'warn' : 'info';
const isoDate = t => new Date(t).toISOString().slice(0, 10);

function countdown(ms) {
  if (ms <= 0) return 'Ended';
  const s = Math.floor(ms / 1000);
  const d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600), m = Math.floor(s % 3600 / 60), sec = s % 60;
  if (d) return `${d}d ${h}h`;
  if (h) return `${h}h ${m}m`;
  return `${m}m ${String(sec).padStart(2, '0')}s`;
}

// ---------- state ----------
const S = {
  mode: 'all',
  account: 'personal',
  watch: new Set(['a2', 'a11']),
  loc: { ...PLACES[0], radius: 25 },
  filters: { formats: new Set(), sellers: new Set(), modes: new Set(), delivery: new Set(), ships: true, ending: 'any', sort: 'nearest' },
  searchView: 'list',
  lastResults: [],
  auctions: {},
  orders: SEED_ORDERS.map(o => ({ ...o })),
  approvals: SEED_APPROVALS.map(a => ({ ...a })),
  offers: [],
  meTab: 'bidding',
  rfqView: 'supplier',
  rfq: { ...RFQ, bids: RFQ.bids.map(b => ({ ...b })), endAt: Date.now() + RFQ.endsIn * 1000, awarded: null },
  draft: null,
  viewing: null,
};

function seedHistory(l) {
  const h = [];
  if (!l.bids || l.format !== 'english') return h;
  let amount = l.price;
  const now = Date.now();
  for (let i = 0; i < Math.min(l.bids, 8); i++) {
    h.push({ name: HANDLES[i % 3], amount, t: now - (i + 1) * rand(60, 900) * 1000, auto: i === 0 });
    amount = r2(Math.max(l.start, amount - increment(amount) * rand(1, 3)));
  }
  return h;
}

for (const l of LISTINGS) addAuction(l);

function addAuction(l) {
  const a = {
    ...l,
    endAt: Date.now() + l.endsIn * 1000,
    status: 'live',
    history: seedHistory(l),
    youBid: false,
    yourSealed: null,
  };
  a.leader = l.format === 'english' && l.bids ? { name: HANDLES[0], max: l.rivalMax } : null;
  if (l.format === 'dutch') a.dutchStartedAt = Date.now() - 20000;
  S.auctions[a.id] = a;
  return a;
}

const all = () => Object.values(S.auctions);

// ---------- location (spec: feature design §9) ----------
// Public point snapped to a ~1 km grid; exact lat/lng never leaves the listing record.
const fuzz = a => ({ lat: Math.round(a.lat * 100) / 100, lng: Math.round(a.lng * 100) / 100 });
function distanceKm(p, q) {
  const toR = x => x * Math.PI / 180;
  const dLat = toR(q.lat - p.lat), dLng = toR(q.lng - p.lng);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toR(p.lat)) * Math.cos(toR(q.lat)) * Math.sin(dLng / 2) ** 2;
  return 2 * 6371 * Math.asin(Math.sqrt(h));
}
const dist = a => distanceKm(S.loc, fuzz(a));
const fmtDist = d => d < 1 ? '< 1 km' : `${Math.round(d).toLocaleString('en-US')} km`;
const isLocal = a => dist(a) <= S.loc.radius;
const reachable = a => isLocal(a) || a.delivery !== 'pickup';
const radiusLabel = () => Number.isFinite(S.loc.radius) ? `${S.loc.radius} km` : 'Anywhere';
const RADII = [5, 10, 25, 50, 100, Infinity];
const radiusOptions = () => RADII.map(r => `<option value="${r}" ${S.loc.radius === r ? 'selected' : ''}>${Number.isFinite(r) ? r + ' km' : 'Anywhere'}</option>`).join('');
const DELIVERY_LABEL = { pickup: 'Pickup only', both: 'Pickup or shipping', ship: 'Shipping only' };
const deliveryBadge = a => a.delivery === 'pickup' ? '<span class="badge pickup">📍 Pickup</span>'
  : a.delivery === 'ship' ? '<span class="badge">📦 Ships</span>' : '<span class="badge pickup">📍 Pickup</span><span class="badge">📦 Ships</span>';
function updateLocChip() {
  const el = $('#loc-label');
  if (el) el.textContent = `${S.loc.name} · ${radiusLabel()}`;
}
const inMode = a => S.mode === 'all' || (S.mode === 'business' ? a.mode === 'b2b' : a.mode !== 'b2b');

// ---------- bidding engine (proxy bidding, spec §4.1) ----------
function minNext(a) {
  return a.leader ? r2(a.price + increment(a.price)) : a.start;
}

function placeBid(a, name, max) {
  if (a.status !== 'live') return { ok: false, msg: 'This auction has ended.' };
  max = r2(max);
  const now = Date.now();
  const push = (who, amount, auto = false) => { a.history.unshift({ name: who, amount: r2(amount), t: now, auto }); a.bids++; };

  if (a.leader && a.leader.name === name) {
    if (max <= a.leader.max) return { ok: false, msg: `Your max is already ${money(a.leader.max)}.` };
    a.leader.max = max;
    if (a.reserve && a.price < a.reserve && max >= a.reserve) { a.price = a.reserve; push(name, a.price); }
    const ext = softClose(a);
    return { ok: true, lead: true, ext, msg: `Max bid raised to ${money(max)}. You’re still the highest bidder.` };
  }

  const min = minNext(a);
  if (max < min) return { ok: false, msg: `Enter ${money(min)} or more.` };

  if (!a.leader) {
    a.leader = { name, max };
    a.price = a.start;
    push(name, a.price);
  } else if (max > a.leader.max) {
    const prev = a.leader;
    a.price = r2(Math.min(max, prev.max + increment(prev.max)));
    if (prev.max > a.history[0]?.amount) push(prev.name, prev.max, true);
    push(name, a.price);
    a.leader = { name, max };
  } else {
    // Existing leader wins ties (earliest bid) and auto-bids just above the newcomer.
    a.price = r2(Math.min(a.leader.max, max + increment(max)));
    push(name, max);
    push(a.leader.name, a.price, true);
  }
  if (a.reserve && a.price < a.reserve && a.leader.max >= a.reserve) a.price = a.reserve;
  const ext = softClose(a);
  const lead = a.leader.name === name;
  return {
    ok: true, lead, ext,
    msg: lead ? `You’re the highest bidder at ${money(a.price)}.` : `Outbid instantly: another bidder’s max is higher. Current price ${money(a.price)}.`,
  };
}

// Anti-sniping soft close (spec §4.4)
function softClose(a) {
  const win = (a.softClose || 120) * 1000;
  if (a.endAt - Date.now() < win) {
    a.endAt = Date.now() + win;
    a.extensions = (a.extensions || 0) + 1;
    return true;
  }
  return false;
}

function dutchPrice(a) {
  const d = a.dutch;
  const steps = Math.floor((Date.now() - a.dutchStartedAt) / (d.every * 1000));
  return Math.max(d.floor, d.start - steps * d.drop);
}

function closeAuction(a) {
  a.status = 'closed';
  if (a.format === 'english') {
    a.sold = !!a.leader && (!a.reserve || a.price >= a.reserve);
    if (a.sold && a.leader.name === 'You') {
      const id = createOrder(a, a.price);
      toast(`🎉 You won <b>${a.title}</b> for ${money(a.price)}. <a href="#/order/${id}">View order</a>`, 'good', 8000);
    } else if (a.youBid) {
      toast(`Auction ended: <b>${a.title}</b>. ${a.sold ? 'You didn’t win this time.' : 'Reserve not met.'}`);
    }
  }
  if (S.viewing === a.id) render();
}

function createOrder(a, amount) {
  const id = 'o' + (1000 + S.orders.length + 1);
  const now = Date.now();
  S.orders.unshift({
    id, title: a.title, emoji: a.emoji, hue: a.hue, amount,
    premium: a.premium ? r2(amount * a.premium / 100) : 0,
    ship: a.delivery === 'pickup' ? 0 : a.ship?.cost || 0, seller: a.seller.name, stage: 0, mode: a.mode, itemId: a.id,
    delivery: a.delivery, area: a.area,
    agreementNo: 'AG-' + id.slice(1), createdAt: now, acceptBy: now + (a.mode === 'b2b' ? 3 : 2) * 86400e3,
    terms: {
      handover: a.delivery === 'ship' ? 'Shipping' : 'Local pickup',
      date: isoDate(now + 3 * 86400e3),
      payment: a.delivery === 'ship' ? 'Bank transfer' : 'Cash on handover',
      note: '',
    },
  });
  setTimeout(() => toast(`📝 Deal agreement AG-${id.slice(1)} created and sent to you and ${a.seller.name}.`), 400);
  return id;
}

// The mockup has no seller UI, so the seller "responds" after a short delay.
function sellerResponds(o, field, apply, msg) {
  setTimeout(() => {
    o[field] = Date.now();
    apply();
    toast(msg, 'good');
    if (location.hash === '#/order/' + o.id) render();
  }, 1800);
}

// ---------- simulated market activity ----------
function botTick() {
  for (const a of all()) {
    if (a.status !== 'live' || a.format !== 'english') continue;
    const youLead = a.leader?.name === 'You';
    const chance = youLead ? (S.viewing === a.id ? 1 / 10 : 1 / 40) : (S.viewing === a.id ? 1 / 25 : 1 / 120);
    if (Math.random() > chance) continue;
    const name = youLead ? pick(HANDLES.filter(h => h !== a.leader.name)) : pick(HANDLES.filter(h => h !== a.leader?.name));
    const base = youLead ? a.leader.max : minNext(a);
    const max = r2(Math.max(minNext(a), base + increment(base) * rand(-2, 3)));
    const res = placeBid(a, name, max);
    if (!res.ok) continue;
    if (youLead && a.leader.name !== 'You') {
      toast(`⚠️ You’ve been outbid on <a href="#/item/${a.id}">${a.title}</a>. Now ${money(a.price)}.`, 'bad', 6000);
    }
    if (res.ext && S.viewing === a.id) toast('⏱ Late bid: auction extended by 2 minutes (soft close).');
    updateAuctionUI(a);
  }
  rfqBotTick();
}

// ---------- RFQ / reverse auction ----------
function rfqRanked() {
  return [...S.rfq.bids].sort((x, y) => x.price - y.price);
}
function rfqScore(b) {
  const best = Math.min(...S.rfq.bids.map(x => x.price));
  const w = S.rfq.weights;
  return r2((best / b.price) * w.price + b.delivery * w.delivery / 100 + b.quality * w.quality / 100);
}
function rfqBotTick() {
  const r = S.rfq;
  if (r.awarded || Date.now() > r.endAt) return;
  if (Math.random() > (S.viewing === 'rfq' ? 1 / 12 : 1 / 60)) return;
  const bot = pick(r.bids.filter(b => b.name !== r.you));
  const best = Math.min(...r.bids.map(b => b.price));
  const target = r2(best - r.decrement * rand(0, 1));
  if (target < 0.6 || target >= bot.price) return;
  bot.price = target;
  if (r.endAt - Date.now() < r.softClose * 1000) r.endAt = Date.now() + r.softClose * 1000;
  if (S.viewing === 'rfq') {
    if (rfqRanked()[0].name !== r.you && S.rfqView === 'supplier') toast(`${bot.name} lowered their bid. Your rank changed.`);
    render();
  }
}

// ---------- UI primitives ----------
function toast(html, kind = '', ms = 4500) {
  const el = document.createElement('div');
  el.className = 'toast ' + kind;
  el.innerHTML = html;
  $('#toasts').append(el);
  setTimeout(() => el.remove(), ms);
}

function modal(html) {
  const m = $('#modal');
  m.innerHTML = `<div class="backdrop" data-action="close-modal"></div><div class="dialog" role="dialog" aria-modal="true">${html}</div>`;
  m.hidden = false;
  $('.dialog input, .dialog button.primary', m)?.focus();
}
function closeModal() { $('#modal').hidden = true; $('#modal').innerHTML = ''; }

const tile = (a, cls = '') => `<div class="tile ${cls}" style="--h:${a.hue}">${a.emoji}</div>`;
const modeBadge = a => `<span class="badge ${a.mode}">${MODE_LABEL[a.mode]}</span>`;
const sellerBadge = s => s.type === 'business'
  ? `<span class="badge ${s.verified ? 'ok' : ''}">${s.verified ? '✓ ' : ''}Business</span>`
  : `<span class="badge">Individual${s.verified ? ' ✓' : ''}</span>`;

function priceLabel(a) {
  if (a.format === 'english') return `<strong data-price="${a.id}">${money(a.price)}</strong><span class="muted small" data-bids="${a.id}">${a.bids} bid${a.bids === 1 ? '' : 's'}</span>`;
  if (a.format === 'sealed') return `<strong>Sealed</strong><span class="muted small">${a.sealedCount} bids · min ${money(a.start)}</span>`;
  if (a.format === 'dutch') return `<strong data-dutch="${a.id}">${money(dutchPrice(a))}</strong><span class="muted small">↓ dropping</span>`;
  return `<strong>${money(a.price)}</strong><span class="muted small">${a.offers ? 'or Best Offer' : 'Buy now'}</span>`;
}

function card(a) {
  const watched = S.watch.has(a.id);
  return `<a class="card" href="#/item/${a.id}">
    <div class="tile" style="--h:${a.hue}">${a.emoji}
      <span class="fmt">${FORMAT_LABEL[a.format]}</span>
      <button class="watch-btn" data-action="watch" data-id="${a.id}" aria-label="${watched ? 'Remove from' : 'Add to'} watchlist">${watched ? '♥' : '♡'}</button>
    </div>
    <div class="card-body">
      <div class="badges">${modeBadge(a)}${sellerBadge(a.seller)}</div>
      <h3>${a.title}</h3>
      <div class="price-row">${priceLabel(a)}</div>
      <div class="small muted">📍 ${a.area} · <b>${fmtDist(dist(a))}</b></div>
      <div class="card-foot">${deliveryBadge(a)}${a.format === 'fixed' ? '' : `<span class="small muted">⏱ <span class="cd" data-cd="${a.id}"></span></span>`}</div>
    </div>
  </a>`;
}

const cards = list => list.length ? `<div class="cards">${list.map(card).join('')}</div>` : `<div class="empty">Nothing here for this mode yet.</div>`;

// ---------- pages ----------
function pageHome() {
  const live = all().filter(a => a.status === 'live' && inMode(a));
  const byEnding = (x, y) => x.endAt - y.endAt;
  // Near you: distance first, nudged by how soon it ends (spec §9.3).
  const nearScore = a => dist(a) + Math.min(24, (a.endAt - Date.now()) / 3600e3) * 0.5;
  const near = live.filter(isLocal).sort((x, y) => nearScore(x) - nearScore(y));
  const shipsIn = live.filter(a => !isLocal(a) && a.delivery !== 'pickup' && a.mode !== 'b2b').sort(byEnding);
  const b2b = live.filter(a => a.mode === 'b2b' && reachable(a)).sort((x, y) => dist(x) - dist(y));
  return `
  <section class="hero">
    <div>
      <h1>Auctions near you.</h1>
      <p class="muted">Bid on things in your neighbourhood and pick them up today, or get them shipped from farther away. Proxy bidding, soft close, and a deal agreement for every win.</p>
      <button class="chip" data-action="loc" style="margin-top:10px">📍 Showing listings within <b>${radiusLabel()}</b> of <b>${S.loc.name}</b> · change</button>
      <form class="search" data-form="search" style="margin-top:12px">
        <input name="q" type="search" placeholder="Try “camera”, “pallet”, “LEGO”…" aria-label="Search">
        <button class="btn primary">Search</button>
      </form>
    </div>
    <div class="hero-modes">
      <a class="hero-mode" href="#/search?seller=individual"><span class="ic">🤝</span><span><strong>C2C</strong><br><span class="muted small">Buy from people near you. Rated sellers, masked contact details.</span></span></a>
      <a class="hero-mode" href="#/search?seller=business&mode=b2c"><span class="ic">🏬</span><span><strong>B2C</strong><br><span class="muted small">Verified stores, tax invoices, warranties and returns.</span></span></a>
      <a class="hero-mode" href="#/business"><span class="ic">🏢</span><span><strong>B2B</strong><br><span class="muted small">Lots, sealed bids, deposits, org approvals and reverse auctions.</span></span></a>
    </div>
  </section>
  <div class="cats">${CATEGORIES.map(c => `<a class="chip" href="#/search?cat=${encodeURIComponent(c.name)}">${c.emoji} ${c.name}</a>`).join('')}</div>

  <section class="section">
    <div class="section-head"><h2>📍 Near you <span class="muted small">within ${radiusLabel()} of ${S.loc.name}</span></h2><a href="#/search?view=map">Map view →</a></div>
    ${near.length ? cards(near) : `<div class="empty">Nothing listed within ${radiusLabel()} yet. <a href="#" data-action="loc">Widen your radius</a> or see what ships to you below.</div>`}
  </section>
  ${S.mode !== 'business' ? `<section class="section">
    <div class="section-head"><h2>📦 Ships to you <span class="muted small">from farther away</span></h2><a href="#/search">See all →</a></div>
    ${cards(shipsIn.slice(0, 8))}
  </section>` : ''}
  ${S.mode !== 'consumer' ? `<section class="section">
    <div class="section-head"><h2>Business lots & industrial <span class="muted small">nearest first</span></h2><a href="#/search?mode=b2b">See all B2B →</a></div>
    ${cards(b2b)}
  </section>` : ''}`;
}

function pageSearch(params) {
  const f = S.filters;
  // Filters come from the URL on each visit, so a hero link's filter doesn't stick to later searches.
  f.q = params.q || '';
  f.cat = params.cat || '';
  f.sellers = new Set(params.seller ? [params.seller] : []);
  f.modes = new Set(params.mode ? [params.mode] : []);
  f.delivery = new Set();
  if (params.view) S.searchView = params.view;
  const check = (set, val, label) => `<label><input type="checkbox" data-filter="${set}" value="${val}" ${f[set].has(val) ? 'checked' : ''}> ${label}</label>`;
  return `
  <div class="section-head"><h1>${f.cat || (f.q ? `Results for “${f.q}”` : 'Listings near you')}</h1>
    <div class="seg" role="group" aria-label="Results view">
      <button class="btn sm" data-action="search-view" data-v="list" aria-pressed="${S.searchView === 'list'}">☰ List</button>
      <button class="btn sm" data-action="search-view" data-v="map" aria-pressed="${S.searchView === 'map'}">🗺 Map</button>
    </div></div>
  <div class="search-layout">
    <aside class="filters panel">
      <fieldset><legend>Distance from ${S.loc.name}</legend>
        <select data-filter-select="radius" aria-label="Search radius">${radiusOptions()}</select>
        <label><input type="checkbox" data-filter-flag="ships" ${f.ships ? 'checked' : ''}> Include items that ship from farther away</label>
        <button class="btn sm ghost" data-action="loc">Change location</button>
      </fieldset>
      <fieldset><legend>Delivery</legend>
        ${check('delivery', 'pickup', '📍 Local pickup')}${check('delivery', 'ship', '📦 Shipping')}
      </fieldset>
      <fieldset><legend>Format</legend>
        ${Object.entries(FORMAT_LABEL).map(([k, v]) => check('formats', k, v)).join('')}
      </fieldset>
      <fieldset><legend>Trading mode</legend>
        ${check('modes', 'c2c', 'C2C (people)')}${check('modes', 'b2c', 'B2C (stores)')}${check('modes', 'b2b', 'B2B (business only)')}
      </fieldset>
      <fieldset><legend>Seller</legend>
        ${check('sellers', 'individual', 'Individual')}${check('sellers', 'business', 'Verified business')}
      </fieldset>
      <fieldset><legend>Ending</legend>
        <select data-filter-select="ending" aria-label="Ending within">
          ${[['any', 'Any time'], ['1h', 'Within 1 hour'], ['24h', 'Within 24 hours']].map(([v, l]) => `<option value="${v}" ${f.ending === v ? 'selected' : ''}>${l}</option>`).join('')}
        </select>
      </fieldset>
      <fieldset><legend>Sort</legend>
        <select data-filter-select="sort" aria-label="Sort">
          ${[['nearest', 'Nearest first'], ['ending', 'Ending soonest'], ['low', 'Price: low to high'], ['high', 'Price: high to low'], ['bids', 'Most bids']].map(([v, l]) => `<option value="${v}" ${f.sort === v ? 'selected' : ''}>${l}</option>`).join('')}
        </select>
      </fieldset>
      <button class="btn sm" data-action="save-search">🔔 Save this search</button>
    </aside>
    <div id="results">${searchResults()}</div>
  </div>`;
}

function curPrice(a) { return a.format === 'dutch' ? dutchPrice(a) : a.price; }

function searchResults() {
  const f = S.filters;
  const q = f.q.toLowerCase();
  let list = all().filter(a => a.status === 'live' && inMode(a)
    && (!q || (a.title + ' ' + a.cat + ' ' + a.seller.name).toLowerCase().includes(q))
    && (!f.cat || a.cat === f.cat)
    && (!f.formats.size || f.formats.has(a.format))
    && (!f.modes.size || f.modes.has(a.mode))
    && (!f.sellers.size || f.sellers.has(a.seller.type))
    && (f.ending === 'any' || a.endAt - Date.now() < (f.ending === '1h' ? 3600e3 : 86400e3))
    && (isLocal(a) || (f.ships && a.delivery !== 'pickup'))
    && (!f.delivery.size || (f.delivery.has('pickup') && a.delivery !== 'ship' && isLocal(a)) || (f.delivery.has('ship') && a.delivery !== 'pickup')));
  const sorters = {
    nearest: (x, y) => dist(x) - dist(y),
    ending: (x, y) => x.endAt - y.endAt,
    low: (x, y) => curPrice(x) - curPrice(y),
    high: (x, y) => curPrice(y) - curPrice(x),
    bids: (x, y) => (y.bids || 0) - (x.bids || 0),
  };
  list.sort(sorters[f.sort]);
  S.lastResults = list;
  const localCount = list.filter(isLocal).length;
  const summary = `<p class="muted small">${localCount} within ${radiusLabel()} of ${S.loc.name}${list.length > localCount ? ` · ${list.length - localCount} more ship from farther away` : ''}${S.mode !== 'all' ? ` · ${S.mode} mode` : ''}</p>`;
  if (S.searchView === 'map') {
    return `${summary}<div class="map" data-map="search"></div>
      <p class="small muted">Circles show an approximate area (~1 km). Exact addresses are shared only with the winning buyer.</p>
      ${cards(list.filter(isLocal))}`;
  }
  return summary + cards(list);
}

// ---------- maps (Leaflet + OpenStreetMap tiles; falls back to a note offline) ----------
function priceText(a) {
  if (a.format === 'sealed') return 'Sealed bid';
  return money(a.format === 'dutch' ? dutchPrice(a) : a.price);
}
// Tear down Leaflet maps before their container is replaced, so pending animations don't fire on a dead map.
function unmountMaps(root = document) {
  $$('[data-map]', root).forEach(el => { if (el._map) { el._map.remove(); el._map = null; } });
}

function mountMaps() {
  const accent = getComputedStyle(document.documentElement).getPropertyValue('--accent').trim() || '#4338ca';
  $$('[data-map]').forEach(el => {
    if (el._map) return;
    if (!window.L) { el.innerHTML = '<div class="empty">Map unavailable (the map library loads from the internet).</div>'; return; }
    const [kind, id] = el.dataset.map.split(':');
    const you = [S.loc.lat, S.loc.lng];
    // Leaflet needs a view before vector layers are added.
    const map = L.map(el, { scrollWheelZoom: false }).setView(you, 11);
    el._map = map;
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18, attribution: '© OpenStreetMap contributors' }).addTo(map);
    L.circleMarker(you, { radius: 7, color: '#fff', weight: 2, fillColor: '#2563eb', fillOpacity: 1 }).addTo(map).bindTooltip('You are here');
    if (kind === 'item') {
      const a = S.auctions[id];
      const p = [fuzz(a).lat, fuzz(a).lng];
      L.circle(p, { radius: 900, color: accent, fillOpacity: 0.2 }).addTo(map).bindTooltip(`Approximate area: ${a.area}`);
      if (dist(a) < 150) map.fitBounds(L.latLngBounds([p, you]).pad(0.25), { maxZoom: 13 });
      else map.setView(p, 12);
      return;
    }
    const pts = [you];
    for (const a of S.lastResults.filter(isLocal)) {
      const p = [fuzz(a).lat, fuzz(a).lng];
      pts.push(p);
      L.circleMarker(p, { radius: 11, color: accent, weight: 2, fillColor: accent, fillOpacity: 0.3 }).addTo(map)
        .bindTooltip(`${a.emoji} ${priceText(a)}`, { permanent: true, direction: 'top', className: 'price-tip' })
        .bindPopup(`<b>${a.title}</b><br>${priceText(a)} · ${fmtDist(dist(a))} · ${a.area}<br><a href="#/item/${a.id}">View listing →</a>`);
    }
    if (Number.isFinite(S.loc.radius)) {
      const ring = L.circle(you, { radius: S.loc.radius * 1000, color: '#2563eb', weight: 1, fill: false, dashArray: '4 6' }).addTo(map);
      map.fitBounds(ring.getBounds());
    } else {
      map.fitBounds(L.latLngBounds(pts).pad(0.2));
    }
  });
}

function statusHTML(a) {
  if (a.status !== 'live') {
    if (a.format === 'english') {
      if (!a.sold) return `<div class="notice warn">Auction ended. ${a.leader ? 'Reserve not met.' : 'No bids.'}</div>`;
      return a.leader.name === 'You'
        ? `<div class="notice good">🎉 You won for ${money(a.price)}. <a href="#/order/${S.orders.find(o => o.itemId === a.id)?.id}">Review the deal agreement →</a></div>`
        : `<div class="notice">Sold for ${money(a.price)}.</div>`;
    }
    return `<div class="notice">This listing has ended.</div>`;
  }
  if (a.format === 'english' && a.youBid) {
    return a.leader?.name === 'You'
      ? `<div class="notice good">✓ You’re the highest bidder. Your max: <b>${money(a.leader.max)}</b> (private)</div>`
      : `<div class="notice bad">✕ You’ve been outbid. Bid again to stay in.</div>`;
  }
  if (a.format === 'sealed' && a.yourSealed) return `<div class="notice info">Your sealed bid: <b>${money(a.yourSealed)}</b>. You can change it until close.</div>`;
  return '';
}

function reserveHTML(a) {
  if (!a.reserve) return 'No reserve';
  return a.price >= a.reserve ? '<span class="pill good">Reserve met</span>' : '<span class="pill warn">Reserve not met</span>';
}

function historyRows(a) {
  if (!a.history.length) return `<tr><td colspan="3" class="muted">No bids yet. Be the first.</td></tr>`;
  return a.history.slice(0, 12).map(h => `<tr class="${h.name === 'You' ? 'you' : ''}">
    <td>${h.name === 'You' ? '<b>You</b>' : h.name + ` <span class="muted">(${rand(5, 400)})</span>`}${h.auto ? ' <span class="badge">auto</span>' : ''}</td>
    <td class="num">${money(h.amount)}</td>
    <td class="num muted">${new Date(h.t).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}</td></tr>`).join('');
}

function b2bGate(a) {
  if (a.mode !== 'b2b' || S.account === 'org') return '';
  return `<div class="notice b2b">🏢 <b>Business buyers only.</b> Switch to a verified business account to bid.
    <div style="margin-top:8px"><button class="btn sm" data-action="use-org">Switch to Acme Supply Co.</button></div></div>`;
}

function b2bTerms(a) {
  if (a.mode !== 'b2b') return '';
  return `<div class="lines small">
    ${a.premium ? `<span>Buyer’s premium</span><span>${a.premium}%</span>` : ''}
    ${a.deposit ? `<span>Bidder deposit</span><span>${money(a.deposit)} ${S.account === 'org' && a.id !== 'a5' ? '<span class="pill good">held</span>' : '<span class="pill warn">required</span>'}</span>` : ''}
    ${a.manifest ? `<span>Manifest</span><span><a href="#" data-action="manifest">Download CSV</a></span>` : ''}
    ${S.account === 'org' ? `<span>Your bid limit</span><span>${money(ORG.you.perBidLimit)} (approval above)</span>` : ''}
  </div>`;
}

function buyBox(a) {
  const head = `<div class="badges">${modeBadge(a)}<span class="badge">${FORMAT_LABEL[a.format]}${a.format === 'sealed' ? (a.sealedType === 'second' ? ' · 2nd price' : ' · 1st price') : ''}</span>${sellerBadge(a.seller)}</div>
    <h1>${a.title}</h1>
    <div class="muted small">${a.cond} · 📍 ${a.area} (${fmtDist(dist(a))}) · ${a.watchers} watching</div>
    ${a.delivery === 'pickup' && !isLocal(a) ? `<div class="notice warn small" style="margin-top:8px">Pickup only, ${fmtDist(dist(a))} from you. Make sure you can collect it before bidding.</div>` : ''}`;
  const watch = `<button class="btn block" data-action="watch" data-id="${a.id}">${S.watch.has(a.id) ? '♥ Watching' : '♡ Add to watchlist'}</button>`;
  const gate = b2bGate(a);
  const live = a.status === 'live';

  if (a.format === 'english') {
    const min = minNext(a);
    return `${head}
      <div class="price-block">
        <div class="label">${a.bids ? 'Current bid' : 'Starting bid'}</div>
        <div class="big tnum" data-price="${a.id}">${money(a.price)}</div>
        <div class="small"><a href="#history" data-bids="${a.id}">${a.bids} bids</a> · <span data-reserve="${a.id}">${reserveHTML(a)}</span></div>
      </div>
      <div class="timer">⏱ ${live ? 'Ends in' : ''} <strong class="cd tnum" data-cd="${a.id}"></strong>
        <span class="muted small">· soft close ${a.softClose / 60} min${a.extensions ? ` · extended ${a.extensions}×` : ''}</span></div>
      <div class="status" data-status="${a.id}">${statusHTML(a)}</div>
      ${gate || (live ? `<form class="bid-form" data-form="bid" data-id="${a.id}">
        <label for="max">Your maximum bid</label>
        <div class="bid-row">
          <div class="money-input"><span>$</span><input id="max" name="max" type="number" inputmode="decimal" step="0.01" min="${min}" placeholder="${min} or more" required></div>
          <button class="btn primary">Place bid</button>
        </div>
        <div class="quick">${[1, 3, 10].map(k => { const v = r2(min + increment(min) * (k - 1)); return `<button type="button" class="chip" data-action="quick" data-v="${v}">${money(v)}</button>`; }).join('')}</div>
        <div class="small muted" data-min="${a.id}">Enter ${money(min)} or more. We bid for you, one increment at a time, up to your max. Your max stays private.</div>
      </form>` : '')}
      ${b2bTerms(a)}
      <div class="stack">
        ${live && a.buyNow && !a.leader && !gate ? `<button class="btn good block" data-action="buy-now" data-id="${a.id}" data-price="${a.buyNow}">Buy it now: ${money(a.buyNow)}</button>` : ''}
        ${watch}
      </div>`;
  }

  if (a.format === 'sealed') {
    return `${head}
      <div class="price-block">
        <div class="label">Minimum bid</div>
        <div class="big tnum">${money(a.start)}</div>
        <div class="small">${a.sealedCount} sealed bids received · bids revealed at close</div>
      </div>
      <div class="timer">⏱ Closes in <strong class="cd tnum" data-cd="${a.id}"></strong></div>
      <div class="status" data-status="${a.id}">${statusHTML(a)}</div>
      <div class="notice info small">${a.sealedType === 'second'
        ? '<b>Second-price (Vickrey):</b> the highest bidder wins but pays the second-highest bid plus one increment. Your best strategy is to bid what it’s truly worth to you.'
        : '<b>First-price:</b> the highest bidder wins and pays their own bid.'}</div>
      ${gate || (live ? `<form class="bid-form" data-form="sealed" data-id="${a.id}" style="margin-top:12px">
        <label for="sb">Your sealed bid</label>
        <div class="bid-row">
          <div class="money-input"><span>$</span><input id="sb" name="amount" type="number" step="1" min="${a.start}" value="${a.yourSealed || ''}" placeholder="${a.start} or more" required></div>
          <button class="btn primary">${a.yourSealed ? 'Update bid' : 'Submit bid'}</button>
        </div>
      </form>` : '')}
      ${b2bTerms(a)}
      <div class="stack">${watch}<button class="btn block" data-action="inspect">📅 Book inspection slot</button></div>`;
  }

  if (a.format === 'dutch') {
    return `${head}
      <div class="price-block">
        <div class="label">Current price (falls ${money(a.dutch.drop)} every ${a.dutch.every}s)</div>
        <div class="big tnum" data-dutch="${a.id}">${money(dutchPrice(a))}</div>
        <div class="small">Next drop in <span data-dutch-next="${a.id}">–</span>s · the first buyer takes the whole lot</div>
      </div>
      <div class="timer">⏱ Clock stops in <strong class="cd tnum" data-cd="${a.id}"></strong></div>
      <div class="status" data-status="${a.id}">${statusHTML(a)}</div>
      ${gate || (live ? `<button class="btn primary block" data-action="dutch-buy" data-id="${a.id}">Buy now at current price</button>` : '')}
      ${b2bTerms(a)}
      <div class="stack">${watch}</div>`;
  }

  // fixed price
  return `${head}
    <div class="price-block">
      <div class="label">Price</div>
      <div class="big tnum">${money(a.price)}</div>
      <div class="small">${a.ship.cost ? '+ ' + money(a.ship.cost) + ' shipping' : 'Free shipping'}</div>
    </div>
    <div class="status" data-status="${a.id}">${statusHTML(a)}</div>
    ${live ? `<div class="stack">
      <button class="btn primary block" data-action="buy-now" data-id="${a.id}" data-price="${a.price}">Buy it now</button>
      ${a.offers ? `<button class="btn block" data-action="offer" data-id="${a.id}">Make an offer</button>` : ''}
      ${watch}
    </div>` : ''}`;
}

function pageItem(id) {
  const a = S.auctions[id];
  if (!a) return `<div class="empty">Listing not found. <a href="#/">Go home</a></div>`;
  S.viewing = id;
  const s = a.seller;
  return `
  <nav class="crumbs"><a href="#/">Home</a> › <a href="#/search?cat=${encodeURIComponent(a.cat)}">${a.cat}</a> › ${a.title.slice(0, 40)}…</nav>
  <div class="item-layout">
    <div>
      <section class="gallery">
        ${tile(a, 'main')}
        <div class="thumbs">${[0, 40, 80, 140].map((d, i) => `<div class="tile ${i ? '' : 'on'}" style="--h:${a.hue + d}" data-action="thumb" data-h="${a.hue + d}">${a.emoji}</div>`).join('')}</div>
      </section>
      <section class="panel" style="margin-top:16px">
        <h2>Description</h2>
        <p>${a.desc}</p>
        <h3>Item specifics</h3>
        <dl class="specs">${Object.entries(a.specs).map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join('')}<dt>Condition</dt><dd>${a.cond}</dd></dl>
      </section>
      <section class="panel">
        <h2>Location & delivery</h2>
        <div class="map small-map" data-map="item:${a.id}"></div>
        <dl class="specs">
          <dt>Item location</dt><dd>📍 ${a.area} · about ${fmtDist(dist(a))} from ${S.loc.name}</dd>
          <dt>Delivery</dt><dd>${DELIVERY_LABEL[a.delivery]}${a.delivery === 'pickup' ? (a.ship.method === 'Pickup only' ? '' : ` · ${a.ship.method}`) : ` · ${a.ship.method} · ${a.ship.cost ? money(a.ship.cost) : 'Free'}`}</dd>
          <dt>Returns</dt><dd>${a.returns}</dd>
          <dt>Payment</dt><dd>After you win, Bidly creates a deal agreement with the final price. You and the seller accept it within ${a.mode === 'b2b' ? '3 days' : '48 hours'}, settle payment directly, and both confirm the deal is paid.</dd>
        </dl>
      </section>
      ${a.format === 'english' ? `<section class="panel" id="history">
        <h2>Bid history</h2>
        <p class="small muted">Bidder names are masked. “auto” is a proxy bid placed on a bidder’s behalf.</p>
        <div class="table-wrap"><table><thead><tr><th>Bidder</th><th class="num">Bid</th><th class="num">Time</th></tr></thead>
        <tbody data-history="${a.id}">${historyRows(a)}</tbody></table></div>
      </section>` : ''}
      <section class="panel">
        <h2>Questions & answers</h2>
        <p class="muted small">Ask the seller. Contact details are hidden until after the sale.</p>
        <form class="bid-row" data-form="question"><input name="q" placeholder="Ask a question…" style="flex:1" aria-label="Question"><button class="btn">Ask</button></form>
      </section>
    </div>
    <aside class="buybox">
      <div class="panel">${buyBox(a)}</div>
      <div class="panel">
        <div class="seller-card">
          <div class="avatar">${s.name[0].toUpperCase()}</div>
          <div><strong>${s.name}</strong> ${sellerBadge(s)}<br>
          <span class="small muted">${s.rating}% positive · ${s.reviews.toLocaleString()} reviews · since ${s.since}</span></div>
        </div>
        <div class="stack" style="grid-template-columns:1fr 1fr"><button class="btn sm" data-action="msg">Message</button><button class="btn sm" data-action="follow">Follow</button></div>
      </div>
    </aside>
  </div>`;
}

function pageOrder(id) {
  const o = S.orders.find(x => x.id === id);
  if (!o) return `<div class="empty">Order not found.</div>`;
  const total = r2(o.amount + o.premium + o.ship);
  const when = t => t ? new Date(t).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }) : '';
  const t = o.terms;
  const pickup = t.handover === 'Local pickup';
  const stamps = [when(o.createdAt), when(o.sellerAcceptedAt), o.sellerPaidAt ? `${when(o.sellerPaidAt)} · ${t.payment}` : '', when(o.receivedAt)];
  const sig = (who, at, verb) => at
    ? `<span class="pill good">✓ ${who} ${verb} · ${when(at)}</span>`
    : `<span class="pill warn">${who}: waiting</span>`;

  const agreement = `<div class="agreement">
    <div class="agreement-head"><div><div class="small muted">DEAL AGREEMENT</div><b>${o.agreementNo}</b></div><button class="btn sm" data-action="invoice">⬇ PDF</button></div>
    <dl class="specs">
      <dt>Item</dt><dd>${o.title}</dd>
      <dt>Seller</dt><dd>${o.seller}</dd>
      <dt>Buyer</dt><dd>${S.account === 'org' ? `${ORG.name} (${ORG.you.name})` : 'Alex Kim'}</dd>
      <dt>Agreed price</dt><dd><b class="tnum">${money(total)}</b><br><span class="muted small">Winning bid ${money(o.amount)}${o.premium ? ` + buyer’s premium ${money(o.premium)}` : ''}${!pickup && o.ship ? ` + shipping ${money(o.ship)}` : ''}</span></dd>
      <dt>Handover</dt><dd>${t.handover}${pickup ? (o.stage >= 1 ? ` · 2140 W Example St, ${o.area}` : ` · ${o.area}<br><span class="muted small">Exact address shared once both sides accept.</span>`) : ''}</dd>
      <dt>Handover date</dt><dd>${t.date}</dd>
      <dt>Payment</dt><dd>${t.payment}<br><span class="muted small">Paid directly between buyer and seller. Bidly doesn’t handle the money.</span></dd>
      ${t.note ? `<dt>Notes</dt><dd>${t.note}</dd>` : ''}
    </dl>
    <div class="sigs"><span class="small muted">Accepted:</span> ${sig('You', o.buyerAcceptedAt, 'accepted')} ${sig('Seller', o.sellerAcceptedAt, 'accepted')}</div>
    ${o.stage >= 1 ? `<div class="sigs"><span class="small muted">Paid:</span> ${sig('You', o.buyerPaidAt, 'confirmed paid')} ${sig('Seller', o.sellerPaidAt, 'confirmed received')}</div>` : ''}
  </div>`;

  let next;
  if (o.stage === 0 && !o.buyerAcceptedAt) {
    const handovers = o.delivery === 'pickup' ? ['Local pickup'] : o.delivery === 'ship' ? ['Shipping'] : ['Local pickup', 'Shipping'];
    const opt = (list, cur) => list.map(v => `<option ${v === cur ? 'selected' : ''}>${v}</option>`).join('');
    next = `<h2>Review the deal</h2>
      <p class="small muted">The price comes from your winning bid. Agree the handover and how you’ll pay, then accept by ${when(o.acceptBy)}.</p>
      <form data-form="agreement" data-id="${o.id}" class="stack">
        <div class="field"><label for="ag-h">Handover</label><select id="ag-h" name="handover">${opt(handovers, t.handover)}</select></div>
        <div class="field"><label for="ag-d">Handover date</label><input id="ag-d" name="date" type="date" value="${t.date}" min="${isoDate(Date.now())}" required></div>
        <div class="field"><label for="ag-p">Payment method</label><select id="ag-p" name="payment">${opt(['Cash on handover', 'Bank transfer', 'Mobile payment / QR', 'Other'], t.payment)}</select></div>
        <div class="field"><label for="ag-n">Notes for the seller (optional)</label><textarea id="ag-n" name="note" placeholder="e.g. I can come after 6 pm">${t.note}</textarea></div>
        <label class="small" style="display:flex;gap:8px;align-items:flex-start"><input type="checkbox" required style="margin-top:3px"> I agree to buy this item for <b>${money(total)}</b> on these terms.</label>
        <button class="btn primary block">Accept agreement</button>
      </form>`;
  } else if (o.stage === 0) {
    next = `<h2>Waiting for the seller</h2>
      <p class="small muted">You accepted the agreement. ${o.seller} has been asked to accept it too.</p>
      <div class="stack"><button class="btn block" data-action="msg">Message seller</button></div>`;
  } else if (o.stage === 1) {
    next = `<h2>Deal agreed: complete the payment</h2>
      <p class="small muted">Pay ${o.seller} <b>${money(total)}</b> as agreed (<b>${t.payment}</b>${pickup ? `, pickup on ${t.date}` : ''}). Then both of you confirm the deal is paid.</p>
      <div class="stack">
        ${pickup ? `<button class="btn block" data-action="pickup-time">🗓 Arrange pickup time</button>` : ''}
        ${o.buyerPaidAt
          ? `<div class="notice info small">You confirmed the deal is paid. Waiting for ${o.seller} to confirm they received it.</div>`
          : `<button class="btn primary block" data-action="confirm-paid" data-id="${o.id}">Confirm deal paid</button>`}
        <button class="btn block" data-action="msg">Message seller</button>
      </div>`;
  } else if (o.stage === 2) {
    next = `<h2>Deal paid</h2>
      <p class="small muted">Both sides confirmed payment. Mark the order as received once you have the item.</p>
      <div class="stack"><button class="btn good block" data-action="mark-received" data-id="${o.id}">Mark as received</button></div>`;
  } else {
    next = `<h2>Order complete</h2>
      <p class="small muted">You received this item on ${when(o.receivedAt)}.</p>
      <div class="stack"><button class="btn primary block" data-action="review" data-id="${o.id}">★ Leave feedback</button></div>`;
  }

  return `<h1>Order ${o.id} <span class="pill ${orderPillClass(o)}">${ORDER_PILL[o.stage]}</span></h1>
  <div class="item-layout">
    <div class="panel">
      <div class="row" style="padding-top:0">${tile(o)}<div class="grow"><strong>${o.title}</strong><div class="small muted">Seller: ${o.seller}</div></div></div>
      ${agreement}
      <ol class="timeline">
        ${ORDER_STAGES.map(([t, d], i) => `<li class="${i <= o.stage ? 'done' : i === o.stage + 1 ? 'now' : ''}">
          <span class="dot">${i <= o.stage ? '✓' : i + 1}</span>
          <div><strong>${t}</strong><div class="small muted">${i <= o.stage ? stamps[i] : d}</div></div></li>`).join('')}
      </ol>
      ${o.dispute ? `<div class="notice warn">⚖ Problem reported: “${o.dispute}”. Our team will review it and contact the seller.</div>` : ''}
      ${o.stage < 3 && !o.dispute ? `<button class="btn sm" data-action="dispute" data-id="${o.id}">Report a problem</button>` : ''}
    </div>
    <div class="panel">${next}</div>
  </div>`;
}

function pageMe() {
  const tabs = [['bidding', 'Bidding'], ['won', 'Purchases'], ['watching', 'Watching'], ['offers', 'Offers'], ['sealed', 'Sealed bids']];
  let body = '';
  const row = (a, right) => `<div class="row">${tile(a)}<div class="grow"><a href="#/item/${a.id}">${a.title}</a><div class="small muted">${modeBadge(a)} ${FORMAT_LABEL[a.format]} · ${a.format === 'fixed' ? '' : `<span class="cd" data-cd="${a.id}"></span>`}</div></div>${right}</div>`;
  if (S.meTab === 'bidding') {
    const list = all().filter(a => a.youBid && a.format === 'english');
    body = list.length ? list.map(a => row(a, `<div style="text-align:right"><div class="tnum"><b data-price="${a.id}">${money(a.price)}</b></div>
      ${a.status !== 'live' ? '<span class="pill">Ended</span>' : a.leader?.name === 'You' ? '<span class="pill good">Winning</span>' : '<span class="pill bad">Outbid</span>'}</div>`)).join('')
      : `<div class="empty">You haven’t bid on anything yet. <a href="#/item/a1">Try the Sony camera (ends soon)</a>.</div>`;
  } else if (S.meTab === 'won') {
    body = S.orders.map(o => `<div class="row">${tile(o)}<div class="grow"><a href="#/order/${o.id}">${o.title}</a><div class="small muted">Order ${o.id} · ${money(o.amount)}</div></div>
      <span class="pill ${orderPillClass(o)}">${ORDER_PILL[o.stage]}</span></div>`).join('');
  } else if (S.meTab === 'watching') {
    const list = [...S.watch].map(id => S.auctions[id]).filter(Boolean);
    body = list.length ? list.map(a => row(a, `<div class="tnum"><b>${money(curPrice(a))}</b></div>`)).join('') : `<div class="empty">No watched items.</div>`;
  } else if (S.meTab === 'offers') {
    body = S.offers.length ? S.offers.map(o => { const a = S.auctions[o.id]; return row(a, `<div style="text-align:right">${money(o.amount)}<br><span class="pill ${o.status === 'Accepted' ? 'good' : o.status === 'Declined' ? 'bad' : 'warn'}">${o.status}</span></div>`); }).join('')
      : `<div class="empty">No offers yet. <a href="#/item/a8">Make an offer on the Jordans</a>.</div>`;
  } else {
    const list = all().filter(a => a.yourSealed);
    body = list.length ? list.map(a => row(a, `<div>${money(a.yourSealed)}<br><span class="pill info">Hidden</span></div>`)).join('') : `<div class="empty">No sealed bids. <a href="#/item/a5">See the CNC mill</a>.</div>`;
  }
  return `<h1>My auctions</h1>
  <div class="tabs" role="tablist">${tabs.map(([k, l]) => `<button role="tab" aria-selected="${S.meTab === k}" data-action="me-tab" data-tab="${k}">${l}</button>`).join('')}</div>
  <div class="panel">${body}</div>`;
}

function pageBusiness() {
  if (S.account !== 'org') {
    return `<div class="panel gate"><div class="ic">🏢</div><h1>Business buying</h1>
      <p class="muted">Organisation accounts unlock B2B auctions, sealed bids, deposits, invoices, approvals and reverse auctions.</p>
      <button class="btn primary" data-action="use-org">Switch to Acme Supply Co. (demo org)</button></div>`;
  }
  const lots = all().filter(a => a.mode === 'b2b');
  const pending = S.approvals.filter(x => !x.status);
  return `<div class="section-head"><div><h1>${ORG.name} <span class="badge ok">✓ Verified business</span></h1>
    <div class="muted small">${ORG.taxId} · signed in as ${ORG.you.name} (${ORG.you.role})</div></div>
    <a class="btn primary" href="#/rfq/r1">Open reverse auction →</a></div>
  <div class="kpis">
    <div class="panel kpi"><div class="label">Spend this month</div><div class="value tnum">${money(ORG.spent)}</div><div class="bar"><span style="width:${ORG.spent / ORG.budget * 100}%"></span></div><div class="small muted">of ${money(ORG.budget)} budget</div></div>
    <div class="panel kpi"><div class="label">Active bids</div><div class="value">${all().filter(a => a.youBid && a.status === 'live').length + all().filter(a => a.yourSealed).length}</div></div>
    <div class="panel kpi"><div class="label">Deposits held</div><div class="value tnum">${money(ORG.depositsHeld)}</div><div class="small muted">credited to purchases</div></div>
    <div class="panel kpi"><div class="label">Pending approvals</div><div class="value">${pending.length}</div></div>
  </div>

  <div class="grid2 section">
    <div class="panel">
      <h2>Approval queue</h2>
      <p class="small muted">Bids above a member’s limit need approval before they’re placed. Bids are binding.</p>
      ${S.approvals.length ? S.approvals.map(ap => { const a = S.auctions[ap.itemId]; return `<div class="row">
        ${tile(a)}<div class="grow"><b>${ap.who}</b> wants to bid <b>${money(ap.amount)}</b><div class="small"><a href="#/item/${a.id}">${a.title}</a></div><div class="small muted">“${ap.note}”</div></div>
        ${ap.status ? `<span class="pill ${ap.status === 'Approved' ? 'good' : 'bad'}">${ap.status}</span>` : `<div class="stack" style="margin:0"><button class="btn sm good" data-action="approve" data-id="${ap.id}">Approve</button><button class="btn sm" data-action="reject" data-id="${ap.id}">Reject</button></div>`}
      </div>`; }).join('') : '<div class="empty">Nothing to approve.</div>'}
    </div>
    <div class="panel">
      <h2>Members & limits</h2>
      <div class="table-wrap"><table><thead><tr><th>Name</th><th>Role</th><th>Limit</th></tr></thead>
      <tbody>${ORG.members.map(m => `<tr class="${m.name === ORG.you.name ? 'you' : ''}"><td>${m.name}</td><td>${m.role}</td><td>${m.limit}</td></tr>`).join('')}</tbody></table></div>
      <button class="btn sm" style="margin-top:10px" data-action="invite">+ Invite member</button>
    </div>
  </div>

  <section class="section">
    <div class="section-head"><h2>Upcoming catalogue: ClearStock Q4 returns event</h2><span class="muted small">Lots close one after another, 30 s apart</span></div>
    ${cards(lots)}
  </section>`;
}

function pageRFQ() {
  S.viewing = 'rfq';
  const r = S.rfq;
  const ranked = rfqRanked();
  const myRank = ranked.findIndex(b => b.name === r.you) + 1;
  const mine = r.bids.find(b => b.name === r.you);
  const live = Date.now() < r.endAt && !r.awarded;
  const viewTabs = `<div class="tabs" role="tablist">
    <button role="tab" aria-selected="${S.rfqView === 'supplier'}" data-action="rfq-view" data-v="supplier">View as supplier (Siam Packaging)</button>
    <button role="tab" aria-selected="${S.rfqView === 'buyer'}" data-action="rfq-view" data-v="buyer">View as buyer (Acme Supply Co.)</button></div>`;
  const header = `<div class="badges"><span class="badge b2b">B2B</span><span class="badge">Reverse auction</span><span class="badge">RFQ ${r.id.toUpperCase()}</span></div>
    <h1>${r.title}</h1><p class="muted">${r.terms}</p>
    <div class="timer">⏱ ${live ? 'Closes in' : ''} <strong class="cd tnum" data-cd-rfq>${countdown(r.endAt - Date.now())}</strong> <span class="muted small">· soft close 2 min · min decrement ${money(r.decrement)}</span></div>`;

  if (S.rfqView === 'supplier') {
    return `${viewTabs}<div class="item-layout"><div class="panel">${header}
      <div class="kpis" style="margin-top:16px">
        <div class="panel kpi"><div class="label">Your rank</div><div class="value">#${myRank} <span class="muted small">of ${ranked.length}</span></div></div>
        <div class="panel kpi"><div class="label">Your bid</div><div class="value tnum">${money(mine.price)}</div><div class="small muted">${r.unit}</div></div>
        <div class="panel kpi"><div class="label">Best bid</div><div class="value tnum">${money(ranked[0].price)}</div></div>
        <div class="panel kpi"><div class="label">Your contract value</div><div class="value tnum">${money(mine.price * r.qty)}</div></div>
      </div>
      <p class="small muted" style="margin-top:12px">Suppliers see their rank and the best price, not competitor names. Award is by weighted score (price ${r.weights.price}%, delivery ${r.weights.delivery}%, quality ${r.weights.quality}%), so the lowest price doesn’t automatically win.</p>
    </div>
    <aside class="buybox"><div class="panel">
      <h2>Lower your bid</h2>
      ${r.awarded ? `<div class="notice ${r.awarded === r.you ? 'good' : 'warn'}">Event closed. Awarded to ${r.awarded}.</div>` : live ? `<form class="bid-form" data-form="rfq">
        <label for="rp">New price ${r.unit}</label>
        <div class="bid-row"><div class="money-input"><span>$</span><input id="rp" name="price" type="number" step="0.01" max="${r2(mine.price - r.decrement)}" placeholder="${r2(mine.price - r.decrement)} or less" required></div>
        <button class="btn primary">Submit</button></div>
        <div class="small muted" style="margin-top:6px">Must be at least ${money(r.decrement)} below your current bid. Ceiling ${money(r.ceiling)}.</div>
      </form>` : '<div class="notice">Bidding closed. Waiting for the buyer to award.</div>'}
    </div></aside></div>`;
  }

  const byScore = [...r.bids].sort((x, y) => rfqScore(y) - rfqScore(x));
  return `${viewTabs}<div class="panel">${header}
    <div class="table-wrap" style="margin-top:16px"><table>
      <thead><tr><th>#</th><th>Supplier</th><th class="num">Unit price</th><th class="num">Total</th><th class="num">Delivery</th><th class="num">Quality</th><th class="num">Score</th><th></th></tr></thead>
      <tbody>${byScore.map((b, i) => `<tr><td>${i + 1}</td><td>${b.name}</td><td class="num">${money(b.price)}</td><td class="num">${money(b.price * r.qty)}</td>
        <td class="num">${b.delivery}</td><td class="num">${b.quality}</td><td class="num"><b>${rfqScore(b)}</b></td>
        <td>${r.awarded ? (r.awarded === b.name ? '<span class="pill good">Awarded</span>' : '') : `<button class="btn sm" data-action="award" data-name="${b.name}">Award</button>`}</td></tr>`).join('')}</tbody>
    </table></div>
    <p class="small muted" style="margin-top:8px">Score = price ${r.weights.price}% (relative to best) + delivery ${r.weights.delivery}% + quality ${r.weights.quality}%. Awarding creates a draft PO.</p>
  </div>`;
}

const SELL_STEPS = ['Seller', 'Item', 'Format & price', 'Review'];
function newDraft() {
  return { step: 0, as: 'individual', mode: 'c2c', title: '', cat: 'Electronics', cond: 'Used, like new', emoji: '📦', format: 'english', start: 50, reserve: '', buyNow: '', duration: 3, sealedType: 'second', dutchStart: 1000, dutchFloor: 500, price: 100, premium: 0, deposit: 0, area: S.loc.name, delivery: 'both' };
}

function pageSell() {
  const d = S.draft ||= newDraft();
  const f = (key, label, type = 'text', hint = '', extra = '') => `<div class="field"><label for="f-${key}">${label}</label>
    <input id="f-${key}" type="${type}" data-bind="${key}" value="${d[key] ?? ''}" ${extra}>${hint ? `<div class="hint">${hint}</div>` : ''}</div>`;
  const choice = (key, val, title, desc, disabled = false) => `<button type="button" class="choice" data-action="choose" data-key="${key}" data-val="${val}" aria-pressed="${d[key] === val}" ${disabled ? 'disabled' : ''}><strong>${title}</strong><span class="small muted">${desc}</span></button>`;
  let body = '';

  if (d.step === 0) {
    body = `<h2>Who is selling?</h2>
      <div class="choice-grid">
        ${choice('as', 'individual', '👤 Individual', 'Sell personal items (C2C). Phone and ID verified before your first sale.')}
        ${choice('as', 'business', '🏬 Business', 'Verified store. Tax invoices, storefront, bulk tools.')}
      </div>
      ${d.as === 'business' ? `<h2 style="margin-top:20px">Who can buy?</h2><div class="choice-grid">
        ${choice('mode', 'b2c', 'Consumers (B2C)', 'Anyone can bid. Consumer return rules apply.')}
        ${choice('mode', 'b2b', 'Businesses only (B2B)', 'Verified businesses. Deposits, premium, invoices.')}
      </div>` : ''}`;
  } else if (d.step === 1) {
    body = `<h2>Tell buyers about it</h2>
      <div class="dropzone">📸 Drag photos here or <a href="#" data-action="noop">browse</a><br><span class="small">Up to 24 photos. We’ll suggest a title and category from them.</span></div>
      <div class="form-grid" style="margin-top:16px">
        ${f('title', 'Title', 'text', 'Brand, model, key details', 'placeholder="e.g. Canon EOS R6 body, low shutter count"')}
        <div class="field"><label for="f-cat">Category</label><select id="f-cat" data-bind="cat">${CATEGORIES.map(c => `<option ${d.cat === c.name ? 'selected' : ''}>${c.name}</option>`).join('')}</select></div>
        <div class="field"><label for="f-cond">Condition</label><select id="f-cond" data-bind="cond">${['New', 'Used, like new', 'Used, good', 'Refurbished', 'For parts', 'Customer returns (untested)'].map(c => `<option ${d.cond === c ? 'selected' : ''}>${c}</option>`).join('')}</select></div>
      </div>
      <div class="notice info small" style="margin-top:12px">💡 Price guide: similar items sold for <b>$180–$240</b> in the last 90 days.</div>
      <h2 style="margin-top:20px">Where is it, and how does it get to the buyer?</h2>
      <div class="form-grid">${f('area', 'Item location (area or postcode)', 'text', '🔒 Buyers see only the area and an approximate distance. The exact address goes to the winner.')}</div>
      <div class="choice-grid" style="margin-top:12px">
        ${choice('delivery', 'pickup', '📍 Pickup only', 'Local buyers collect it. Best for big or heavy items.')}
        ${choice('delivery', 'both', '📍📦 Pickup or shipping', 'Local buyers can collect, others get it shipped.')}
        ${choice('delivery', 'ship', '📦 Shipping only', 'Shown to buyers everywhere.')}
      </div>`;
  } else if (d.step === 2) {
    const b2b = d.mode === 'b2b';
    body = `<h2>How do you want to sell?</h2>
      <div class="choice-grid">
        ${choice('format', 'english', '📈 Auction', 'Open bids go up. Proxy bidding and soft close.')}
        ${choice('format', 'fixed', '🏷️ Buy now', 'Fixed price, with optional Best Offer.')}
        ${choice('format', 'sealed', '✉️ Sealed bid', 'Hidden bids, revealed at close. B2B only.', !b2b)}
        ${choice('format', 'dutch', '📉 Dutch clock', 'Price falls until someone buys. B2B only.', !b2b)}
      </div>
      <div class="form-grid" style="margin-top:16px">
        ${d.format === 'english' ? f('start', 'Starting bid ($)', 'number') + f('reserve', 'Reserve price ($)', 'number', 'Optional and hidden. We show only “reserve met / not met”.') + f('buyNow', 'Buy it now ($)', 'number', 'Optional. Disappears after the first bid.') : ''}
        ${d.format === 'fixed' ? f('price', 'Price ($)', 'number') : ''}
        ${d.format === 'sealed' ? f('start', 'Minimum bid ($)', 'number') + `<div class="field"><label for="f-st">Price rule</label><select id="f-st" data-bind="sealedType"><option value="second" ${d.sealedType === 'second' ? 'selected' : ''}>Second-price (Vickrey)</option><option value="first" ${d.sealedType === 'first' ? 'selected' : ''}>First-price</option></select></div>` : ''}
        ${d.format === 'dutch' ? f('dutchStart', 'Start price ($)', 'number') + f('dutchFloor', 'Floor price ($)', 'number', 'Hidden. The clock stops here.') : ''}
        ${d.format !== 'fixed' ? `<div class="field"><label for="f-dur">Duration</label><select id="f-dur" data-bind="duration">${[1, 3, 5, 7, 10].map(n => `<option value="${n}" ${+d.duration === n ? 'selected' : ''}>${n} day${n > 1 ? 's' : ''}</option>`).join('')}</select></div>` : ''}
        ${b2b ? f('premium', "Buyer's premium (%)", 'number') + f('deposit', 'Bidder deposit ($)', 'number') : ''}
      </div>`;
  } else {
    const est = +(d.format === 'fixed' ? d.price : d.format === 'dutch' ? d.dutchStart * 0.7 : (d.buyNow || d.reserve || d.start * 2)) || 0;
    const fee = r2(est * FEES[d.mode]);
    body = `<h2>Review & publish</h2>
      <div class="row" style="padding-top:0"><div class="tile" style="--h:250">${d.emoji}</div><div class="grow"><b>${d.title || 'Untitled listing'}</b><div class="small muted">${d.cat} · ${d.cond} · ${MODE_LABEL[d.mode]} · ${FORMAT_LABEL[d.format]}</div><div class="small muted">📍 ${d.area || S.loc.name} · ${DELIVERY_LABEL[d.delivery]}</div></div></div>
      <h3 style="margin-top:16px">Fee estimate (draft fee schedule)</h3>
      <div class="lines">
        <span>If it sells for</span><span class="tnum">${money(est)}</span>
        <span>Final value fee (${FEES[d.mode] * 100}%)</span><span class="tnum">−${money(fee)}</span>
        <span>Insertion fee</span><span>Free (first 50/month)</span>
        <span class="total">You receive</span><span class="total tnum">${money(est - fee)}</span>
      </div>
      <p class="small muted">When it sells, you and the buyer accept a deal agreement, settle payment directly, and both confirm it’s paid. Bidly bills its fee monthly.</p>`;
  }

  return `<h1>Create a listing</h1>
  <div class="steps">${SELL_STEPS.map((s, i) => `<span class="${i === d.step ? 'on' : i < d.step ? 'done' : ''}">${i + 1}. ${s}</span>`).join('')}</div>
  <div class="panel">${body}
    <div class="wizard-nav">
      <button class="btn" data-action="sell-back" ${d.step === 0 ? 'disabled' : ''}>← Back</button>
      ${d.step < 3 ? `<button class="btn primary" data-action="sell-next">Continue →</button>` : `<button class="btn primary" data-action="publish">Publish listing</button>`}
    </div>
  </div>`;
}

// ---------- router ----------
function parseHash() {
  const raw = location.hash.slice(1) || '/';
  const [path, qs] = raw.split('?');
  const params = Object.fromEntries(new URLSearchParams(qs || ''));
  return { parts: path.split('/').filter(Boolean), params };
}

function render() {
  const { parts, params } = parseHash();
  S.viewing = null;
  const [p, id] = parts;
  let html;
  if (!p) html = pageHome();
  else if (p === 'search') html = pageSearch(params);
  else if (p === 'item') html = pageItem(id);
  else if (p === 'order') html = pageOrder(id);
  else if (p === 'me') html = pageMe();
  else if (p === 'sell') html = pageSell();
  else if (p === 'business') html = pageBusiness();
  else if (p === 'rfq') html = pageRFQ();
  else html = `<div class="empty">Page not found. <a href="#/">Go home</a></div>`;
  unmountMaps($('#app'));
  $('#app').innerHTML = html;
  $$('.modebar button').forEach(b => b.setAttribute('aria-selected', b.dataset.mode === S.mode));
  $('[data-change="account"]').value = S.account;
  updateLocChip();
  mountMaps();
  tick();
}

function refreshResults() {
  unmountMaps($('#results'));
  $('#results').innerHTML = searchResults();
  mountMaps();
  tick();
}

window.addEventListener('hashchange', () => { render(); window.scrollTo(0, 0); });

// ---------- live updates ----------
function updateAuctionUI(a) {
  $$(`[data-price="${a.id}"]`).forEach(el => {
    const t = money(a.price);
    if (el.textContent !== t) { el.textContent = t; el.classList.remove('price-flash'); void el.offsetWidth; el.classList.add('price-flash'); }
  });
  $$(`[data-bids="${a.id}"]`).forEach(el => el.textContent = `${a.bids} bid${a.bids === 1 ? '' : 's'}`);
  $$(`[data-reserve="${a.id}"]`).forEach(el => el.innerHTML = reserveHTML(a));
  $$(`[data-status="${a.id}"]`).forEach(el => el.innerHTML = statusHTML(a));
  $$(`[data-history="${a.id}"]`).forEach(el => el.innerHTML = historyRows(a));
  const min = minNext(a);
  $$(`[data-min="${a.id}"]`).forEach(el => el.textContent = `Enter ${money(min)} or more. We bid for you, one increment at a time, up to your max. Your max stays private.`);
  const input = $(`form[data-id="${a.id}"] input[name="max"]`);
  if (input) { input.min = min; input.placeholder = `${min} or more`; }
}

function tick() {
  const now = Date.now();
  for (const a of all()) {
    if (a.status === 'live' && a.format !== 'fixed' && a.endAt <= now) closeAuction(a);
  }
  $$('[data-cd]').forEach(el => {
    const a = S.auctions[el.dataset.cd];
    const left = a.endAt - now;
    el.textContent = a.status === 'live' ? countdown(left) : 'Ended';
    el.classList.toggle('urgent', a.status === 'live' && left < 5 * 60e3);
  });
  $$('[data-dutch]').forEach(el => { el.textContent = money(dutchPrice(S.auctions[el.dataset.dutch])); });
  $$('[data-dutch-next]').forEach(el => {
    const a = S.auctions[el.dataset.dutchNext];
    const ms = a.dutch.every * 1000;
    el.textContent = Math.ceil((ms - (now - a.dutchStartedAt) % ms) / 1000);
  });
  const rc = $('[data-cd-rfq]');
  if (rc) rc.textContent = countdown(S.rfq.endAt - now);
}

setInterval(() => { tick(); botTick(); }, 1000);

// ---------- actions ----------
function confirmBid(a, max) {
  const premium = a.premium ? r2(max * a.premium / 100) : 0;
  const needsApproval = a.mode === 'b2b' && S.account === 'org' && max > ORG.you.perBidLimit;
  modal(`<h2>Confirm your bid</h2>
    <p class="muted small">${a.title}</p>
    <div class="lines">
      <span>Your maximum bid</span><span class="tnum">${money(max)}</span>
      ${premium ? `<span>Buyer’s premium (${a.premium}%) at max</span><span class="tnum">${money(premium)}</span>` : ''}
      <span>Shipping</span><span class="tnum">${a.ship.cost ? money(a.ship.cost) : 'Free'}</span>
      <span class="total">You could pay up to</span><span class="total tnum">${money(max + premium + (a.ship.cost || 0))}</span>
    </div>
    ${needsApproval ? `<div class="notice warn small">This is above your ${money(ORG.you.perBidLimit)} bid limit, so it goes to your org admin for approval first.</div>` : ''}
    <p class="small muted">Bids are binding. If you win, you’ll get a deal agreement at this price to accept within ${a.mode === 'b2b' ? '3 business days' : '48 hours'}.</p>
    <div class="dialog-actions"><button class="btn" data-action="close-modal">Cancel</button>
    <button class="btn primary" data-action="confirm-bid" data-id="${a.id}" data-max="${max}">${needsApproval ? 'Send for approval' : 'Confirm bid'}</button></div>`);
}

const actions = {
  mode: el => {
    // Mode tabs filter listings: stay on search (dropping conflicting seller/mode filters), otherwise go home.
    S.mode = el.dataset.mode;
    const { parts, params } = parseHash();
    let target = '#/';
    if (parts[0] === 'search') {
      const keep = new URLSearchParams();
      if (params.q) keep.set('q', params.q);
      if (params.cat) keep.set('cat', params.cat);
      target = '#/search' + (keep.toString() ? '?' + keep : '');
    }
    if (location.hash === target || (target === '#/' && !location.hash)) render();
    else location.hash = target;
  },
  watch: (el, e) => {
    e.preventDefault(); e.stopPropagation();
    const id = el.dataset.id;
    S.watch.has(id) ? S.watch.delete(id) : S.watch.add(id);
    toast(S.watch.has(id) ? '♥ Added to watchlist. We’ll remind you before it ends.' : 'Removed from watchlist.');
    render();
  },
  'use-org': () => { S.account = 'org'; toast('Now buying as <b>Acme Supply Co.</b>'); render(); },
  'close-modal': closeModal,
  quick: el => { const i = el.closest('form').querySelector('input'); i.value = el.dataset.v; i.focus(); },
  thumb: el => { $('.gallery .tile.main').style.setProperty('--h', el.dataset.h); $$('.thumbs .tile').forEach(t => t.classList.toggle('on', t === el)); },
  'confirm-bid': el => {
    const a = S.auctions[el.dataset.id];
    const max = +el.dataset.max;
    closeModal();
    if (a.mode === 'b2b' && S.account === 'org' && max > ORG.you.perBidLimit) {
      S.approvals.unshift({ id: 'ap' + Date.now(), who: ORG.you.name, itemId: a.id, amount: max, note: 'Above my bid limit' });
      toast('Sent to Tom Becker (Owner) for approval. <a href="#/business">View queue</a>');
      return;
    }
    const res = placeBid(a, 'You', max);
    if (!res.ok) { toast(res.msg, 'bad'); return; }
    a.youBid = true;
    toast(res.msg + (res.ext ? ' ⏱ Auction extended (soft close).' : ''), res.lead ? 'good' : 'bad');
    render();
  },
  'buy-now': el => {
    const a = S.auctions[el.dataset.id];
    const price = +el.dataset.price;
    modal(`<h2>Buy it now</h2><p>${a.title}</p>
      <div class="lines"><span>Price</span><span>${money(price)}</span><span>Shipping</span><span>${a.ship.cost ? money(a.ship.cost) : 'Free'}</span>
      <span class="total">Total</span><span class="total">${money(price + (a.ship.cost || 0))}</span></div>
      <div class="dialog-actions"><button class="btn" data-action="close-modal">Cancel</button><button class="btn primary" data-action="confirm-buy" data-id="${a.id}" data-price="${price}">Confirm order</button></div>
      <p class="small muted">This creates a deal agreement for you and the seller to accept.</p>`);
  },
  'confirm-buy': el => {
    const a = S.auctions[el.dataset.id];
    a.status = 'closed'; a.sold = true;
    closeModal();
    location.hash = '#/order/' + createOrder(a, +el.dataset.price);
  },
  'dutch-buy': el => {
    const a = S.auctions[el.dataset.id];
    const price = dutchPrice(a);
    modal(`<h2>Buy at ${money(price)}?</h2><p class="muted small">The price is locked when you confirm. The first buyer takes the whole lot.</p>
      <div class="dialog-actions"><button class="btn" data-action="close-modal">Keep waiting</button><button class="btn primary" data-action="confirm-buy" data-id="${a.id}" data-price="${price}">Buy now</button></div>`);
  },
  offer: el => {
    const a = S.auctions[el.dataset.id];
    modal(`<h2>Make an offer</h2><p class="muted small">${a.title} · listed at ${money(a.price)}</p>
      <form data-form="offer" data-id="${a.id}"><div class="money-input"><span>$</span><input name="amount" type="number" min="1" required placeholder="Your offer"></div>
      <p class="small muted">Offers are binding for 48 hours. The seller can accept, counter or decline.</p>
      <div class="dialog-actions"><button type="button" class="btn" data-action="close-modal">Cancel</button><button class="btn primary">Send offer</button></div></form>`);
  },
  'mark-received': el => {
    const o = S.orders.find(x => x.id === el.dataset.id);
    o.stage = 3; o.receivedAt = Date.now();
    toast('✓ Marked as received. The order is complete.', 'good');
    render();
  },
  'confirm-paid': el => {
    const o = S.orders.find(x => x.id === el.dataset.id);
    o.buyerPaidAt = Date.now();
    toast(`You confirmed the deal is paid. Waiting for ${o.seller} to confirm.`);
    render();
    sellerResponds(o, 'sellerPaidAt', () => { o.stage = 2; }, `✓ ${o.seller} confirmed they received payment. Deal paid.`);
  },
  dispute: el => {
    modal(`<h2>Report a problem</h2><form data-form="dispute" data-id="${el.dataset.id}">
      <div class="stack">${['Item not received', 'Not as described', 'Arrived damaged', 'Suspected counterfeit'].map((r, i) => `<label class="choice" style="display:flex;gap:8px"><input type="radio" name="reason" value="${r}" ${i ? '' : 'checked'}> ${r}</label>`).join('')}</div>
      <div class="dropzone" style="margin-top:10px;padding:14px">📎 Add photos / evidence</div>
      <div class="dialog-actions"><button type="button" class="btn" data-action="close-modal">Cancel</button><button class="btn primary">Open case</button></div></form>`);
  },
  review: () => { toast('★★★★★ Feedback posted. It becomes visible once the seller also leaves feedback, or after 14 days.'); },
  'me-tab': el => { S.meTab = el.dataset.tab; render(); },
  approve: el => {
    const ap = S.approvals.find(x => x.id === el.dataset.id);
    const a = S.auctions[ap.itemId];
    const res = placeBid(a, 'You', ap.amount);
    ap.status = 'Approved';
    if (res.ok) { a.youBid = true; toast(`Approved. Bid placed for Acme Supply Co. ${res.msg}`, res.lead ? 'good' : 'bad'); }
    else toast(`Approved, but the bid failed: ${res.msg}`, 'bad');
    render();
  },
  reject: el => { S.approvals.find(x => x.id === el.dataset.id).status = 'Rejected'; toast('Rejected. Requester notified.'); render(); },
  'rfq-view': el => { S.rfqView = el.dataset.v; render(); },
  award: el => {
    S.rfq.awarded = el.dataset.name;
    toast(`Awarded to <b>${el.dataset.name}</b>. Draft PO created and suppliers notified.`, 'good');
    render();
  },
  choose: el => {
    const d = S.draft;
    d[el.dataset.key] = el.dataset.val;
    if (el.dataset.key === 'as') d.mode = d.as === 'individual' ? 'c2c' : 'b2c';
    if (el.dataset.key === 'mode' && d.mode !== 'b2b' && ['sealed', 'dutch'].includes(d.format)) d.format = 'english';
    render();
  },
  'sell-next': () => { S.draft.step++; render(); },
  'sell-back': () => { S.draft.step--; render(); },
  publish: () => {
    const d = S.draft;
    const id = 'n' + Date.now();
    const cat = CATEGORIES.find(c => c.name === d.cat);
    const base = {
      id, title: d.title || 'My new listing', emoji: cat.emoji, hue: rand(0, 360), cat: d.cat, mode: d.mode, format: d.format,
      seller: { name: 'You', type: d.as, rating: 100, reviews: 0, since: 2026, verified: d.as === 'business' },
      cond: d.cond, returns: d.mode === 'b2c' ? '30-day returns' : 'No returns',
      area: d.area || S.loc.name, delivery: d.delivery,
      lat: S.loc.lat + (Math.random() - 0.5) * 0.03, lng: S.loc.lng + (Math.random() - 0.5) * 0.03,
      ship: d.delivery === 'pickup' ? { cost: 0, method: 'Pickup only' } : { cost: 10, method: 'Tracked parcel' },
      desc: 'Listing created in the mockup sell flow.', specs: { Category: d.cat }, watchers: 0,
      premium: +d.premium || 0, deposit: +d.deposit || 0, endsIn: (+d.duration || 3) * 86400, softClose: 120,
    };
    if (d.format === 'english') Object.assign(base, { start: +d.start || 1, price: +d.start || 1, reserve: +d.reserve || 0, buyNow: +d.buyNow || 0, bids: 0 });
    if (d.format === 'fixed') Object.assign(base, { price: +d.price || 1, offers: true });
    if (d.format === 'sealed') Object.assign(base, { start: +d.start || 1, price: +d.start || 1, sealedType: d.sealedType, sealedCount: 0 });
    if (d.format === 'dutch') Object.assign(base, { dutch: { start: +d.dutchStart, floor: +d.dutchFloor, drop: Math.max(1, Math.round((d.dutchStart - d.dutchFloor) / 200)), every: 5 } });
    addAuction(base);
    S.draft = null;
    toast('✓ Listing published. Passed automated moderation.', 'good');
    location.hash = '#/item/' + id;
  },
  'save-search': () => toast(`🔔 Saved. We’ll alert you when new matches are listed within ${radiusLabel()} of ${S.loc.name}.`),
  loc: (el, e) => {
    e.preventDefault();
    modal(`<h2>Your location</h2>
      <p class="small muted">We show listings near this point. Sellers never see your location.</p>
      <button class="btn block" data-action="geo">📍 Use my current location</button>
      <div class="stack">${PLACES.map((p, i) => `<button class="choice" data-action="set-place" data-i="${i}" aria-pressed="${S.loc.name === p.name}"><strong>${p.name}</strong></button>`).join('')}</div>
      <div class="field" style="margin-top:12px"><label for="rad">Search radius</label><select id="rad" data-change="radius">${radiusOptions()}</select></div>
      <div class="dialog-actions"><button class="btn primary" data-action="close-modal">Done</button></div>`);
  },
  'set-place': el => {
    S.loc = { ...PLACES[+el.dataset.i], radius: S.loc.radius };
    closeModal();
    toast(`📍 Showing listings near ${S.loc.name}.`);
    render();
  },
  geo: el => {
    if (!navigator.geolocation) { toast('Location isn’t available in this browser.', 'bad'); return; }
    el.textContent = 'Locating…';
    navigator.geolocation.getCurrentPosition(pos => {
      S.loc = { name: 'Current location', lat: pos.coords.latitude, lng: pos.coords.longitude, radius: S.loc.radius };
      closeModal();
      toast('📍 Using your current location.');
      render();
    }, err => {
      el.textContent = '📍 Use my current location';
      toast(`Couldn’t get your location (${err.message || 'permission denied'}). Pick a city instead.`, 'bad');
    }, { timeout: 8000 });
  },
  'search-view': el => { S.searchView = el.dataset.v; $$('[data-action="search-view"]').forEach(b => b.setAttribute('aria-pressed', b === el)); refreshResults(); },
  'pickup-time': () => toast('🗓 Pickup request sent. The seller will confirm a time in Messages (mockup).'),
  manifest: (el, e) => { e.preventDefault(); toast('Manifest download is not part of the mockup.'); },
  inspect: () => toast('📅 Inspection slots: Tue 10:00–14:00 and Thu 10:00–14:00 (mockup).'),
  msg: () => toast('Messaging opens in a side panel (not in mockup). Links and phone numbers are screened.'),
  follow: () => toast('Following seller. You’ll see their new listings in your feed.'),
  invite: () => toast('Invite sent (mockup).'),
  invoice: () => toast('PDF generation is not part of the mockup.'),
  noop: (el, e) => e.preventDefault(),
};

document.addEventListener('click', e => {
  const el = e.target.closest('[data-action]');
  if (el && actions[el.dataset.action]) actions[el.dataset.action](el, e);
});

document.addEventListener('keydown', e => { if (e.key === 'Escape' && !$('#modal').hidden) closeModal(); });

document.addEventListener('change', e => {
  const el = e.target;
  if (el.dataset.change === 'account') { S.account = el.value; render(); return; }
  if (el.dataset.change === 'radius') { S.loc.radius = +el.value; render(); return; }
  if (el.dataset.filter) {
    const set = S.filters[el.dataset.filter];
    el.checked ? set.add(el.value) : set.delete(el.value);
    refreshResults(); return;
  }
  if (el.dataset.filterFlag) { S.filters[el.dataset.filterFlag] = el.checked; refreshResults(); return; }
  if (el.dataset.filterSelect === 'radius') { S.loc.radius = +el.value; updateLocChip(); refreshResults(); return; }
  if (el.dataset.filterSelect) { S.filters[el.dataset.filterSelect] = el.value; refreshResults(); return; }
  if (el.dataset.bind && S.draft) S.draft[el.dataset.bind] = el.value;
});

document.addEventListener('input', e => {
  const el = e.target;
  if (el.dataset.bind && S.draft) S.draft[el.dataset.bind] = el.value;
});

const forms = {
  search: f => {
    const q = new FormData(f).get('q').trim();
    S.filters.formats.clear(); S.filters.sellers.clear(); S.filters.modes.clear();
    location.hash = '#/search?q=' + encodeURIComponent(q);
  },
  bid: f => {
    const a = S.auctions[f.dataset.id];
    const max = +new FormData(f).get('max');
    if (a.leader?.name !== 'You' && max < minNext(a)) { toast(`Enter ${money(minNext(a))} or more.`, 'bad'); return; }
    confirmBid(a, max);
  },
  sealed: f => {
    const a = S.auctions[f.dataset.id];
    const amount = +new FormData(f).get('amount');
    if (!a.yourSealed) a.sealedCount++;
    a.yourSealed = amount;
    toast(`✉️ Sealed bid of ${money(amount)} recorded. Nobody, including the seller, sees it until close.`, 'good');
    render();
  },
  offer: f => {
    const a = S.auctions[f.dataset.id];
    const amount = +new FormData(f).get('amount');
    closeModal();
    const ratio = amount / a.price;
    let status, msg;
    if (ratio >= 0.9) { status = 'Accepted'; msg = `✓ Seller accepted your offer of ${money(amount)}.`; }
    else if (ratio >= 0.75) { const c = Math.round((amount + a.price) / 2); status = `Countered ${money(c)}`; msg = `Seller countered at ${money(c)}. See <a href="#/me">My auctions → Offers</a>.`; }
    else { status = 'Declined'; msg = 'Seller declined your offer (auto-decline below 75%).'; }
    S.offers.unshift({ id: a.id, amount, status });
    toast(msg, status === 'Accepted' ? 'good' : status === 'Declined' ? 'bad' : '');
    if (status === 'Accepted') { a.status = 'closed'; a.sold = true; location.hash = '#/order/' + createOrder(a, amount); }
  },
  dispute: f => {
    const o = S.orders.find(x => x.id === f.dataset.id);
    o.dispute = new FormData(f).get('reason');
    closeModal();
    toast('Problem reported. Our team will review it and contact the seller.');
    render();
  },
  agreement: f => {
    const o = S.orders.find(x => x.id === f.dataset.id);
    const fd = new FormData(f);
    o.terms = { handover: fd.get('handover'), date: fd.get('date'), payment: fd.get('payment'), note: fd.get('note').trim() };
    o.buyerAcceptedAt = Date.now();
    toast(`📝 You accepted agreement ${o.agreementNo}. Sent to ${o.seller}.`);
    render();
    sellerResponds(o, 'sellerAcceptedAt', () => { o.stage = 1; }, `✓ ${o.seller} accepted the agreement. Deal agreed.`);
  },
  question: f => { toast('Question sent to the seller. You’ll be notified when they answer.'); f.reset(); },
  rfq: f => {
    const r = S.rfq;
    const price = r2(+new FormData(f).get('price'));
    const mine = r.bids.find(b => b.name === r.you);
    if (price > r2(mine.price - r.decrement)) { toast(`Must be at least ${money(r.decrement)} lower than your current bid.`, 'bad'); return; }
    mine.price = price;
    if (r.endAt - Date.now() < r.softClose * 1000) r.endAt = Date.now() + r.softClose * 1000;
    const rank = rfqRanked().findIndex(b => b.name === r.you) + 1;
    toast(`Bid ${money(price)} submitted. You’re now rank #${rank}.`, rank === 1 ? 'good' : '');
    render();
  },
};

document.addEventListener('submit', e => {
  const f = e.target;
  if (forms[f.dataset.form]) { e.preventDefault(); forms[f.dataset.form](f); }
});

render();
