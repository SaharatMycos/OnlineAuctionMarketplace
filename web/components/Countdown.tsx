"use client";

import { useEffect, useState } from "react";
import { timeLeft } from "@/lib/format";

/**
 * Time left until <c>endsAt</c>. <c>offsetMs</c> is server time minus client time, so the countdown
 * follows the server clock (feature design §4.5), not the user's device clock.
 */
export default function Countdown({ endsAt, offsetMs = 0 }: { endsAt: string; offsetMs?: number }) {
  const [now, setNow] = useState<number | null>(null);

  useEffect(() => {
    setNow(Date.now() + offsetMs);
    const timer = setInterval(() => setNow(Date.now() + offsetMs), 1000);
    return () => clearInterval(timer);
  }, [offsetMs]);

  if (now === null) return <span className="tnum">…</span>;
  const urgent = new Date(endsAt).getTime() - now < 120_000;
  return <span className={`tnum${urgent ? " urgent" : ""}`}>{timeLeft(endsAt, now)}</span>;
}
