const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const ts = require("typescript");

function sessionRoute() {
  const calls = [];
  const cookies = [];
  const response = (body, options = {}) => ({ body, status: options.status ?? 200, cookies: { set: (...args) => cookies.push(args) } });
  const context = {
    exports: {},
    require: (name) => name === "next/server"
      ? { NextResponse: { json: response } }
      : { API_URL: "http://api.test", COOKIE_TOKEN: "mp_token", COOKIE_USER: "mp_user" },
    fetch: async (url, options) => {
      calls.push({ url, body: JSON.parse(options.body) });
      return { ok: true, json: async () => ({ token: "test-token", user: { handle: "new-user", displayName: "New User" } }) };
    },
  };
  const source = fs.readFileSync(path.join(__dirname, "../app/api/session/route.ts"), "utf8");
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, context);
  return { POST: context.exports.POST, calls, cookies };
}

test("sign-up forwards trimmed profile and establishes the session", async () => {
  const route = sessionRoute();
  const result = await route.POST({ json: async () => ({ email: " new@example.test ", displayName: " New User " }) });
  assert.equal(result.status, 200);
  assert.deepEqual(route.calls[0].body, { email: "new@example.test", displayName: "New User" });
  assert.equal(route.cookies.find(([name]) => name === "mp_token")[2].httpOnly, true);
  assert.equal(JSON.parse(route.cookies.find(([name]) => name === "mp_user")[1]).displayName, "New User");
});

test("invalid sign-up input is rejected before contacting the API", async () => {
  for (const input of [null, { email: "invalid" }, { email: "new@example.test", displayName: " " }, { email: "new@example.test", displayName: "x".repeat(101) }]) {
    const route = sessionRoute();
    assert.equal((await route.POST({ json: async () => input })).status, 400);
    assert.equal(route.calls.length, 0);
  }
});

test("email-only sign-in remains supported", async () => {
  const route = sessionRoute();
  assert.equal((await route.POST({ json: async () => ({ email: "bob@example.test" }) })).status, 200);
  assert.deepEqual(route.calls[0].body, { email: "bob@example.test" });
});

test("malformed JSON returns a validation response", async () => {
  const route = sessionRoute();
  assert.equal((await route.POST({ json: async () => { throw new SyntaxError(); } })).status, 400);
  assert.equal(route.calls.length, 0);
});
