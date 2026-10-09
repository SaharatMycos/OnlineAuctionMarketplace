import { NextRequest, NextResponse } from "next/server";
import { API_URL, COOKIE_TOKEN } from "@/lib/config";

/**
 * Backend-for-frontend proxy: the browser calls /api/proxy/v1/..., this adds the bearer token from
 * the httpOnly cookie and forwards to the api. The token never reaches page JavaScript this way.
 */
async function forward(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const target = `${API_URL}/${path.map(encodeURIComponent).join("/")}${req.nextUrl.search}`;
  const token = req.cookies.get(COOKIE_TOKEN)?.value;

  const headers = new Headers();
  for (const name of ["content-type", "idempotency-key", "accept"]) {
    const value = req.headers.get(name);
    if (value) headers.set(name, value);
  }
  if (token) headers.set("authorization", `Bearer ${token}`);

  const body = req.method === "GET" || req.method === "HEAD" ? undefined : await req.text();
  const res = await fetch(target, { method: req.method, headers, body, cache: "no-store" });
  return new NextResponse(res.status === 204 ? null : await res.text(), {
    status: res.status,
    headers: { "content-type": res.headers.get("content-type") ?? "application/json" },
  });
}

export { forward as GET, forward as POST, forward as PUT, forward as PATCH, forward as DELETE };
