/** Keep account redirects on this site and fall back safely for malformed input. */
export function accountDestination(next: string, origin: string): string {
  try {
    const destination = new URL(next, origin);
    if (destination.origin === origin) return destination.href;
  } catch {
    // Invalid return URLs must not turn a successful sign-in into an error.
  }
  return "/";
}
