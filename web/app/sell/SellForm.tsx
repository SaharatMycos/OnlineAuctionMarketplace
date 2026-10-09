"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { callApi, ProblemError } from "@/lib/client";
import type { Category, Delivery, Listing, Place } from "@/lib/types";

const DURATIONS = [
  [5, "5 minutes (demo)"],
  [60, "1 hour (demo)"],
  [1440, "1 day"],
  [4320, "3 days"],
  [7200, "5 days"],
  [10080, "7 days"],
  [14400, "10 days"],
] as const;

const CONDITIONS = ["New", "Used, like new", "Used, excellent", "Used, good", "Used, fair", "For parts"];

/** Listing creation (feature design §2.1) including item location and delivery (§9.2). */
export default function SellForm({ categories, places }: { categories: Category[]; places: Place[] }) {
  const router = useRouter();
  const areas = places.filter((p) => p.kind === "area").concat(places.filter((p) => p.kind === "city"));
  const [form, setForm] = useState({
    categoryId: categories[0]?.id ?? 1,
    title: "",
    description: "",
    condition: "Used, good",
    photoUrls: "",
    specs: "",
    placeId: areas[0]?.id ?? 1,
    address: "",
    delivery: "pickup" as Delivery,
    shippingCost: "",
    startPrice: "",
    reservePrice: "",
    buyNowPrice: "",
    durationMinutes: 1440,
  });
  const [gps, setGps] = useState<{ lat: number; lng: number } | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm({ ...form, [key]: e.target.value });

  function useExactLocation() {
    navigator.geolocation?.getCurrentPosition(
      (pos) => setGps({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
      () => setError("Couldn't get your location; pick an area instead."),
    );
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    const num = (v: string) => (v.trim() === "" ? null : Number(v));
    const specs = Object.fromEntries(
      form.specs
        .split("\n")
        .map((line) => line.split(":"))
        .filter((parts) => parts.length >= 2 && parts[0].trim())
        .map(([k, ...v]) => [k.trim(), v.join(":").trim()]),
    );
    try {
      const listing = await callApi<Listing>("/v1/listings", {
        method: "POST",
        body: JSON.stringify({
          listing: {
            categoryId: Number(form.categoryId),
            title: form.title,
            description: form.description,
            condition: form.condition,
            photoUrls: form.photoUrls.split(/\s+/).filter(Boolean),
            specs,
            delivery: form.delivery,
            shippingCost: form.delivery === "pickup" ? null : num(form.shippingCost) ?? 0,
            startPrice: num(form.startPrice),
            reservePrice: num(form.reservePrice),
            buyNowPrice: num(form.buyNowPrice),
            durationMinutes: Number(form.durationMinutes),
          },
          location: gps
            ? { lat: gps.lat, lng: gps.lng, address: form.address }
            : { placeId: Number(form.placeId), address: form.address },
          publish: true,
        }),
      });
      router.push(`/item/${listing.id}`);
    } catch (e) {
      setError(e instanceof ProblemError ? e.message : "Couldn't publish the listing.");
      setBusy(false);
    }
  }

  return (
    <form className="form" onSubmit={submit}>
      <fieldset>
        <legend>Item</legend>
        <label>
          Category
          <select className="input" value={form.categoryId} onChange={set("categoryId")}>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>{c.emoji} {c.name}</option>
            ))}
          </select>
        </label>
        <label>
          Title
          <input className="input" required minLength={5} maxLength={80} value={form.title} onChange={set("title")} placeholder="Trek FX 3 Disc hybrid bike (2022), size M" />
        </label>
        <label>
          Condition
          <select className="input" value={form.condition} onChange={set("condition")}>
            {CONDITIONS.map((c) => <option key={c}>{c}</option>)}
          </select>
        </label>
        <label>
          Description
          <textarea className="input" rows={4} maxLength={4000} value={form.description} onChange={set("description")} />
        </label>
        <label>
          Item specifics <span className="muted small">(one per line, e.g. “Brand: Trek”)</span>
          <textarea className="input" rows={3} value={form.specs} onChange={set("specs")} />
        </label>
        <label>
          Photo URLs <span className="muted small">(optional, space-separated; uploads come later)</span>
          <input className="input" value={form.photoUrls} onChange={set("photoUrls")} />
        </label>
      </fieldset>

      <fieldset>
        <legend>Location and delivery</legend>
        {gps ? (
          <p className="small">
            Using your current location (private).{" "}
            <button type="button" className="link" onClick={() => setGps(null)}>Pick an area instead</button>
          </p>
        ) : (
          <label>
            Area
            <select className="input" value={form.placeId} onChange={set("placeId")}>
              {areas.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
            <button type="button" className="link small" onClick={useExactLocation}>Use my exact location instead</button>
          </label>
        )}
        <label>
          Pickup address <span className="muted small">(private: shown only to the winning buyer after you both accept)</span>
          <input className="input" required value={form.address} onChange={set("address")} placeholder="1847 N Damen Ave" />
        </label>
        <div className="radio-row" role="radiogroup" aria-label="Delivery">
          {(["pickup", "both", "ship"] as Delivery[]).map((d) => (
            <label key={d} className="check">
              <input type="radio" name="delivery" checked={form.delivery === d} onChange={() => setForm({ ...form, delivery: d })} />
              {d === "pickup" ? "Pickup only" : d === "both" ? "Pickup or shipping" : "Shipping only"}
            </label>
          ))}
        </div>
        {form.delivery !== "pickup" && (
          <label>
            Shipping cost
            <input className="input" inputMode="decimal" value={form.shippingCost} onChange={set("shippingCost")} placeholder="12.00" />
          </label>
        )}
      </fieldset>

      <fieldset>
        <legend>Auction</legend>
        <div className="cols">
          <label>
            Starting bid
            <input className="input" required inputMode="decimal" value={form.startPrice} onChange={set("startPrice")} placeholder="150" />
          </label>
          <label>
            Reserve <span className="muted small">(hidden, optional)</span>
            <input className="input" inputMode="decimal" value={form.reservePrice} onChange={set("reservePrice")} />
          </label>
          <label>
            Buy Now <span className="muted small">(optional)</span>
            <input className="input" inputMode="decimal" value={form.buyNowPrice} onChange={set("buyNowPrice")} />
          </label>
        </div>
        <label>
          Duration
          <select className="input" value={form.durationMinutes} onChange={set("durationMinutes")}>
            {DURATIONS.map(([v, label]) => (
              <option key={v} value={v}>{label}</option>
            ))}
          </select>
        </label>
        <p className="small muted">
          Bids in the last 2 minutes extend the auction by 2 minutes. When it ends, you and the winner get a deal
          agreement to accept; payment happens between you.
        </p>
      </fieldset>

      {error && <p className="error">{error}</p>}
      <button className="btn primary wide" disabled={busy}>{busy ? "Opening…" : "Open auction"}</button>
    </form>
  );
}
