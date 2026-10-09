import Link from "next/link";
import { notFound } from "next/navigation";
import { apiOrNull, getSessionUser } from "@/lib/api";
import { REALTIME_PUBLIC_URL } from "@/lib/config";
import { deliveryLabel, money } from "@/lib/format";
import type { Auction, BidHistoryItem, Listing } from "@/lib/types";
import BidPanel from "./BidPanel";

export const dynamic = "force-dynamic";

export default async function ItemPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const listing = await apiOrNull<Listing>(`/v1/listings/${id}`);
  if (!listing) notFound();

  const [auction, user] = await Promise.all([apiOrNull<Auction>(`/v1/auctions/by-listing/${id}`), getSessionUser()]);
  const bids = auction ? (await apiOrNull<BidHistoryItem[]>(`/v1/auctions/${auction.id}/bids`)) ?? [] : [];

  return (
    <div className="item">
      <div className="item-main">
        <div className="gallery" aria-hidden>
          {listing.photoUrls.length > 0 ? (
            listing.photoUrls.map((u) => <img key={u} src={u} alt="" />)
          ) : (
            <span className="big-emoji">{listing.category.emoji}</span>
          )}
        </div>
        <h1>{listing.title}</h1>
        <p className="muted small">
          {listing.category.emoji} {listing.category.name} · {listing.condition} · sold by @{listing.sellerHandle}
        </p>

        <div className="panel">
          <h3>Location and delivery</h3>
          <p>
            📍 <strong>{listing.areaName}</strong>{" "}
            <span className="muted small">(approximate area, about 1 km; the exact address is shared after both sides accept the deal)</span>
          </p>
          <p>
            <span className={`badge ${listing.delivery}`}>{deliveryLabel[listing.delivery]}</span>
            {listing.delivery !== "pickup" && listing.shippingCost !== null && (
              <span className="small"> Shipping {money(listing.shippingCost, listing.currency)}</span>
            )}
          </p>
        </div>

        {listing.description && (
          <div className="panel">
            <h3>Description</h3>
            <p className="pre">{listing.description}</p>
          </div>
        )}

        {Object.keys(listing.specs).length > 0 && (
          <div className="panel">
            <h3>Item specifics</h3>
            <dl className="specs">
              {Object.entries(listing.specs).map(([k, v]) => (
                <div key={k}>
                  <dt>{k}</dt>
                  <dd>{v}</dd>
                </div>
              ))}
            </dl>
          </div>
        )}
      </div>

      <aside className="item-side">
        {listing.status === "draft" ? (
          <div className="panel">This listing is a draft.</div>
        ) : auction ? (
          <BidPanel
            initialAuction={auction}
            initialBids={bids}
            signedIn={user !== null}
            realtimeUrl={REALTIME_PUBLIC_URL}
            reservePrice={listing.reservePrice}
          />
        ) : (
          <AuctionStarting />
        )}
        <p className="small muted">
          No payments in the app: after you win, you and the seller accept a deal agreement and settle payment directly.{" "}
          <Link href="/search">Keep browsing</Link>
        </p>
      </aside>
    </div>
  );
}

function AuctionStarting() {
  // The auction is created from the ListingPublished event a moment after publishing.
  return (
    <div className="panel">
      <p>Starting the auction…</p>
      <meta httpEquiv="refresh" content="1" />
    </div>
  );
}
