"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

export default function UserMenu({ user }: { user: { handle: string; displayName: string } | null }) {
  const pathname = usePathname();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");

  const next = pathname === "/signin" || pathname === "/signup" ? "" : `?next=${encodeURIComponent(pathname)}`;
  if (!user) {
    return (
      <div className="user">
        <Link className="btn small" href={`/signin${next}`}>Sign in</Link>
        <Link className="btn primary small" href={`/signup${next}`}>Sign up</Link>
      </div>
    );
  }

  async function signOut() {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      const response = await fetch("/api/session", { method: "DELETE" });
      if (!response.ok) throw new Error("Sign-out failed");
      window.location.assign("/");
    } catch {
      setError("Couldn't sign out. Please try again.");
      setPending(false);
    }
  }

  return (
    <div>
      <div className="user">
        <Link href="/me" className="small" title={`@${user.handle}`}>{user.displayName}</Link>
        <button className="btn small" disabled={pending} onClick={() => void signOut()}>{pending ? "Signing out..." : "Sign out"}</button>
      </div>
      {error && <p className="error small" role="alert">{error}</p>}
    </div>
  );
}
