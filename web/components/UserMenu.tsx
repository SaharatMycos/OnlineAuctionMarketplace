"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";

export default function UserMenu({ user }: { user: { handle: string; displayName: string } | null }) {
  const router = useRouter();

  if (!user) return <Link className="btn small" href="/signin">Sign in</Link>;

  async function signOut() {
    await fetch("/api/session", { method: "DELETE" });
    router.push("/");
    router.refresh();
  }

  return (
    <div className="user">
      <Link href="/me" className="small">@{user.handle}</Link>
      <button className="link small" onClick={signOut}>Sign out</button>
    </div>
  );
}
