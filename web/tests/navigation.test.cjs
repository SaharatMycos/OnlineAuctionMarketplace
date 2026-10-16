const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const ts = require("typescript");

const source = fs.readFileSync(path.join(__dirname, "../lib/navigation.ts"), "utf8");
const context = { exports: {}, URL };
vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, context);
const { accountDestination } = context.exports;
const origin = "https://bidly.test";

test("account redirect preserves local paths, search filters and fragments", () => {
  assert.equal(accountDestination("/search?q=bike#results", origin), `${origin}/search?q=bike#results`);
  assert.equal(accountDestination(`${origin}/me`, origin), `${origin}/me`);
});

test("account redirect falls back for external and executable URLs", () => {
  for (const next of ["https://other.test/me", "//other.test/me", "javascript:alert(1)", "data:text/html,test"]) {
    assert.equal(accountDestination(next, origin), "/");
  }
});

test("malformed return URL cannot fail a successful sign-in", () => {
  assert.equal(accountDestination("http://[", origin), "/");
});
