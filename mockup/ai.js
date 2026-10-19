'use strict';

// Deterministic, offline previews. No text, photo or listing is sent to an AI service.
const escapeText = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const TOPICS = [
  ['photography', 'Electronics', /camera|lens|lenses|sony|canon|nikon|photo|mirrorless/i],
  ['computers', 'Electronics', /laptop|computer|macbook|thinkpad|tablet|ipad/i],
  ['audio', 'Electronics', /audio|speaker|headphone|turntable/i],
  ['cycling', 'Sports & Outdoors', /bik(e|es|ing)|bicycle|cycling|trek/i],
  ['collectibles', 'Collectibles', /collect|card|pokemon|pokémon|vintage|rare/i],
  ['building toys', 'Toys & Hobbies', /lego|toy|brick|hobb/i],
  ['furniture', 'Home & Garden', /furniture|chair|sofa|table|desk|home|garden/i],
  ['fashion', 'Fashion', /fashion|shoe|jordan|sneaker|cloth|jacket/i],
  ['tools', 'Industrial', /tool|drill|industrial|machine|workshop/i],
];
let sellerRevision = 0;
let sellerBusy = false;
let sellerSuggestion = null;
let photoBusy = false;
let photoError = '';
let nextPhotoId = 1;

const DEMO_PHOTOS = {
  camera: { name: 'camera.jpg', label: 'Camera', emoji: '📷', color: '#dfdcf7' },
  bicycle: { name: 'bicycle.jpg', label: 'Bicycle', emoji: '🚲', color: '#dcebdd' },
  chair: { name: 'chair.jpg', label: 'Chair', emoji: '🪑', color: '#f3e3ce' },
};

function demoPhoto(key) {
  const sample = DEMO_PHOTOS[key];
  if (!sample) return null;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600" viewBox="0 0 800 600"><rect width="800" height="600" fill="${sample.color}"/><text x="400" y="300" text-anchor="middle" font-size="180">${sample.emoji}</text><text x="400" y="450" text-anchor="middle" font-family="sans-serif" font-size="32" fill="#292735">${sample.label} · Demo image</text></svg>`;
  return { id: nextPhotoId++, name: sample.name, url: 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg), demo: true };
}

function invalidateSellerSuggestion() {
  sellerRevision++;
  sellerBusy = false;
  sellerSuggestion = null;
}

function suggestFromPhotos() {
  invalidateSellerSuggestion();
  const photos = S.draft?.photos || [];
  if (!photos.length) return;
  const revision = sellerRevision;
  const draft = S.draft;
  // Uploaded files use filename cues; built-in samples have known demo identities.
  // This deliberately does not claim to perform visual recognition.
  const photoTopics = photos.map(photo => inferTopics(photo.name));
  const topics = { tags: [...new Set(photoTopics.flatMap(result => result.tags))],
    categories: [...new Set(photoTopics.flatMap(result => result.categories))] };
  const topic = topics.tags[0];
  const item = { photography: 'Camera', computers: 'Laptop', audio: 'Audio equipment', cycling: 'Bicycle',
    collectibles: 'Collectible item', 'building toys': 'Building toy set', furniture: 'Furniture', fashion: 'Fashion item', tools: 'Workshop tools' }[topic] || 'Item pictured in the photos';
  sellerBusy = true;
  setTimeout(() => {
    if (revision !== sellerRevision || S.draft !== draft) return;
    sellerBusy = false;
    sellerSuggestion = { title: `${item} for auction`,
      desc: `${item}, shown in the ${photos.length === 1 ? 'attached photo' : `${photos.length} attached photos`}. Please confirm the brand, model, condition, included accessories and any defects before publishing.`,
      cat: topics.categories[0] || draft.cat, tags: topics.tags.join(', ') };
    if (parseHash().parts[0] === 'sell') render();
  }, 650);
}

async function addListingPhotos(files) {
  if (photoBusy || !S.draft) return;
  saveSellFields();
  const draft = S.draft;
  draft.photos ||= [];
  photoError = '';
  const selected = Array.from(files);
  const remaining = 24 - draft.photos.length;
  if (selected.length > remaining) photoError = 'You can add up to 24 photos. Extra files were skipped.';
  photoBusy = true;
  invalidateSellerSuggestion();
  render();
  for (const file of selected.slice(0, remaining)) {
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 10 * 1024 * 1024 || !file.size) {
      photoError = 'Choose valid JPG, PNG or WebP photos up to 10 MB each.';
      continue;
    }
    const url = URL.createObjectURL(file);
    try {
      const image = new Image();
      image.src = url;
      await image.decode();
      if (!image.naturalWidth || !image.naturalHeight) throw new Error('Invalid image');
      if (S.draft !== draft) { URL.revokeObjectURL(url); continue; }
      draft.photos.push({ id: nextPhotoId++, name: file.name, url });
    } catch {
      URL.revokeObjectURL(url);
      photoError = 'One photo could not be opened. Choose another image.';
    }
  }
  photoBusy = false;
  if (S.draft !== draft) return;
  suggestFromPhotos();
  if (parseHash().parts[0] === 'sell') render();
}

