import Link from "next/link";
import { CardGrid } from "@/components/ListingCard";
import { api, getBuyerLocation } from "@/lib/api";
import type { Category, Feed } from "@/lib/types";

export const dynamic = "force-dynamic";

/** Home feed (feature design §9.3): Near you, Ships to you, Ending soon. */
export default async function Home() {
  const location = await getBuyerLocation();
  const radius = location.radiusKm === null ? "" : `&radiusKm=${location.radiusKm}`;

  const loaded = await Promise.all([
    api<Feed>(`/v1/feed/nearby?lat=${location.lat}&lng=${location.lng}${radius}`),
    api<Category[]>("/v1/categories"),
  ]).catch(() => null);
  if (!loaded) {
    return (
      <div className="empty">
        <h1>We can&apos;t reach the marketplace right now</h1>
        <p className="muted">The api isn&apos;t responding. Start it with <code>docker compose up -d</code> and refresh.</p>
      </div>
    );
  }

  const [feed, categories] = loaded;
  const radiusLabel = location.radiusKm === null ? "anywhere" : `within ${location.radiusKm} km`;

  return (
    <>
      <section className="hero">
        <h1>Auctions near {location.area}</h1>
        <p className="muted">Bid on things you can pick up {radiusLabel}, or have shipped from farther away.</p>
        <div className="cats">
          {categories.map((c) => (
            <Link key={c.id} href={`/search?categoryId=${c.id}`} className="chip">
              {c.emoji} {c.name}
            </Link>
          ))}
        </div>
      </section>

      <section>
        <div className="section-head">
          <h2>Near you</h2>
          <Link href="/search?sort=nearest" className="small">See all</Link>
        </div>
        <CardGrid
          cards={feed.near}
          empty={
            <>
              Nothing live {radiusLabel} yet. Try a bigger radius, or <Link href="/sell">open an auction</Link>.
            </>
          }
        />
      </section>

      {feed.shipsToYou.length > 0 && (
        <section>
          <div className="section-head">
            <h2>Ships to you</h2>
            <Link href="/search?delivery=ship" className="small">See all</Link>
          </div>
          <CardGrid cards={feed.shipsToYou} />
        </section>
      )}

      {feed.endingSoon.length > 0 && (
        <section>
          <div className="section-head">
            <h2>Ending soon</h2>
            <Link href="/search?sort=ending" className="small">See all</Link>
          </div>
          <CardGrid cards={feed.endingSoon} />
        </section>
      )}
    </>
  );
}
