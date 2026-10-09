// Seed data for the mockup. Times are seconds from page load.
const INCREMENTS = [
  [1, 0.05], [5, 0.25], [25, 0.5], [100, 1], [250, 2.5],
  [500, 5], [1000, 10], [2500, 25], [5000, 50], [Infinity, 100],
];
function increment(price) {
  for (const [limit, inc] of INCREMENTS) if (price < limit) return inc;
}

const CATEGORIES = [
  { name: 'Electronics', emoji: '📷' },
  { name: 'Collectibles', emoji: '🃏' },
  { name: 'Fashion', emoji: '👟' },
  { name: 'Home & Garden', emoji: '🛋️' },
  { name: 'Toys & Hobbies', emoji: '🧱' },
  { name: 'Industrial', emoji: '🏭' },
  { name: 'Liquidation lots', emoji: '📦' },
  { name: 'Food & Agriculture', emoji: '🌹' },
  { name: 'Sports & Outdoors', emoji: '🚲' },
];

// Places the buyer can pick in the location chooser. Chicago is the demo default (most local listings).
const PLACES = [
  { name: 'Chicago, IL', lat: 41.8781, lng: -87.6298 },
  { name: 'Brooklyn, NY', lat: 40.6782, lng: -73.9442 },
  { name: 'Dallas, TX', lat: 32.7767, lng: -96.7970 },
  { name: 'Seattle, WA', lat: 47.6062, lng: -122.3321 },
  { name: 'Portland, OR', lat: 45.5152, lng: -122.6784 },
  { name: 'San Diego, CA', lat: 32.7157, lng: -117.1611 },
];

const SELLERS = {
  mai: { name: 'mai_shoots', type: 'individual', rating: 99.2, reviews: 212, since: 2019, verified: true },
  ken: { name: 'ken.collects', type: 'individual', rating: 100, reviews: 87, since: 2016, verified: true },
  renew: { name: 'RenewTech Store', type: 'business', rating: 98.7, reviews: 12400, since: 2014, verified: true },
  clear: { name: 'ClearStock Liquidation', type: 'business', rating: 97.9, reviews: 3120, since: 2012, verified: true },
  heavy: { name: 'Northline Industrial Auctions', type: 'business', rating: 99.1, reviews: 860, since: 2008, verified: true },
  bloom: { name: 'Bloomfield Growers Co-op', type: 'business', rating: 98.4, reviews: 410, since: 2017, verified: true },
  estate: { name: 'Harper & Sons Estate Sales', type: 'business', rating: 99.5, reviews: 1980, since: 2003, verified: true },
  sam: { name: 'sneakerSam', type: 'individual', rating: 98.1, reviews: 54, since: 2021, verified: false },
  cafe: { name: 'BaristaPro Equipment', type: 'business', rating: 99.0, reviews: 640, since: 2015, verified: true },
};

