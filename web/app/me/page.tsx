import Link from "next/link";
import { redirect } from "next/navigation";
import Countdown from "@/components/Countdown";
import { api, getSessionUser } from "@/lib/api";
import { money, orderStatusLabel } from "@/lib/format";
import type { Listing, MyBidding, OrderSummary } from "@/lib/types";

export const dynamic = "force-dynamic";

const standingLabel = { winning: "Winning", outbid: "Outbid", won: "Won", lost: "Lost" };

export default async function MePage() {
  const user = await getSessionUser();
  if (!user) redirect("/signin?next=/me");

  const [bidding, orders, listings] = await Promise.all([
    api<MyBidding[]>("/v1/me/bidding"),
    api<OrderSummary[]>("/v1/me/orders"),
    api<Listing[]>("/v1/me/listings"),
  ]);

  return (
    <>
      <h1>My auctions</h1>
      <p className="muted">Signed in as {user.displayName} (@{user.handle}).</p>

      <section>
        <h2>Deals</h2>
        {orders.length === 0 ? (
          <p className="empty">No deals yet. Win an auction or open one.</p>
        ) : (
          <table className="table">
            <thead><tr><th>Agreement</th><th>Item</th><th>You are</th><th>With</th><th>Total</th><th>Status</th></tr></thead>
            <tbody>
              {orders.map((o) => (
                <tr key={o.id}>
                  <td><Link href={`/orders/${o.id}`}>{o.number}</Link></td>
                  <td>{o.title}</td>
                  <td>{o.role}</td>
                  <td>@{o.counterparty}</td>
                  <td className="tnum">{money(o.total, o.currency)}</td>
                  <td><span className={`pill ${o.status}`}>{orderStatusLabel[o.status]}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section>
        <h2>Bidding</h2>
        {bidding.length === 0 ? (
          <p className="empty">You haven&apos;t bid on anything yet. <Link href="/">Find something near you.</Link></p>
        ) : (
          <table className="table">
            <thead><tr><th>Item</th><th>Current</th><th>Your max</th><th>Ends</th><th>Status</th></tr></thead>
            <tbody>
              {bidding.map((b) => (
                <tr key={b.auctionId}>
                  <td><Link href={`/item/${b.listingId}`}>{b.title}</Link></td>
                  <td className="tnum">{money(b.currentPrice, b.currency)}</td>
                  <td className="tnum">{money(b.yourMax, b.currency)}</td>
                  <td>{b.auctionStatus === "live" ? <Countdown endsAt={b.endsAt} /> : "Ended"}</td>
                  <td><span className={`pill ${b.standing}`}>{standingLabel[b.standing]}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section>
        <h2>Auctions I opened</h2>
        {listings.length === 0 ? (
          <p className="empty">Nothing yet. <Link href="/sell">Open an auction</Link>.</p>
        ) : (
          <table className="table">
            <thead><tr><th>Item</th><th>Area</th><th>Start</th><th>Status</th></tr></thead>
            <tbody>
              {listings.map((l) => (
                <tr key={l.id}>
                  <td><Link href={`/item/${l.id}`}>{l.title}</Link></td>
                  <td>{l.areaName}</td>
                  <td className="tnum">{money(l.startPrice, l.currency)}</td>
                  <td>{l.status}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  );
}
