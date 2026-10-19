const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Exercise the actual browser scripts with controlled timers and minimal DOM adapters.
function fixture({ signedIn = true, storageUnavailable = false, initialStorage, initialSession } = {}) {
  const listeners = {};
  const timers = [];
  const nodes = new Map();
  const storage = new Map(initialStorage || []);
  const sessions = new Map(initialSession || (signedIn ? [['bidly.mock.session', 'alex@example.com']] : []));
  const node = () => ({ innerHTML: '', hidden: true, value: '', textContent: '',
    setAttribute() {}, append() {}, remove() {}, querySelectorAll: () => [] });
  const document = {
    body: node(), documentElement: node(), createElement: node,
    querySelector: selector => nodes.get(selector) || null,
    querySelectorAll: () => [],
    addEventListener: (type, handler) => (listeners[type] ||= []).push(handler),
  };
  for (const key of ['#app', '#loc-label', '[data-change="account"]', '#toasts', '#modal', '#mock-user-menu']) nodes.set(key, node());
  const revoked = [];
  let objectId = 0;
  const context = vm.createContext({ document, console, URLSearchParams, Date, Math,
    URL: { createObjectURL: () => `blob:mock-${++objectId}`, revokeObjectURL: url => revoked.push(url) },
    Image: class { naturalWidth = 800; naturalHeight = 600; decode() { return Promise.resolve(); } },
    localStorage: { getItem: key => { if (storageUnavailable) throw Error('Unavailable'); return storage.get(key) || null; }, setItem: (key, value) => { if (storageUnavailable) throw Error('Unavailable'); storage.set(key, value); }, removeItem: key => storage.delete(key) },
    sessionStorage: { getItem: key => sessions.get(key) || null, setItem: (key, value) => sessions.set(key, value), removeItem: key => sessions.delete(key) },
    location: { hash: '#/' }, window: { addEventListener() {} },
    getComputedStyle: () => ({ getPropertyValue: () => '' }),
    setInterval() {}, setTimeout: callback => timers.push(callback),
    FormData: class {
      constructor(form) { this.fields = form.fields; }
      get(key) { return this.fields[key] ?? null; }
      getAll(key) { const value = this.get(key); return Array.isArray(value) ? value : value === null ? [] : [value]; }
      *[Symbol.iterator]() { yield* Object.entries(this.fields); }
    },
  });
  const run = code => vm.runInContext(code, context);
  for (const file of ['data.js', 'auth.js', 'ai.js', 'app.js']) run(fs.readFileSync(path.join(__dirname, file), 'utf8'));
  return {
    run, nodes, storage, sessions, revoked,
    flush() { for (const timer of timers.splice(0)) timer(); },
    submit(kind, fields) {
      const target = { dataset: { previewForm: kind }, fields };
      for (const handler of listeners.submit) handler({ target, preventDefault() {} });
    },
    submitAuth(kind, fields, next = '/me') {
      const target = { dataset: { authForm: kind, next }, fields };
      for (const handler of listeners.submit) handler({ target, preventDefault() {} });
    },
    authClick(action) {
      const button = { dataset: { auth: action } };
      for (const handler of listeners.click) handler({ target: { closest: selector => selector === '[data-auth]' ? button : null } });
    },
    click(action, data = {}) {
      const button = { dataset: { preview: action, ...data }, closest: () => null };
      for (const handler of listeners.click) handler({ target: { closest: selector => selector === '[data-preview]' ? button : null } });
    },
  };
}

test('core home, account and listing pages render with production navigation targets', () => {
  const f = fixture();
  assert.match(f.nodes.get('#app').innerHTML, /Near you/);
  // Bidder suggestions were cut from production scope, so the mock no longer previews them.
  assert.doesNotMatch(f.nodes.get('#app').innerHTML, /Suggested for you/);
  assert.equal(f.run('safeMockDestination("/interests")'), '/');
  assert.match(f.run('previewMe()'), /Auctions I opened/);
  assert.match(f.run('previewSell()'), /AI photo assistant/);
  assert.ok(f.run('previewSell()').indexOf('Add item photos') < f.run('previewSell()').indexOf('Review item details'));
  assert.equal(f.run('all().every(a => a.format === "english")'), true);
});