const LISTINGS = [
  {
    id: 'a1', title: 'Sony A7 III mirrorless camera body, 8k shutter count', emoji: '📷', hue: 220,
    cat: 'Electronics', mode: 'c2c', format: 'english', seller: SELLERS.mai,
    cond: 'Used, excellent', loc: 'Portland, OR', ship: { cost: 18, method: 'Tracked parcel, 2–4 days' },
    returns: '14-day returns, buyer pays return shipping',
    desc: 'Lightly used body, always kept in a dry cabinet. Includes 2 batteries, charger, strap and original box. No scratches on the sensor.',
    specs: { Brand: 'Sony', Model: 'ILCE-7M3', Mount: 'Sony E', 'Shutter count': '8,214' },
    start: 400, price: 860, reserve: 800, bids: 14, rivalMax: 905, endsIn: 95, softClose: 120, watchers: 41,
  },
  {
    id: 'a2', title: 'Vintage Omega Seamaster automatic, 1968, serviced', emoji: '⌚', hue: 40,
    cat: 'Collectibles', mode: 'c2c', format: 'english', seller: SELLERS.ken,
    cond: 'Pre-owned, serviced 2025', loc: 'Austin, TX', ship: { cost: 25, method: 'Insured express' },
    returns: 'No returns (authenticity guaranteed)',
    desc: 'Cal. 565 movement, recent full service with paperwork. Original crown, aftermarket strap.',
    specs: { Brand: 'Omega', Year: '1968', Movement: 'Automatic cal. 565', Case: '35 mm steel' },
    start: 500, price: 1450, reserve: 2000, bids: 9, rivalMax: 1600, endsIn: 3 * 3600 + 720, softClose: 120, watchers: 96,
  },
  {
    id: 'a3', title: 'iPhone 15 Pro 256GB, refurbished Grade A, 1-year warranty', emoji: '📱', hue: 260,
    cat: 'Electronics', mode: 'b2c', format: 'english', seller: SELLERS.renew,
    cond: 'Refurbished, Grade A', loc: 'Newark, NJ', ship: { cost: 0, method: 'Free 2-day shipping' },
    returns: '30-day free returns', buyNow: 799,
    desc: 'Professionally refurbished, new battery (100% health), 1-year seller warranty. Tax invoice provided.',
    specs: { Brand: 'Apple', Storage: '256 GB', Color: 'Natural Titanium', Battery: '100%' },
    start: 499, price: 499, bids: 0, endsIn: 28 * 3600, softClose: 120, watchers: 12,
  },
  {
    id: 'a4', title: 'Lot: 120 returned small kitchen appliances (1 pallet, manifested)', emoji: '📦', hue: 25,
    cat: 'Liquidation lots', mode: 'b2b', format: 'english', seller: SELLERS.clear,
    cond: 'Customer returns, untested', loc: 'Columbus, OH (pickup or freight)', ship: { cost: 240, method: 'LTL freight quote / pickup' },
    returns: 'Sold as-is', premium: 10, deposit: 500, manifest: true,
    desc: 'Mixed brands: blenders, air fryers, toasters, kettles. Estimated retail value $11,400. Full manifest available.',
    specs: { Units: '120', 'Est. retail value': '$11,400', Pallets: '1', Weight: '410 kg' },
    start: 1000, price: 3200, bids: 21, rivalMax: 3350, endsIn: 2 * 3600 + 300, softClose: 120, watchers: 58,
  },
  {
    id: 'a5', title: 'Haas VF-2 CNC vertical mill (2015), 4,100 spindle hours', emoji: '🏭', hue: 190,
    cat: 'Industrial', mode: 'b2b', format: 'sealed', sealedType: 'second', seller: SELLERS.heavy,
    cond: 'Used, working, inspected', loc: 'Detroit, MI', ship: { cost: 0, method: 'Buyer arranges rigging & freight' },
    returns: 'Sold as-is after inspection window', premium: 8, deposit: 2000,
    desc: 'Inspection day available. Sealed second-price: the highest bidder wins and pays the second-highest bid plus one increment.',
    specs: { Make: 'Haas', Model: 'VF-2', Year: '2015', Travels: '30" × 16" × 20"' },
    start: 18000, price: 18000, bids: 0, sealedCount: 7, endsIn: 2 * 86400 + 5 * 3600, watchers: 33,
  },
  {
    id: 'a6', title: '500 kg fresh-cut roses, mixed colours (Dutch clock, today’s harvest)', emoji: '🌹', hue: 345,
    cat: 'Food & Agriculture', mode: 'b2b', format: 'dutch', seller: SELLERS.bloom,
    cond: 'Fresh, grade A1', loc: 'Salinas, CA', ship: { cost: 120, method: 'Refrigerated truck, same day' },
    returns: 'Quality claims within 24 h', premium: 0, deposit: 0,
    desc: 'The price drops every 5 seconds until someone buys. The first buyer takes the whole lot.',
    specs: { Weight: '500 kg', Stems: '≈ 9,000', Grade: 'A1', Harvest: 'Today' },
    dutch: { start: 2400, floor: 1200, drop: 25, every: 5 }, endsIn: 45 * 60, watchers: 19,
  },
  {
    id: 'a7', title: 'LEGO Star Wars UCS Millennium Falcon 75192, sealed', emoji: '🧱', hue: 0,
    cat: 'Toys & Hobbies', mode: 'c2c', format: 'english', seller: SELLERS.ken,
    cond: 'New, factory sealed', loc: 'Denver, CO', ship: { cost: 45, method: 'Tracked freight box' },
    returns: '14-day returns if seal unbroken',
    desc: 'Box has minor shelf wear on one corner, seals intact.',
    specs: { Set: '75192', Pieces: '7,541', Condition: 'Sealed' },
    start: 300, price: 610, bids: 18, rivalMax: 640, endsIn: 6 * 3600 + 1500, softClose: 120, watchers: 77,
  },
  {
    id: 'a8', title: 'Nike Air Jordan 1 “Chicago” (2015), US 10, with receipt', emoji: '👟', hue: 5,
    cat: 'Fashion', mode: 'c2c', format: 'fixed', seller: SELLERS.sam,
    cond: 'Worn twice', loc: 'Brooklyn, NY', ship: { cost: 15, method: 'Tracked, 2–3 days' },
    returns: 'No returns', offers: true,
    desc: 'Original box and receipt. Light creasing on toe box.',
    specs: { Brand: 'Nike', Size: 'US 10', Year: '2015', SKU: '555088-101' },
    price: 1200, endsIn: 20 * 86400, watchers: 23,
  },
  {
    id: 'a9', title: 'Lot: 40 × Dell Latitude 7440 laptops (i7, 16GB, off-lease)', emoji: '💻', hue: 210,
    cat: 'Liquidation lots', mode: 'b2b', format: 'english', seller: SELLERS.clear,
    cond: 'Off-lease, Grade B, wiped', loc: 'Dallas, TX', ship: { cost: 180, method: 'Freight or pickup' },
    returns: 'DOA claims within 7 days', premium: 10, deposit: 1000, manifest: true,
    desc: 'Data wiped to NIST 800-88, certificate provided. Chargers included, no OS.',
    specs: { Units: '40', CPU: 'Intel i7-1365U', RAM: '16 GB', Storage: '512 GB SSD' },
    start: 6000, price: 8200, bids: 6, rivalMax: 8600, endsIn: 26 * 3600, softClose: 120, watchers: 31,
  },
  {
    id: 'a10', title: 'Mid-century teak sideboard (Danish, c. 1960)', emoji: '🗄️', hue: 30,
    cat: 'Home & Garden', mode: 'b2c', format: 'english', seller: SELLERS.estate,
    cond: 'Vintage, restored', loc: 'Chicago, IL', ship: { cost: 0, method: 'Local pickup only' },
    returns: 'No returns; viewing available',
    desc: 'Four sliding doors, two drawers. Professionally re-oiled. 180 × 45 × 78 cm.',
    specs: { Era: '1960s', Material: 'Teak', Width: '180 cm' },
    start: 150, price: 340, reserve: 300, bids: 5, rivalMax: 360, endsIn: 9 * 3600, softClose: 120, watchers: 22,
  },
  {
    id: 'a11', title: 'Pokémon Base Set Charizard holo #4, PSA 8', emoji: '🃏', hue: 15,
    cat: 'Collectibles', mode: 'c2c', format: 'english', seller: SELLERS.ken,
    cond: 'Graded PSA 8', loc: 'Seattle, WA', ship: { cost: 12, method: 'Insured, signature' },
    returns: 'No returns (graded)',
    desc: 'Unlimited print, PSA 8 NM-MT. Slab in perfect condition.',
    specs: { Set: 'Base Set', Grade: 'PSA 8', Cert: '#7731…' },
    start: 900, price: 2300, bids: 33, rivalMax: 2380, endsIn: 25 * 60, softClose: 120, watchers: 140,
  },
  {
    id: 'a12', title: 'La Marzocco Linea PB 2-group commercial espresso machine', emoji: '☕', hue: 20,
    cat: 'Industrial', mode: 'b2c', format: 'fixed', seller: SELLERS.cafe,
    cond: 'Used, serviced, 6-month warranty', loc: 'San Diego, CA', ship: { cost: 150, method: 'Palletised freight' },
    returns: '14-day returns, restocking fee 10%', offers: true,
    desc: 'Serviced with new gaskets and group heads. Ideal for café openings.',
    specs: { Groups: '2', Voltage: '220V', Year: '2019' },
    price: 6900, endsIn: 30 * 86400, watchers: 15,
  },
];

