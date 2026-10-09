// Runtime configuration (read on the server at request time, so one image works everywhere).

/** Where the Next.js server reaches the api (inside compose: http://api:8080). */
export const API_URL = process.env.API_URL ?? "http://localhost:5080";

/** Where the browser reaches the realtime process. */
export const REALTIME_PUBLIC_URL = process.env.REALTIME_PUBLIC_URL ?? "http://localhost:5081";

export const COOKIE_TOKEN = "mp_token";
export const COOKIE_USER = "mp_user";
export const COOKIE_LOCATION = "mp_loc";

/** Demo default: Chicago, 25 km (feature design §9.1). */
export const DEFAULT_LOCATION = { lat: 41.8781, lng: -87.6298, area: "Chicago, IL", radiusKm: 25 as number | null };