function inferTopics(text) {
  const hits = TOPICS.filter(([, , pattern]) => pattern.test(text));
  const categories = [...new Set(hits.map(([, category]) => category))];
  for (const category of CATEGORIES) {
    if (text.toLowerCase().includes(category.name.toLowerCase())) categories.push(category.name);
  }
  return { tags: hits.map(([tag]) => tag), categories: [...new Set(categories)] };
}

function previewHome() {
  const live = all().filter(a => a.status === 'live' && a.endAt > Date.now());
  const near = live.filter(isLocal).sort((a, b) => dist(a) - dist(b));
  const ships = live.filter(a => !isLocal(a) && a.delivery !== 'pickup');
  const ending = live.filter(reachable).sort((a, b) => a.endAt - b.endAt).slice(0, 8);
  return `<section class="hero production-hero"><div><span class="pill info">Production preview · Mock AI</span>
    <h1>Auctions near ${escapeText(S.loc.name)}</h1>
    <p class="muted">Bid on things you can pick up within ${radiusLabel()}, or have shipped from farther away.</p>
    <div class="cats">${CATEGORIES.map(c => `<a class="chip" href="#/search?cat=${encodeURIComponent(c.name)}">${c.emoji} ${c.name}</a>`).join('')}</div></div></section>
    <section class="section"><div class="section-head"><h2>Near you</h2><a href="#/search">See all</a></div>${cards(near)}</section>
    ${ships.length ? `<section class="section"><div class="section-head"><h2>Ships to you</h2><a href="#/search?delivery=ship">See all</a></div>${cards(ships)}</section>` : ''}
    <section class="section"><h2>Ending soon</h2>${cards(ending)}</section>`;
}

function previewMe() {
  return `<h1>My auctions</h1><p class="muted">Signed in as ${escapeText(mockUser?.displayName || '')} · Local demo account</p>
    <section class="section"><h2>Deals</h2>${S.orders.map(o => `<div class="row"><a class="grow" href="#/order/${o.id}">${escapeText(o.title)}</a><span class="pill ${orderPillClass(o)}">${ORDER_PILL[o.stage]}</span></div>`).join('') || '<p class="empty">Win an auction to start a deal.</p>'}</section>
    <section class="section"><h2>Bidding</h2>${cards(all().filter(a => a.youBid))}</section>
    <section class="section"><h2>Auctions I opened</h2>${cards(all().filter(a => a.seller.name === 'You'))}</section>`;
}

