import Link from "next/link";
import { deliveryLabel, distance, money } from "@/lib/format";
import type { Card } from "@/lib/types";
import Countdown from "./Countdown";

export default function ListingCard({ card }: { card: Card }) {
  return (
    <Link href={`/item/${card.listingId}`} className="card">
      <div className="thumb" aria-hidden>
        {card.thumbnailUrl ? <img src={card.thumbnailUrl} alt="" /> : <span>{card.emoji}</span>}
      </div>
      <div className="card-body">
        <div className="card-title">{card.title}</div>
        <div className="price tnum">
          {money(card.price, card.currency)}
          <span className="muted small"> · {card.bidCount} {card.bidCount === 1 ? "bid" : "bids"}</span>
        </div>
        {card.buyNowPrice !== null && (
          <div className="small">or Buy Now {money(card.buyNowPrice, card.currency)}</div>
        )}
        <div className="small muted">
          📍 {card.areaName}
          {card.distanceKm !== null && <> · {distance(card.distanceKm)}</>}
        </div>
        <div className="badges">
          <span className={`badge ${card.delivery}`}>{deliveryLabel[card.delivery]}</span>
          {card.status === "live" ? (
            <span className="badge time">
              <Countdown endsAt={card.endsAt} />
            </span>
          ) : (
            <span className="badge ended">{card.status === "sold" ? "Sold" : "Ended"}</span>
          )}
        </div>
      </div>
    </Link>
  );
}

export function CardGrid({ cards, empty }: { cards: Card[]; empty?: React.ReactNode }) {
  if (cards.length === 0) return <div className="empty">{empty ?? "Nothing here yet."}</div>;
  return (
    <div className="grid">
      {cards.map((c) => (
        <ListingCard key={c.listingId} card={c} />
      ))}
    </div>
  );
}
