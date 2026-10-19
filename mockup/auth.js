'use strict';

// Local demo accounts only. No credentials, tokens or requests to production.
const DEMO_ACCOUNTS = [
  { email: 'alex@example.com', displayName: 'Alex Kim' },
  { email: 'bob@example.com', displayName: 'Bob' },
  { email: 'carol@example.com', displayName: 'Carol' },
];
let mockAccounts = [...DEMO_ACCOUNTS];
let mockUser = null;
let authPending = false;
let authError = '';
let authInput = { email: '', displayName: '' };
let authRevision = 0;
try {
  const stored = JSON.parse(localStorage.getItem('bidly.mock.accounts') || '[]');
  for (const user of Array.isArray(stored) ? stored : []) {
    if (typeof user?.email === 'string' && typeof user.displayName === 'string'
      && user.email.length <= 320 && user.displayName.length <= 100 && !mockAccounts.some(account => account.email === user.email)) mockAccounts.push(user);
  }
} catch { /* Continue with in-memory demo accounts when storage is unavailable. */ }
try {
  const email = sessionStorage.getItem('bidly.mock.session');
  mockUser = mockAccounts.find(user => user.email === email) || null;
} catch { /* The session will last only for this page visit. */ }

function safeMockDestination(value, fallback = '/') {
  return typeof value === 'string' && /^\/(?:$|(?:search|sell|me)(?:\?[^#\\\s]*)?$|(?:item|order)\/[a-zA-Z0-9-]+(?:\?[^#\\\s]*)?$)/.test(value) ? value : fallback;
}

function currentMockDestination() {
  return safeMockDestination(location.hash.slice(1));
}

function requireMockSignIn() {
  if (mockUser) return true;
  closeModal();
  location.hash = '#/signin?next=' + encodeURIComponent(currentMockDestination());
  render();
  return false;
}

function resetMockAccountState() {
  // Match a fresh page visit: do not carry "You" bids, deals, photos or drafts across accounts.
  closeModal();
  for (const auction of Object.values(S.auctions)) {
    for (const photo of auction.photos || []) if (!photo.demo) URL.revokeObjectURL(photo.url);
  }
  for (const photo of S.draft?.photos || []) if (!photo.demo) URL.revokeObjectURL(photo.url);
  S.auctions = {};
  for (const listing of LISTINGS) addAuction(listing);
  S.orders = mockUser?.email === 'alex@example.com' ? SEED_ORDERS.map(order => ({ ...order, terms: { ...order.terms } })) : [];
  S.watch = new Set();
  S.offers = [];
  S.approvals = [];
  S.draft = null;
  S.viewing = null;
  S.account = 'personal';
  S.mode = 'all';
  invalidateSellerSuggestion();
  photoBusy = false;
  photoError = '';
  const toasts = document.querySelector('#toasts');
  if (toasts) toasts.innerHTML = '';
}

function renderMockUserMenu() {
  const menu = document.querySelector('#mock-user-menu');
  if (menu) {
    const next = encodeURIComponent(currentMockDestination());
    menu.innerHTML = mockUser
      ? `<a href="#/me">${escapeText(mockUser.displayName)}</a><button class="btn sm" data-auth="signout">Sign out</button>`
      : `<a class="btn sm" href="#/signin?next=${next}">Sign in</a><a class="btn primary sm" href="#/signup?next=${next}">Sign up</a>`;
  }
  document.querySelectorAll('[data-signed-in]').forEach(element => { element.hidden = !mockUser; });
}

function previewAccount(mode, params = {}) {
  const signingUp = mode === 'signup';
  const next = safeMockDestination(params.next, signingUp ? '/me' : '/');
  return `<div class="panel mock-account-panel"><a href="#/" class="small">Back to browsing</a>
    <h1>${signingUp ? 'Create your account' : 'Welcome back'}</h1>
    <p class="muted">${signingUp ? 'Join Bidly' : 'Sign in'} to bid on nearby finds, open auctions and manage your deals.</p>
    <form data-auth-form="${mode}" data-next="${escapeText(next)}" aria-busy="${authPending}" class="mock-account-form">
    ${signingUp ? `<label for="mock-name">Display name<input id="mock-name" name="displayName" required maxlength="100" autocomplete="nickname" placeholder="How others will see you" value="${escapeText(authInput.displayName)}" ${authPending ? 'disabled' : ''}></label>` : ''}
    <label for="mock-email">Email address<input id="mock-email" name="email" required type="email" maxlength="320" autocomplete="email" placeholder="you@example.com" value="${escapeText(authInput.email)}" ${authPending ? 'disabled' : ''}></label>
    ${authError ? `<p class="notice warn" role="alert">${escapeText(authError)}</p>` : ''}
    <button class="btn primary" ${authPending ? 'disabled' : ''}>${authPending ? signingUp ? 'Creating account…' : 'Signing in…' : signingUp ? 'Create account' : 'Sign in'}</button></form>
    <p class="small">${signingUp ? 'Already have an account?' : 'New to Bidly?'} <a href="#/${signingUp ? 'signin' : 'signup'}?next=${encodeURIComponent(next)}">${signingUp ? 'Sign in' : 'Create an account'}</a></p>
    <p class="small muted">Local mock accounts only. No password or email verification. Use a demo email; nothing is sent to a server.</p>
    <details class="mock-account-hint"><summary>Hint: trying the demo?</summary><p class="small muted">Any email signs in or creates a local account. Existing emails keep their original display name. Accounts stay in this browser; sign-in lasts for this tab.</p>
    ${!signingUp ? DEMO_ACCOUNTS.map(user => `<button type="button" class="btn" data-auth="demo" data-email="${user.email}" data-next="${escapeText(next)}" ${authPending ? 'disabled' : ''}>${user.displayName} <span class="muted small">${user.email}</span></button>`).join('') : ''}</details></div>`;
}

function signInMockAccount(email, displayName, next, signingUp) {
  if (authPending) return;
  email = email.trim().toLowerCase();
  displayName = displayName.trim();
  authInput = { email, displayName };
  authError = '';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 320 || (signingUp && (!displayName || displayName.length > 100))) {
    authError = 'Enter a valid email address' + (signingUp ? ' and a display name of up to 100 characters.' : '.');
    render();
    return;
  }
  authPending = true;
  const revision = ++authRevision;
  render();
  setTimeout(() => {
    if (revision !== authRevision) return;
    const existing = mockAccounts.find(user => user.email === email);
    mockUser = existing || { email, displayName: displayName || email.split('@')[0] };
    if (!existing) mockAccounts.push(mockUser);
    try { localStorage.setItem('bidly.mock.accounts', JSON.stringify(mockAccounts)); } catch { /* In-memory accounts. */ }
    try { sessionStorage.setItem('bidly.mock.session', email); } catch { /* In-memory session. */ }
    authPending = false;
    authInput = { email: '', displayName: '' };
    resetMockAccountState();
    location.hash = '#' + safeMockDestination(next);
    render();
  }, 450);
}

document.addEventListener('submit', event => {
  const form = event.target;
  if (!['signin', 'signup'].includes(form.dataset.authForm)) return;
  event.preventDefault();
  const fields = new FormData(form);
  signInMockAccount(String(fields.get('email') || ''), String(fields.get('displayName') || ''), form.dataset.next, form.dataset.authForm === 'signup');
});

document.addEventListener('click', event => {
  const button = event.target.closest('[data-auth]');
  if (!button) return;
  if (button.dataset.auth === 'demo') signInMockAccount(button.dataset.email, '', button.dataset.next, false);
  if (button.dataset.auth === 'signout') {
    authRevision++;
    authPending = false;
    authError = '';
    authInput = { email: '', displayName: '' };
    mockUser = null;
    try { sessionStorage.removeItem('bidly.mock.session'); } catch { /* In-memory session. */ }
    resetMockAccountState();
    location.hash = '#/';
    render();
  }
});
