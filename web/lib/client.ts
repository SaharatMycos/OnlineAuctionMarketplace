"use client";

import { COOKIE_LOCATION, DEFAULT_LOCATION } from "./config";
import type { BuyerLocation } from "./types";

export class ProblemError extends Error {
  constructor(public status: number, public code: string, message: string) {
    super(message);
  }
}

/** Browser-side api call through the /api/proxy BFF route (adds the token server-side). */
export async function callApi<T>(path: string, init?: RequestInit & { idempotencyKey?: string }): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (init?.body) headers["Content-Type"] = "application/json";
  if (init?.idempotencyKey) headers["Idempotency-Key"] = init.idempotencyKey;

  const res = await fetch(`/api/proxy${path}`, { ...init, headers: { ...headers, ...(init?.headers as object) } });
  if (!res.ok) {
    const problem = await res.json().catch(() => ({}));
    if (res.status === 401) throw new ProblemError(401, "unauthorized", "Sign in to do this.");
    throw new ProblemError(res.status, problem.code ?? "error", problem.detail ?? `Request failed (${res.status}).`);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

export function readLocationCookie(): BuyerLocation {
  const match = document.cookie.split("; ").find((c) => c.startsWith(`${COOKIE_LOCATION}=`));
  if (!match) return DEFAULT_LOCATION;
  try {
    return { ...DEFAULT_LOCATION, ...JSON.parse(decodeURIComponent(match.split("=").slice(1).join("="))) };
  } catch {
    return DEFAULT_LOCATION;
  }
}

export function writeLocationCookie(location: BuyerLocation) {
  const value = encodeURIComponent(JSON.stringify(location));
  document.cookie = `${COOKIE_LOCATION}=${value}; path=/; max-age=${60 * 60 * 24 * 365}; samesite=lax`;
}

export function newIdempotencyKey() {
  return crypto.randomUUID();
}
