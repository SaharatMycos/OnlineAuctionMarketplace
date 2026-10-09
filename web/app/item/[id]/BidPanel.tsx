"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import Countdown from "@/components/Countdown";
import LocalTime from "@/components/LocalTime";
import { callApi, newIdempotencyKey, ProblemError } from "@/lib/client";
import { money } from "@/lib/format";
import { useRealtime } from "@/lib/realtime";
import type { Auction, BidHistoryItem, Order } from "@/lib/types";

interface Props {
  initialAuction: Auction;
  initialBids: BidHistoryItem[];
  signedIn: boolean;
  realtimeUrl: string;
  reservePrice: number | null;
}

/** Live bidding (feature design §4.1, §4.4, §4.5): price, countdown on server time, proxy max, Buy Now, history. */
export default function BidPanel({ initialAuction, initialBids, signedIn, realtimeUrl, reservePrice }: Props) {
  const [auction, setAuction] = useState(initialAuction);
  const [bids, setBids] = useState(initialBids);
  const [max, setMax] = useState("");
  const [confirming, setConfirming] = useState<number | null>(null);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [orderId, setOrderId] = useState<string | null>(null);
  const [extendedFlash, setExtendedFlash] = useState(false);

  const offsetMs = useMemo(
    () => new Date(auction.serverTime).getTime() - Date.now(),
    // Only the first load sets the offset; later views carry their own server time.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );
  const c = auction.currency;
  const live = auction.status === "live";
  const you = auction.you;

  const refresh = useCallback(async () => {
    const [a, b] = await Promise.all([
      callApi<Auction>(`/v1/auctions/${auction.id}`),
      callApi<BidHistoryItem[]>(`/v1/auctions/${auction.id}/bids`),
    ]);
    setAuction((current) => (a.seq >= current.seq ? a : current));
    setBids(b);
    // Drop a stale "you're winning" message once someone else takes the lead.
    setMessage((m) => (m?.kind === "ok" && !a.you?.isLeader ? null : m));
  }, [auction.id]);

  useRealtime(
    realtimeUrl,
    {
      "bid.placed": (e: { seq: number }) => {
        if (e.seq > auction.seq) refresh();
      },
      "auction.extended": () => {
        setExtendedFlash(true);
        setTimeout(() => setExtendedFlash(false), 4000);
        refresh();
      },
      "auction.closed": () => refresh(),
    },
    auction.id,
  );

  // Fallback when realtime is unavailable: poll while live, and refresh right after the end.
  useEffect(() => {
    if (!live) return;
    const timer = setInterval(refresh, 15_000);
    const endIn = new Date(auction.endsAt).getTime() - (Date.now() + offsetMs);
    const atEnd = setTimeout(refresh, Math.max(endIn + 1500, 0));
    return () => {
      clearInterval(timer);
      clearTimeout(atEnd);
    };
  }, [live, auction.endsAt, offsetMs, refresh]);

  // Winner or seller: find the deal agreement once the order exists.
  useEffect(() => {
    if (auction.status !== "sold" || !you || !(you.won || you.isSeller) || orderId) return;
    let stop = false;
    const look = async () => {
      try {
        const order = await callApi<Order>(`/v1/orders/by-auction/${auction.id}`);
        if (!stop) setOrderId(order.id);
      } catch {
        if (!stop) setTimeout(look, 1000);
      }
    };
    look();
    return () => {
      stop = true;
    };
  }, [auction.status, auction.id, you, orderId]);

  async function placeBid(amount: number) {
    setBusy(true);
    setMessage(null);
    try {
      const result = await callApi<{ isLeader: boolean; currentPrice: number; extended: boolean }>(`/v1/auctions/${auction.id}/bids`, {
        method: "POST",
        body: JSON.stringify({ maxAmount: amount }),
        idempotencyKey: newIdempotencyKey(),
      });
      setMessage(
        result.isLeader
          ? { kind: "ok", text: `You're the highest bidder at ${money(result.currentPrice, c)}. We'll bid for you up to ${money(amount, c)}.` }
          : { kind: "error", text: `Another bidder's maximum is higher. The price is now ${money(result.currentPrice, c)}.` },
      );
      setMax("");
      await refresh();
    } catch (e) {
      setMessage({ kind: "error", text: e instanceof ProblemError ? e.message : "Bid failed. Try again." });
    } finally {
      setBusy(false);
      setConfirming(null);
    }
  }

  async function buyNow() {
    if (!confirm(`Buy now for ${money(auction.buyNowPrice, c)}? This is binding.`)) return;
    setBusy(true);
    try {
      await callApi(`/v1/auctions/${auction.id}/buy-now`, { method: "POST" });
      await refresh();
    } catch (e) {
      setMessage({ kind: "error", text: e instanceof ProblemError ? e.message : "Buy Now failed." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="panel bid-panel">
      {live ? (
        <>
          <div className="small muted">Current bid · {auction.bidCount} {auction.bidCount === 1 ? "bid" : "bids"}</div>
          <div className="big-price tnum">{money(auction.currentPrice, c)}</div>
          <div className={`small${extendedFlash ? " flash" : ""}`}>
            Ends in <strong><Countdown endsAt={auction.endsAt} offsetMs={offsetMs} /></strong>
            {extendedFlash && <span className="badge time"> Extended: bid in the last 2 minutes</span>}
          </div>
          {auction.hasReserve && (
            <div className={`small ${auction.reserveMet ? "good" : "warn"}`}>
              {auction.reserveMet ? "Reserve met" : "Reserve not met"}
              {you?.isSeller && reservePrice !== null && <> (your reserve: {money(reservePrice, c)})</>}
            </div>
          )}
          {auction.leader && <div className="small muted">Highest bidder: {auction.leader}</div>}

          {you?.isSeller ? (
            <p className="note">This is your auction. You can&apos;t bid on it.</p>
          ) : !signedIn ? (
            <Link className="btn wide" href={`/signin?next=/item/${auction.listingId}`}>Sign in to bid</Link>
          ) : (
            <>
              {you?.hasBid && (
                <div className={`standing ${you.isLeader ? "good" : "bad"}`}>
                  {you.isLeader ? `You're winning. Your maximum: ${money(you.yourMax, c)}` : "You've been outbid."}
                </div>
              )}
              <form
                className="bid-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  const amount = Number(max);
                  if (!Number.isFinite(amount) || amount <= 0) return;
                  setConfirming(amount);
                }}
              >
                <label className="small" htmlFor="max">Your maximum bid (we bid for you, one increment at a time)</label>
                <div className="row">
                  <input
                    id="max"
                    className="input"
                    inputMode="decimal"
                    placeholder={`${auction.minNextBid.toFixed(2)} or more`}
                    value={max}
                    onChange={(e) => setMax(e.target.value)}
                  />
                  <button className="btn primary" disabled={busy}>Bid</button>
                </div>
                <div className="quick">
                  {[auction.minNextBid, auction.minNextBid * 1.1, auction.minNextBid * 1.25].map((v) => {
                    const amount = Math.ceil(v);
                    return (
                      <button type="button" key={amount} className="chip" onClick={() => setMax(String(amount))}>
                        {money(amount, c)}
                      </button>
                    );
                  })}
                </div>
              </form>
              {confirming !== null && (
                <div className="confirm">
                  <p>
                    Confirm a maximum bid of <strong>{money(confirming, c)}</strong>? Bids are binding. If you win, you pay
                    the seller directly as agreed in the deal agreement.
                  </p>
                  <div className="row">
                    <button className="btn primary" disabled={busy} onClick={() => placeBid(confirming)}>Confirm bid</button>
                    <button className="btn" onClick={() => setConfirming(null)}>Cancel</button>
                  </div>
                </div>
              )}
              {auction.buyNowPrice !== null && (
                <button className="btn wide" disabled={busy} onClick={buyNow}>Buy Now for {money(auction.buyNowPrice, c)}</button>
              )}
            </>
          )}
          {message && <p className={message.kind === "ok" ? "success" : "error"}>{message.text}</p>}
        </>
      ) : (
        <>
          <div className="small muted">{auction.status === "sold" ? "Sold for" : "Ended"}</div>
          <div className="big-price tnum">
            {auction.status === "sold" ? money(auction.finalPrice, c) : "No sale"}
          </div>
          {you?.won && <p className="success">You won this auction! 🎉</p>}
          {you?.isSeller && auction.status === "sold" && <p className="success">Your item sold.</p>}
          {(you?.won || (you?.isSeller && auction.status === "sold")) &&
            (orderId ? (
              <Link className="btn primary wide" href={`/orders/${orderId}`}>Open the deal agreement</Link>
            ) : (
              <p className="small muted">Preparing the deal agreement…</p>
            ))}
        </>
      )}

      <h3>Bid history</h3>
      {bids.length === 0 ? (
        <p className="small muted">No bids yet. Starting bid {money(auction.startPrice, c)}.</p>
      ) : (
        <ol className="history">
          {bids.map((b) => (
            <li key={b.seq}>
              <span>{b.bidder}{b.kind === "proxy" && <span className="muted small"> (auto)</span>}{b.kind === "buynow" && <span className="muted small"> (Buy Now)</span>}</span>
              <span className="tnum">{money(b.amount, c)}</span>
              <span className="muted small"><LocalTime value={b.at} /></span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
