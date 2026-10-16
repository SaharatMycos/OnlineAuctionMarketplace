"use client";

import { useState } from "react";
import Link from "next/link";
import { accountDestination } from "@/lib/navigation";

interface AccountFormProps {
  users?: { email: string; displayName: string }[];
  next: string;
  mode?: "signin" | "signup";
}

const messages = {
  signin: {
    submit: "Sign in",
    pending: "Signing in...",
    error: "We couldn't sign you in. Please try again.",
    switchPrompt: "New to Bidly? ",
    switchLabel: "Create an account",
    switchPath: "/signup",
    hint: "Choose a mock account below or enter any email above.",
  },
  signup: {
    submit: "Create account",
    pending: "Creating account...",
    error: "We couldn't create your account. Please try again.",
    switchPrompt: "Already have an account? ",
    switchLabel: "Sign in",
    switchPath: "/signin",
    hint: "A new email creates an account and signs you in. An existing email signs you into that account without changing its name.",
  },
};

export default function AccountForm({ users = [], next, mode = "signin" }: AccountFormProps) {
  const signingUp = mode === "signup";
  const text = messages[mode];
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);

  async function submitAccount(address: string) {
    if (pending) return;
    setError("");
    setPending(true);
    try {
      const res = await fetch("/api/session", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: address.trim(),
          displayName: signingUp ? displayName.trim() : undefined,
        }),
      });
      if (!res.ok) throw new Error("Account request failed");
      // A full navigation clears user-specific client state after changing accounts.
      window.location.assign(accountDestination(next, window.location.origin));
    } catch (err) {
      const message = err instanceof TypeError
        ? "Unable to connect. Check your connection and try again."
        : text.error;
      setError(message);
      setPending(false);
    }
  }

  return (
    <>
      <form className="form" aria-busy={pending} onSubmit={(event) => {
        event.preventDefault();
        void submitAccount(email);
      }}>
        {signingUp && <label htmlFor="signup-name">
          Display name
          <input id="signup-name" name="displayName" className="input" autoComplete="nickname" required maxLength={100}
            pattern=".*\S.*" title="Enter a display name containing at least one non-space character."
            placeholder="How others will see you" value={displayName} disabled={pending}
            onChange={(event) => setDisplayName(event.target.value)} />
        </label>}
        <label htmlFor="signin-email">
          Email address
          <input id="signin-email" name="email" className="input" type="email" autoComplete="email" required maxLength={320}
            placeholder="you@example.com" value={email} disabled={pending}
            aria-describedby={error ? "signin-error" : undefined}
            onChange={(event) => setEmail(event.target.value)} />
        </label>
        {error && <p id="signin-error" className="error" role="alert">{error}</p>}
        <button className="btn primary" disabled={pending}>{pending ? text.pending : text.submit}</button>
      </form>
      <p className="small">
        {text.switchPrompt}
        <Link href={`${text.switchPath}?next=${encodeURIComponent(next)}`}>
          {text.switchLabel}
        </Link>
      </p>
      <details className="signin-hint">
        <summary className="muted small">Hint: trying the demo?</summary>
        <p className="muted small">This preview uses development accounts without passwords or email verification. {text.hint} Production authentication is coming later.</p>
        {!signingUp && users.length > 0 && <div className="stack">
          {users.map((user) => (
            <button key={user.email} type="button" className="btn" disabled={pending} onClick={() => void submitAccount(user.email)}>
              {user.displayName} <span className="muted small">{user.email}</span>
            </button>
          ))}
        </div>}
      </details>
    </>
  );
}
