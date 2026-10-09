"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export default function SignInForm({ users, next }: { users: { email: string; displayName: string }[]; next: string }) {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");

  async function signIn(address: string) {
    setError("");
    const res = await fetch("/api/session", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ email: address }),
    });
    if (!res.ok) return setError("Sign-in failed. Is the api running with Auth:DevTokens enabled?");
    router.push(next.startsWith("/") ? next : "/");
    router.refresh();
  }

  return (
    <>
      <div className="stack">
        {users.map((u) => (
          <button key={u.email} className="btn wide" onClick={() => signIn(u.email)}>
            {u.displayName} <span className="muted small">{u.email}</span>
          </button>
        ))}
      </div>
      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault();
          signIn(email);
        }}
      >
        <input className="input" type="email" required placeholder="you@example.test" value={email} onChange={(e) => setEmail(e.target.value)} />
        <button className="btn">Sign in</button>
      </form>
      {error && <p className="error">{error}</p>}
    </>
  );
}