function previewSell() {
  const d = S.draft ||= { ...newDraft(), desc: '', address: '', tags: '', duration: 1, shipping: 0, photos: [] };
  d.photos ||= [];
  const input = (key, label, type = 'text', extra = '') => `<div class="field"><label for="sell-${key}">${label}</label><input id="sell-${key}" name="${key}" type="${type}" value="${escapeText(d[key])}" ${extra}></div>`;
  return `<div class="preview-form"><h1>Start with your photos</h1><p class="muted">Add photos of your item. AI suggests a title, description and category for you to review.</p>
    <form data-preview-form="sell" class="panel" id="preview-sell">
    <fieldset><legend>1. Add item photos</legend>
    <div class="photo-dropzone" data-photo-drop><span class="photo-symbol" aria-hidden="true">＋</span><label class="btn primary" for="listing-photo-files">Choose photos</label>
    <input class="photo-file-input" id="listing-photo-files" type="file" accept="image/jpeg,image/png,image/webp" multiple ${photoBusy ? 'disabled' : ''}>
    <p>or drag and drop them here</p><p class="hint">JPG, PNG or WebP · Up to 24 photos · 10 MB each</p></div>
    <p class="small muted">Try a demo image: ${Object.entries(DEMO_PHOTOS).map(([key, sample]) => `<button type="button" class="chip" data-preview="demo-photo" data-key="${key}" ${photoBusy ? 'disabled' : ''}>${sample.emoji} ${sample.label}</button>`).join('')}</p>
    <div class="listing-photos">${d.photos.map((photo, index) => `<figure><img src="${escapeText(photo.url)}" alt="Item photo ${index + 1}"><figcaption>${index === 0 ? '<span class="pill info">Cover photo</span>' : `<button class="btn sm" type="button" data-preview="cover-photo" data-id="${photo.id}" ${photoBusy ? 'disabled' : ''}>Make cover</button>`}<button type="button" class="btn sm" data-preview="remove-photo" data-id="${photo.id}" aria-label="Remove photo ${index + 1}" ${photoBusy ? 'disabled' : ''}>Remove</button></figcaption></figure>`).join('')}</div>
    <p role="alert" class="photo-error">${escapeText(photoError)}</p><p class="hint">${d.photos.length}/24 photos · Your photos stay in this browser.</p>
    <div class="ai-section"><strong>AI photo assistant <span class="pill info">Mock</span></strong><p class="small muted">Demo images and uploaded filenames drive this simulation; it does not recognize image contents. Check every suggested detail.</p>
    <button class="btn" type="button" data-preview="seller-ai" ${sellerBusy || photoBusy || !d.photos.length ? 'disabled' : ''}>${photoBusy ? 'Opening photos…' : sellerBusy ? 'Analyzing photos…' : 'Suggest from photos'}</button>
    <div role="status" aria-live="polite">${sellerBusy ? '<p>Preparing your title, description and category…</p>' : sellerSuggestion ? `<h3>Suggested listing details</h3><p><b>${escapeText(sellerSuggestion.title)}</b></p><p>${escapeText(sellerSuggestion.desc)}</p><p class="small">Category: ${escapeText(sellerSuggestion.cat)} · ${escapeText(sellerSuggestion.tags)}</p><button class="btn primary" type="button" data-preview="apply-seller-ai">Use these suggestions</button><p class="hint">You can edit every field below before publishing.</p>` : '<p class="hint">Add a photo to automatically prepare suggestions. Your existing text stays unchanged until you apply them.</p>'}</div></div></fieldset>
    <fieldset><legend>2. Review item details</legend>
    ${input('title', 'Title', 'text', 'required maxlength="150" list="listing-titles" autocomplete="off"')}<datalist id="listing-titles">${LISTINGS.filter(a => a.format === 'english').map(a => `<option value="${escapeText(a.title)}"></option>`).join('')}</datalist>
    <div class="field"><label for="sell-desc">Description</label><textarea id="sell-desc" name="desc" required maxlength="4000" rows="4">${escapeText(d.desc)}</textarea></div>
    <div class="field"><label for="sell-cat">Category</label><select name="cat" id="sell-cat">${CATEGORIES.map(c => `<option ${d.cat === c.name ? 'selected' : ''}>${c.name}</option>`).join('')}</select></div>
    <div class="field"><label for="sell-cond">Condition</label><select name="cond" id="sell-cond">${['New', 'Used, like new', 'Used, excellent', 'Used, good', 'Used, fair', 'For parts'].map(c => `<option ${d.cond === c ? 'selected' : ''}>${c}</option>`).join('')}</select></div>
    ${input('tags', 'Tags (comma separated)')}
    </fieldset>
    <fieldset><legend>3. Location and delivery</legend>${input('area', 'Public area', 'text', 'required maxlength="100"')}${input('address', 'Private pickup address (use demo text only)', 'text', 'maxlength="200"')}
    <div class="field"><label for="sell-delivery">Delivery</label><select id="sell-delivery" name="delivery">${Object.entries(DELIVERY_LABEL).map(([key, label]) => `<option value="${key}" ${d.delivery === key ? 'selected' : ''}>${label}</option>`).join('')}</select></div>${input('shipping', 'Shipping cost ($)', 'number', 'min="0" step="0.01"')}</fieldset>
    <fieldset><legend>4. Auction pricing</legend><div class="form-grid">${input('start', 'Starting price ($)', 'number', 'required min="0.01" step="0.01"')}${input('reserve', 'Reserve price ($, private)', 'number', 'min="0.01" step="0.01"')}${input('buyNow', 'Buy now price ($, optional)', 'number', 'min="0.01" step="0.01"')}</div>
    <div class="field"><label for="sell-duration">Duration</label><select id="sell-duration" name="duration">${[1, 3, 5, 7, 10].map(n => `<option value="${n}" ${+d.duration === n ? 'selected' : ''}>${n} day${n === 1 ? '' : 's'}</option>`).join('')}</select></div></fieldset>
    <button class="btn primary" ${photoBusy || !d.photos.length ? 'disabled' : ''}>Publish auction</button><p class="hint">Demo only. A win creates a deal agreement; payment happens directly between buyer and seller.</p></form></div>`;
}

function saveSellFields() {
  const form = document.querySelector('#preview-sell');
  if (form) Object.assign(S.draft, Object.fromEntries(new FormData(form)));
}

document.addEventListener('input', e => {
  if (e.target.closest('#preview-sell')) {
    saveSellFields();
  }
});