// Item locations: [lat, lng, public area name, delivery]. lat/lng is the seller's exact point (private);
// the app only ever shows a fuzzed ~1 km area and a rounded distance.
const GEO = {
  a1: [45.5265, -122.6812, 'Pearl District, Portland', 'both'],
  a2: [30.2669, -97.7428, 'Downtown Austin', 'ship'],
  a3: [40.7357, -74.1724, 'Newark, NJ', 'ship'],
  a4: [39.9612, -82.9988, 'Columbus, OH', 'both'],
  a5: [42.3314, -83.0458, 'Detroit, MI', 'pickup'],
  a6: [36.6777, -121.6555, 'Salinas, CA', 'both'],
  a7: [39.7311, -104.9817, 'Capitol Hill, Denver', 'both'],
  a8: [40.6872, -73.9418, 'Bed-Stuy, Brooklyn', 'both'],
  a9: [32.7831, -96.8067, 'Dallas, TX', 'both'],
  a10: [41.9214, -87.6513, 'Lincoln Park, Chicago', 'pickup'],
  a11: [47.6253, -122.3222, 'Capitol Hill, Seattle', 'ship'],
  a12: [32.7254, -117.1689, 'Little Italy, San Diego', 'both'],
};

const LOCALS = {
  dan: { name: 'dan_rides', type: 'individual', rating: 99.0, reviews: 31, since: 2020, verified: true },
  oak: { name: 'oakpark_moving', type: 'individual', rating: 100, reviews: 4, since: 2025, verified: true },
  grill: { name: 'grillmaster_e', type: 'individual', rating: 97.5, reviews: 12, since: 2022, verified: true },
  naper: { name: 'naper_gamer', type: 'individual', rating: 98.8, reviews: 66, since: 2019, verified: true },
  bistro: { name: 'Loop Bistro Group', type: 'business', rating: 99.3, reviews: 48, since: 2018, verified: true },
  hp: { name: 'hydepark_fit', type: 'individual', rating: 100, reviews: 9, since: 2023, verified: false },
};

