import type { Metadata } from "next";
import Link from "next/link";
import LiveNotifications from "@/components/LiveNotifications";
import LocationChip from "@/components/LocationChip";
import UserMenu from "@/components/UserMenu";
import { getBuyerLocation, getSessionUser } from "@/lib/api";
import { REALTIME_PUBLIC_URL } from "@/lib/config";
import "./globals.css";

export const metadata: Metadata = {
  title: "Bidly",
  description: "Location-first auction marketplace",
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const [user, location] = await Promise.all([getSessionUser(), getBuyerLocation()]);

  return (
    <html lang="en">
      <body>
        <header className="topbar">
          <div className="wrap topbar-inner">
            <Link href="/" className="brand">Bidly</Link>
            <LocationChip initial={location} signedIn={user !== null} />
            <form action="/search" className="search">
              <input name="q" className="input" placeholder="Search near you" aria-label="Search" />
            </form>
            <nav className="nav">
              <Link href="/sell">Open auction</Link>
              {user && <Link href="/me">My auctions</Link>}
              <UserMenu user={user} />
            </nav>
          </div>
        </header>
        <main>{children}</main>
        {user && <LiveNotifications realtimeUrl={REALTIME_PUBLIC_URL} />}
        <footer className="wrap small muted footer">
          Bidly is a placeholder brand. Payments happen between buyer and seller; the platform records the deal agreement.
        </footer>
      </body>
    </html>
  );
}
