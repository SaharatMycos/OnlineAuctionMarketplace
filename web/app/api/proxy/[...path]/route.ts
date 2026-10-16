import { NextRequest, NextResponse } from "next/server";
import { API_URL, COOKIE_TOKEN, MAX_PHOTO_BYTES } from "@/lib/config";

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

  let body: ArrayBuffer | undefined;
  if (req.method !== "GET" && req.method !== "HEAD") {
    if (path.join("/") === "v1/listing-photos") {
      // Bound multipart buffering before forwarding to the API's independent image limit.
      const limited = await readAtMost(req, MAX_PHOTO_BYTES + 64 * 1024);
      if (!limited) return NextResponse.json({ detail: "Photos must be at most 10 MiB." }, { status: 413 });
      body = limited;
    } else {
      body = await req.arrayBuffer();
    }
  }
  const res = await fetch(target, { method: req.method, headers, body, cache: "no-store" });
  const responseHeaders = new Headers({ "content-type": res.headers.get("content-type") ?? "application/json" });
  for (const name of ["cache-control", "x-content-type-options"]) {
    const value = res.headers.get(name);
    if (value) responseHeaders.set(name, value);
  }
  return new NextResponse(res.status === 204 ? null : res.body, { status: res.status, headers: responseHeaders });
}

/** Buffers the request body, or returns null as soon as it exceeds `limit` bytes. */
async function readAtMost(req: NextRequest, limit: number): Promise<ArrayBuffer | null> {
  if (Number(req.headers.get("content-length") ?? 0) > limit) return null;
  const chunks: Uint8Array<ArrayBuffer>[] = [];
  let size = 0;
  const reader = req.body?.getReader();
  for (let r = await reader?.read(); r && !r.done; r = await reader!.read()) {
    size += r.value.byteLength;
    if (size > limit) {
      await reader!.cancel();
      return null;
    }
    chunks.push(r.value);
  }
  return new Blob(chunks).arrayBuffer();
}

export { forward as GET, forward as POST, forward as PUT, forward as PATCH, forward as DELETE };