// Chicago-area listings so the "Near you" feed has something to show for the default location.
LISTINGS.push(
  {
    id: 'l1', title: 'Trek FX 3 Disc hybrid bike (2022), size M', emoji: '🚲', hue: 120,
    cat: 'Sports & Outdoors', mode: 'c2c', format: 'english', seller: LOCALS.dan,
    cond: 'Used, good', ship: { cost: 0, method: 'Pickup only' }, returns: 'No returns',
    desc: 'Carbon fork, hydraulic disc brakes. New tyres in spring. Test ride welcome before bidding.',
    specs: { Brand: 'Trek', Model: 'FX 3 Disc', Size: 'M', Year: '2022' },
    start: 150, price: 320, bids: 6, rivalMax: 335, endsIn: 4 * 3600 + 600, softClose: 120, watchers: 18,
    lat: 41.9088, lng: -87.6776, area: 'Wicker Park, Chicago', delivery: 'pickup',
  },
  {
    id: 'l2', title: 'IKEA KALLAX 4×4 shelf unit, white (moving sale)', emoji: '🗃️', hue: 45,
    cat: 'Home & Garden', mode: 'c2c', format: 'english', seller: LOCALS.oak,
    cond: 'Used, good', ship: { cost: 0, method: 'Pickup only' }, returns: 'No returns',
    desc: 'Already disassembled, all hardware in a bag. Must go by Saturday.',
    specs: { Brand: 'IKEA', Model: 'KALLAX', Size: '147 × 147 cm' },
    start: 10, price: 45, bids: 4, rivalMax: 50, endsIn: 50 * 60, softClose: 120, watchers: 9,
    lat: 41.8850, lng: -87.7845, area: 'Oak Park', delivery: 'pickup',
  },
  {
    id: 'l3', title: 'Weber Genesis II E-310 gas grill with cover', emoji: '🔥', hue: 10,
    cat: 'Home & Garden', mode: 'c2c', format: 'fixed', seller: LOCALS.grill,
    cond: 'Used, cleaned', ship: { cost: 0, method: 'Pickup only' }, returns: 'No returns', offers: true,
    desc: 'Three burners, works perfectly. Propane tank not included.',
    specs: { Brand: 'Weber', Model: 'Genesis II E-310', Fuel: 'Propane' },
    price: 350, endsIn: 14 * 86400, watchers: 6,
    lat: 42.0451, lng: -87.6877, area: 'Evanston', delivery: 'pickup',
  },
  {
    id: 'l4', title: 'Nintendo Switch OLED + 3 games (Zelda TOTK, Mario Kart, Smash)', emoji: '🎮', hue: 0,
    cat: 'Electronics', mode: 'c2c', format: 'english', seller: LOCALS.naper,
    cond: 'Used, excellent', ship: { cost: 12, method: 'Tracked parcel, 2–3 days' }, returns: '14-day returns',
    desc: 'Screen protector since day one. Pickup in Naperville or I can ship.',
    specs: { Model: 'OLED (white)', Storage: '64 GB', Games: '3 cartridges' },
    start: 120, price: 210, bids: 11, rivalMax: 225, endsIn: 2 * 3600 + 400, softClose: 120, watchers: 27,
    lat: 41.7508, lng: -88.1535, area: 'Naperville', delivery: 'both',
  },
  {
    id: 'l5', title: 'Restaurant closing: 30 bistro chairs + 8 marble tables (lot)', emoji: '🪑', hue: 30,
    cat: 'Liquidation lots', mode: 'b2b', format: 'english', seller: LOCALS.bistro,
    cond: 'Used, commercial grade', ship: { cost: 0, method: 'Pickup with loading dock, Mon–Fri' }, returns: 'Sold as-is',
    premium: 5, deposit: 200, manifest: true,
    desc: 'Full dining-room set from a closing West Loop bistro. Inspection by appointment.',
    specs: { Chairs: '30', Tables: '8 (marble top)', Pickup: 'Loading dock' },
    start: 600, price: 1800, bids: 9, rivalMax: 1900, endsIn: 26 * 3600, softClose: 120, watchers: 14,
    lat: 41.8827, lng: -87.6480, area: 'West Loop, Chicago', delivery: 'pickup',
  },
  {
    id: 'l6', title: 'Peloton Bike+ with shoes (size 42) and weights', emoji: '🚴', hue: 350,
    cat: 'Sports & Outdoors', mode: 'c2c', format: 'english', seller: LOCALS.hp,
    cond: 'Used, like new', ship: { cost: 0, method: 'Pickup only (ground floor)' }, returns: 'No returns',
    desc: 'About 40 rides. Subscription not included. Help loading available.',
    specs: { Model: 'Bike+', Extras: 'Shoes, 2 lb weights, mat' },
    start: 400, price: 780, bids: 7, rivalMax: 820, endsIn: 7 * 3600, softClose: 120, watchers: 21,
    lat: 41.7943, lng: -87.5907, area: 'Hyde Park, Chicago', delivery: 'pickup',
  },
);

