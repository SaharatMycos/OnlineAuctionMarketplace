import { NextRequest, NextResponse } from "next/server";
import { API_URL, COOKIE_TOKEN, COOKIE_USER } from "@/lib/config";

const cookieOptions = { path: "/", sameSite: "lax" as const, maxAge: 60 * 60 * 12 };

/** Dev sign-in: exchanges an email for a token from the api's dev endpoint (dev auth only). */
export async function POST(req: NextRequest) {
  let input: { email?: unknown; displayName?: unknown };
  try {
    input = await req.json();
  } catch {
    return NextResponse.json({ error: "Invalid request." }, { status: 400 });
  }
  if (!input || typeof input.email !== "string") {
    return NextResponse.json({ error: "Enter a valid email address." }, { status: 400 });
  }
  const email = input.email.trim();
  if (!email || email.length > 320 || !email.includes("@")) {
    return NextResponse.json({ error: "Enter a valid email address." }, { status: 400 });
  }

  let displayName: string | undefined;
  if (input.displayName !== undefined) {
    if (typeof input.displayName !== "string") {
      return NextResponse.json({ error: "Enter a display name of up to 100 characters." }, { status: 400 });
    }
    displayName = input.displayName.trim();
    if (!displayName || displayName.length > 100) {
      return NextResponse.json({ error: "Enter a display name of up to 100 characters." }, { status: 400 });
    }
  }
  const res = await fetch(`${API_URL}/v1/dev/token`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ email, displayName }),
    cache: "no-store",
  });
  if (!res.ok) return new NextResponse(await res.text(), { status: res.status });

  const { token, user } = (await res.json()) as { token: string; user: { handle: string; displayName: string } };
  const response = NextResponse.json({ user });
  response.cookies.set(COOKIE_TOKEN, token, { ...cookieOptions, httpOnly: true });
  response.cookies.set(COOKIE_USER, JSON.stringify({ handle: user.handle, displayName: user.displayName }), cookieOptions);
  return response;
}

/** For the realtime connection, which talks to the realtime process directly. */
export async function GET(req: NextRequest) {
  return NextResponse.json({ token: req.cookies.get(COOKIE_TOKEN)?.value ?? null });
}

export async function DELETE() {
  const response = NextResponse.json({ ok: true });
  response.cookies.delete(COOKIE_TOKEN);
  response.cookies.delete(COOKIE_USER);
  return response;
}