document.addEventListener('click', e => {
  const button = e.target.closest('[data-preview]');
  if (!button) return;
  if (button.dataset.preview !== 'view-photo' && !requireMockSignIn()) return;
  if (button.dataset.preview === 'seller-ai') {
    saveSellFields();
    if (!S.draft?.photos?.length) { toast('Add an item photo first.'); return; }
    suggestFromPhotos();
    render();
  } else if (button.dataset.preview === 'demo-photo') {
    if (photoBusy) return;
    saveSellFields();
    if (S.draft.photos.length >= 24) { photoError = 'You can add up to 24 photos.'; render(); return; }
    const photo = demoPhoto(button.dataset.key);
    if (!photo) return;
    S.draft.photos.push(photo);
    photoError = '';
    suggestFromPhotos();
    render();
  } else if (['remove-photo', 'cover-photo'].includes(button.dataset.preview)) {
    if (photoBusy || !S.draft) return;
    saveSellFields();
    const index = S.draft.photos.findIndex(photo => photo.id === +button.dataset.id);
    if (index < 0) return;
    const [photo] = S.draft.photos.splice(index, 1);
    if (button.dataset.preview === 'cover-photo') S.draft.photos.unshift(photo);
    else if (!photo.demo) URL.revokeObjectURL(photo.url);
    photoError = '';
    suggestFromPhotos();
    render();
  } else if (button.dataset.preview === 'view-photo') {
    const photo = S.auctions[button.dataset.auction]?.photos?.[+button.dataset.index];
    const image = document.querySelector('.gallery .tile.main img');
    if (photo && image) image.src = photo.url;
  } else if (button.dataset.preview === 'apply-seller-ai' && sellerSuggestion) {
    saveSellFields();
    Object.assign(S.draft, sellerSuggestion);
    sellerSuggestion = null;
    render();
  }
});

document.addEventListener('submit', e => {
  const form = e.target;
  const kind = form.dataset.previewForm;
  if (!kind) return;
  e.preventDefault();
  if (!requireMockSignIn()) return;
  if (kind === 'sell') {
    saveSellFields();
    const d = S.draft;
    if (photoBusy || !d.photos?.length) { toast('Add at least one item photo before publishing.'); return; }
    if (!d.title.trim() || !d.desc.trim()) { toast('Add a title and description.'); return; }
    if ((d.reserve && +d.reserve < +d.start) || (d.buyNow && +d.buyNow <= Math.max(+d.start, +d.reserve || 0))) {
      toast('Reserve must be at least the starting price; buy now must be above both.', 'bad'); return;
    }
    const category = CATEGORIES.find(c => c.name === d.cat);
    const { address, ...publicDraft } = d;
    const a = addAuction({ ...publicDraft, id: 'n' + Date.now(), title: escapeText(d.title.trim()), desc: escapeText(d.desc.trim()),
      area: escapeText(d.area), format: 'english', mode: 'c2c', seller: { name: 'You', type: 'individual', rating: 100, reviews: 0, since: 2026 },
      emoji: category.emoji, hue: 245, lat: S.loc.lat, lng: S.loc.lng, start: +d.start, price: +d.start,
      reserve: +d.reserve || 0, buyNow: +d.buyNow || 0, bids: 0, watchers: 0, endsIn: +d.duration * 86400,
      premium: 0, deposit: 0, returns: 'As agreed with seller', specs: {}, ship: { cost: +d.shipping || 0, method: 'As agreed' } });
    // The private demo address never enters public listing state.
    S.draft = null;
    sellerSuggestion = null;
    invalidateSellerSuggestion();
    toast('Demo auction published.', 'good');
    location.hash = '#/item/' + a.id;
  }
});

document.addEventListener('change', e => {
  if (e.target.id === 'listing-photo-files') void addListingPhotos(e.target.files);
});

for (const type of ['dragover', 'dragleave', 'drop']) {
  document.addEventListener(type, e => {
    const zone = e.target.closest('[data-photo-drop]');
    if (!zone) return;
    e.preventDefault();
    zone.classList.toggle('dragging', type === 'dragover');
    if (type === 'drop') void addListingPhotos(e.dataTransfer.files);
  });
}

// Native datalist offers keyboard-accessible completion without a network dependency.
const searchOptions = document.createElement('datalist');
searchOptions.id = 'auction-completions';
searchOptions.innerHTML = [...new Set(LISTINGS.filter(a => a.format === 'english').map(a => a.title).concat(TOPICS.map(([tag]) => tag)))]
  .map(text => `<option value="${escapeText(text)}"></option>`).join('');
document.body.append(searchOptions);
document.querySelectorAll('form[data-form="search"] input').forEach(input => {
  input.setAttribute('list', searchOptions.id);
  input.setAttribute('autocomplete', 'off');
  input.placeholder = 'Search near you · mock autocomplete';
});
