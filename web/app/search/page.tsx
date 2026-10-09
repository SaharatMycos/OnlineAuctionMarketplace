import { CardGrid } from "@/components/ListingCard";
import { api, getBuyerLocation } from "@/lib/api";
import type { Card, Category } from "@/lib/types";

export const dynamic = "force-dynamic";

type Params = Record<string, string | string[] | undefined>;

const SORTS = [
  ["nearest", "Nearest first"],
  ["ending", "Ending soonest"],
  ["price_asc", "Price: low to high"],
  ["price_desc", "Price: high to low"],
  ["bids", "Most bids"],
  ["newest", "Newly listed"],
];

/** Search with distance radius, delivery, category and sort (feature design §9.4). Plain GET form: works without JS. */
export default async function SearchPage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const get = (k: string) => (typeof params[k] === "string" ? (params[k] as string) : "");
  const location = await getBuyerLocation();

  const radius = get("radiusKm") || (location.radiusKm === null ? "" : String(location.radiusKm));
  const query = new URLSearchParams({ lat: String(location.lat), lng: String(location.lng) });
  for (const key of ["q", "categoryId", "delivery", "sort"]) if (get(key)) query.set(key, get(key));
  if (radius) query.set("radiusKm", radius);
  if (get("includeShipping") === "false") query.set("includeShipping", "false");

  const [results, categories] = await Promise.all([
    api<Card[]>(`/v1/search?${query}`),
    api<Category[]>("/v1/categories"),
  ]);

  return (
    <>
      <h1>{get("q") ? `Results for “${get("q")}”` : "Browse auctions"}</h1>
      <form className="filters">
        <input name="q" className="input" placeholder="Keywords" defaultValue={get("q")} />
        <select name="categoryId" className="input" defaultValue={get("categoryId")}>
          <option value="">All categories</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>{c.emoji} {c.name}</option>
          ))}
        </select>
        <select name="radiusKm" className="input" defaultValue={radius}>
          {["5", "10", "25", "50", "100"].map((r) => (
            <option key={r} value={r}>Within {r} km</option>
          ))}
          <option value="">Anywhere</option>
        </select>
        <select name="delivery" className="input" defaultValue={get("delivery")}>
          <option value="">Pickup or shipping</option>
          <option value="pickup">Pickup</option>
          <option value="ship">Shipping</option>
        </select>
        <select name="sort" className="input" defaultValue={get("sort") || (get("q") ? "ending" : "nearest")}>
          {SORTS.map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <label className="small check">
          <input type="checkbox" name="includeShipping" value="false" defaultChecked={get("includeShipping") === "false"} />
          Only items in my radius
        </label>
        <button className="btn">Search</button>
      </form>
      <p className="small muted">
        {results.length} result{results.length === 1 ? "" : "s"} near {location.area}. Distances are approximate; exact
        addresses are shared only after a deal is agreed.
      </p>
      <CardGrid cards={results} empty="No auctions match. Try a bigger radius or fewer filters." />
    </>
  );
}
