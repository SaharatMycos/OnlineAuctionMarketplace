// Server-only helpers (they read request cookies); import them from server components and route handlers.
import { cookies } from "next/headers";
import { API_URL, COOKIE_LOCATION, COOKIE_TOKEN, COOKIE_USER, DEFAULT_LOCATION } from "./config";
import type { BuyerLocation } from "./types";

export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string) {
    super(message);
  }
}

/** Server-side call to the api with the signed-in user's token. Throws ApiError on non-2xx. */
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const token = (await cookies()).get(COOKIE_TOKEN)?.value;
  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  });
  if (!res.ok) {
    const problem = await res.json().catch(() => ({}));
    throw new ApiError(res.status, problem.code ?? "error", problem.detail ?? res.statusText);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

/** Like api(), but returns null on 404 (and on any error when the api is down). */
export async function apiOrNull<T>(path: string): Promise<T | null> {
  try {
    return await api<T>(path);
  } catch (e) {
    if (e instanceof ApiError && e.status !== 404) throw e;
    return null;
  }
}

export async function apiStatus(path: string): Promise<number | null> {
  try {
    const res = await fetch(`${API_URL}${path}`, { cache: "no-store" });
    return res.status;
  } catch {
    return null;
  }
}

export interface SessionUser {
  handle: string;
  displayName: string;
}

export async function getSessionUser(): Promise<SessionUser | null> {
  const raw = (await cookies()).get(COOKIE_USER)?.value;
  if (!raw) return null;
  try {
    return JSON.parse(raw) as SessionUser;
  } catch {
    return null;
  }
}

export async function getBuyerLocation(): Promise<BuyerLocation> {
  const raw = (await cookies()).get(COOKIE_LOCATION)?.value;
  if (!raw) return DEFAULT_LOCATION;
  try {
    return { ...DEFAULT_LOCATION, ...(JSON.parse(raw) as Partial<BuyerLocation>) };
  } catch {
    return DEFAULT_LOCATION;
  }
}