for (const l of LISTINGS) {
  if (l.lat === undefined && GEO[l.id]) [l.lat, l.lng, l.area, l.delivery] = GEO[l.id];
}

const HANDLES = ['k***3', 'p***a', 'm***9', 'z***1', 'r***e', 'j***7', 't***4'];

const SEED_ORDERS = [
  { id: 'o1001', title: 'Kindle Paperwhite (11th gen), 16GB', emoji: '📖', hue: 160, amount: 74, premium: 0, ship: 6, seller: 'readmore_jo', stage: 2, mode: 'c2c',
    delivery: 'ship', area: 'Logan Square, Chicago', agreementNo: 'AG-1001',
    createdAt: Date.now() - 3 * 86400e3, acceptBy: Date.now() - 86400e3,
    buyerAcceptedAt: Date.now() - 3 * 86400e3 + 600e3, sellerAcceptedAt: Date.now() - 3 * 86400e3 + 3600e3,
    buyerPaidAt: Date.now() - 2 * 86400e3, sellerPaidAt: Date.now() - 2 * 86400e3 + 1800e3,
    terms: { handover: 'Shipping', date: new Date(Date.now() - 86400e3).toISOString().slice(0, 10), payment: 'Bank transfer', note: '' } },
];

const ORG = {
  name: 'Acme Supply Co.', taxId: 'US-EIN 12-3456789', verified: true,
  you: { name: 'Anna Ruiz', role: 'Buyer', perBidLimit: 5000 },
  budget: 50000, spent: 18240, depositsHeld: 1500,
  members: [
    { name: 'Tom Becker', role: 'Owner', limit: '—' },
    { name: 'Anna Ruiz', role: 'Buyer', limit: '$5,000 / bid' },
    { name: 'Jamie Lee', role: 'Buyer', limit: '$2,000 / bid' },
    { name: 'Priya Shah', role: 'Finance', limit: 'Invoices & payouts' },
  ],
};

const SEED_APPROVALS = [
  { id: 'ap1', who: 'Jamie Lee', itemId: 'a9', amount: 9500, note: 'Replacement laptops for the Dallas office' },
];

const RFQ = {
  id: 'r1', title: 'Corrugated shipping boxes 40×30×30 cm, 50,000 units', buyer: 'Acme Supply Co.',
  unit: 'per box', qty: 50000, ceiling: 0.95, decrement: 0.01, endsIn: 14 * 60, softClose: 120,
  terms: 'Delivery to Columbus, OH within 21 days. Net 30 via Bidly invoicing. FSC-certified board required.',
  weights: { price: 70, delivery: 15, quality: 15 },
  you: 'Siam Packaging',
  bids: [
    { name: 'PackRight Ltd', price: 0.78, delivery: 80, quality: 90 },
    { name: 'BoxCo Industries', price: 0.81, delivery: 95, quality: 85 },
    { name: 'Siam Packaging', price: 0.84, delivery: 90, quality: 92 },
    { name: 'GreenCarton', price: 0.86, delivery: 70, quality: 95 },
  ],
};