test('seller assist requires explicit apply and does not publish stale private data', () => {
  const f = fixture();
  f.run('previewSell();');
  f.click('demo-photo', { key: 'camera' });
  f.flush();
  assert.equal(f.run('S.draft.title'), '');
  assert.equal(f.run('S.draft.tags'), '');
  assert.match(f.run('sellerSuggestion.tags'), /photography/);
  f.click('apply-seller-ai');
  assert.match(f.run('S.draft.tags'), /photography/);
  f.run('S.draft.address = "PRIVATE_DEMO_ADDRESS"');
  f.submit('sell', {});
  assert.equal(f.run('Object.values(S.auctions).some(a => a.address === "PRIVATE_DEMO_ADDRESS")'), false);
  assert.equal(f.run('all().filter(a => a.seller.name === "You").length'), 1);
  assert.match(f.run('card(all().find(a => a.seller.name === "You"))'), /<img src="data:image\/svg/);
});

test('image-first flow refuses text-only publish and ignores late results after removing photos', () => {
  const f = fixture();
  f.run('previewSell(); S.draft.title = "Camera"; S.draft.desc = "A camera";');
  f.submit('sell', {});
  assert.equal(f.run('all().filter(a => a.seller.name === "You").length'), 0);
  f.click('demo-photo', { key: 'camera' });
  const id = f.run('S.draft.photos[0].id');
  f.click('remove-photo', { id: String(id) });
  f.flush();
  assert.equal(f.run('sellerSuggestion'), null);
  assert.equal(f.run('sellerBusy'), false);
  assert.equal(f.run('S.draft.photos.length'), 0);
});

test('file uploads validate types and sizes, show previews and suggest from filename cues', async () => {
  const f = fixture();
  f.run('previewSell();');
  await f.run(`addListingPhotos([
    {name:'bicycle.webp', type:'image/webp', size:500},
    {name:'unsafe.svg', type:'image/svg+xml', size:100},
    {name:'huge.jpg', type:'image/jpeg', size:11*1024*1024}
  ])`);
  f.flush();
  assert.equal(f.run('S.draft.photos.length'), 1);
  assert.match(f.run('previewSell()'), /blob:mock-1/);
  assert.equal(f.run('sellerSuggestion.cat'), 'Sports & Outdoors');
  assert.match(f.run('sellerSuggestion.title'), /Bicycle/);
  f.click('remove-photo', { id: String(f.run('S.draft.photos[0].id')) });
  assert.deepEqual(f.revoked, ['blob:mock-1']);
});

test('cover selection reorders published images and unknown filenames avoid invented recognition', async () => {
  const f = fixture();
  f.run('previewSell();');
  f.click('demo-photo', { key: 'camera' });
  f.click('demo-photo', { key: 'chair' });
  const id = f.run('S.draft.photos[1].id');
  f.click('cover-photo', { id: String(id) });
  assert.equal(f.run('S.draft.photos[0].name'), 'chair.jpg');
  f.flush();
  assert.equal(f.run('sellerSuggestion.cat'), 'Home & Garden');
  f.run('S.draft.photos = []');
  await f.run('addListingPhotos([{name:"IMG_0001.jpg", type:"image/jpeg", size:100}])');
  f.flush();
  assert.equal(f.run('sellerSuggestion.title'), 'Item pictured in the photos for auction');
});

test('broken images are released and photo count is capped', async () => {
  const f = fixture();
  f.run('previewSell(); Image.prototype.decode = () => Promise.reject(new Error("bad image"));');
  await f.run('addListingPhotos([{name:"broken.jpg", type:"image/jpeg", size:100}])');
  assert.equal(f.run('S.draft.photos.length'), 0);
  assert.deepEqual(f.revoked, ['blob:mock-1']);
  f.run('Image.prototype.decode = () => Promise.resolve();');
  await f.run('addListingPhotos(Array.from({length:25}, () => ({name:"camera.jpg", type:"image/jpeg", size:100})))');
  assert.equal(f.run('S.draft.photos.length'), 24);
  assert.match(f.run('photoError'), /24 photos/);
});

test('signed-out browsing has account links and protected routes preserve the return page', () => {
  const f = fixture({ signedIn: false });
  assert.match(f.nodes.get('#mock-user-menu').innerHTML, /Sign in/);
  assert.match(f.nodes.get('#mock-user-menu').innerHTML, /Sign up/);
  assert.equal(f.run('mockUser'), null);
  f.run('location.hash = "#/sell"; render();');
  assert.equal(f.run('location.hash'), '#/signin?next=%2Fsell');
  assert.match(f.nodes.get('#app').innerHTML, /data-next="\/sell"/);
  assert.doesNotMatch(f.nodes.get('#app').innerHTML, /id="preview-sell"/);
  f.submitAuth('signin', { email: 'bob@example.com' }, '/sell');
  assert.equal(f.run('authPending'), true);
  f.flush();
  assert.equal(f.run('location.hash'), '#/sell');
  assert.equal(f.run('mockUser.displayName'), 'Bob');
  assert.match(f.nodes.get('#app').innerHTML, /Start with your photos/);
});

test('sign-up creates a normalized local account, signs in and existing emails keep their name', () => {
  const f = fixture({ signedIn: false });
  f.run('location.hash = "#/signup"; render();');
  f.submitAuth('signup', { email: ' NEW@example.com ', displayName: ' New User ' });
  f.flush();
  assert.equal(f.run('mockUser.email'), 'new@example.com');
  assert.equal(f.run('mockUser.displayName'), 'New User');
  assert.equal(f.sessions.get('bidly.mock.session'), 'new@example.com');
  assert.match(f.storage.get('bidly.mock.accounts'), /New User/);
  f.authClick('signout');
  assert.equal(f.run('mockUser'), null);
  assert.equal(f.run('location.hash'), '#/');
  assert.equal(f.sessions.size, 0);
  f.submitAuth('signup', { email: 'new@example.com', displayName: 'Replacement name' });
  f.flush();
  assert.equal(f.run('mockUser.displayName'), 'New User');
});

test('sign-out clears private runtime state', () => {
  const f = fixture();
  f.run('previewSell(); S.draft.title = "Private draft"; all()[0].youBid = true;');
  f.authClick('signout');
  assert.equal(f.run('S.draft'), null);
  assert.equal(f.run('S.orders.length'), 0);
  assert.equal(f.run('all().some(a => a.youBid)'), false);
  f.submitAuth('signin', { email: 'bob.com' });
  f.flush();
  assert.equal(f.run('S.draft'), null);
});

test('invalid input is retryable, names are escaped, and return routes cannot be external', () => {
  const f = fixture({ signedIn: false });
  f.run('location.hash = "#/signup"; render();');
  f.submitAuth('signup', { email: 'bad', displayName: ' ' });
  assert.equal(f.run('mockUser'), null);
  assert.equal(f.run('authPending'), false);
  assert.match(f.nodes.get('#app').innerHTML, /role="alert"/);
  f.submitAuth('signup', { email: 'valid@example.com', displayName: '<img src=x onerror=alert(1)>' }, '//evil.example');
  f.flush();
  assert.equal(f.run('location.hash'), '#/');
  assert.doesNotMatch(f.nodes.get('#mock-user-menu').innerHTML, /<img src=x/);
  for (const value of ['https://evil.example', '//evil.example', '/\\evil.example', '/signin', '/signup', '/sell\n']) {
    assert.equal(f.run(`safeMockDestination(${JSON.stringify(value)})`), '/');
  }
});

test('sign-in works without local storage and sign-out invalidates pending AI work', () => {
  const f = fixture({ signedIn: false, storageUnavailable: true });
  f.submitAuth('signin', { email: 'bob@example.com' });
  f.flush();
  assert.equal(f.run('mockUser.displayName'), 'Bob');
  f.run('previewSell();');
  f.click('demo-photo', { key: 'camera' });
  assert.equal(f.run('sellerBusy'), true);
  f.authClick('signout');
  f.flush();
  assert.equal(f.run('sellerSuggestion'), null);
  assert.equal(f.run('sellerBusy'), false);
});

test('registered accounts and tab sessions restore on reload, and sign-out survives reload', () => {
  const f = fixture({ signedIn: false });
  f.submitAuth('signup', { email: 'saved@example.com', displayName: 'Saved User' });
  f.flush();
  const restored = fixture({ signedIn: false, initialStorage: f.storage, initialSession: f.sessions });
  assert.equal(restored.run('mockUser.displayName'), 'Saved User');
  assert.match(restored.nodes.get('#mock-user-menu').innerHTML, /Saved User/);
  restored.authClick('signout');
  const signedOut = fixture({ signedIn: false, initialStorage: restored.storage, initialSession: restored.sessions });
  assert.equal(signedOut.run('mockUser'), null);
});
