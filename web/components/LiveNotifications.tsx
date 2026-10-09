"use client";

import Link from "next/link";
import { useState } from "react";
import { money } from "@/lib/format";
import { useRealtime } from "@/lib/realtime";

interface Toast {
  id: number;
  text: string;
  href: string;
}

let nextId = 1;

/** Personal realtime events (user:{id} group): outbid, won, auction ended, deal updates. */
export default function LiveNotifications({ realtimeUrl }: { realtimeUrl: string }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  function push(text: string, href: string) {
    const toast = { id: nextId++, text, href };
    setToasts((t) => [...t.slice(-3), toast]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== toast.id)), 8000);
  }

  useRealtime(realtimeUrl, {
    "user.outbid": (e: { listingId: string; title: string; price: number }) =>
      push(`You've been outbid on "${e.title}" (now ${money(e.price)}).`, `/item/${e.listingId}`),
    "user.won": (e: { listingId: string; title: string; finalPrice: number }) =>
      push(`You won "${e.title}" for ${money(e.finalPrice)}! Review the deal agreement.`, `/item/${e.listingId}`),
    "auction.ended": (e: { listingId: string; title: string; sold: boolean }) =>
      push(e.sold ? `"${e.title}" sold. A deal agreement is ready.` : `"${e.title}" ended without a sale.`, `/item/${e.listingId}`),
    "order.updated": (e: { orderId: string; agreementNumber: string; action: string; title: string }) =>
      e.action === "created" ? undefined : push(`${e.agreementNumber} (${e.title}): ${e.action.replace("_", " ")}.`, `/orders/${e.orderId}`),
  });

  if (toasts.length === 0) return null;
  return (
    <div className="toasts" role="status" aria-live="polite">
      {toasts.map((t) => (
        <Link key={t.id} href={t.href} className="toast">
          {t.text}
        </Link>
      ))}
    </div>
  );
}
