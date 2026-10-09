"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { callApi, readLocationCookie, writeLocationCookie } from "@/lib/client";
import type { BuyerLocation, Place } from "@/lib/types";

const RADII: (number | null)[] = [5, 10, 25, 50, 100, null];

/** Header chip "📍 Chicago · 25 km" with a picker for city/area, GPS and radius (feature design §9.1). */
export default function LocationChip({ initial, signedIn }: { initial: BuyerLocation; signedIn: boolean }) {
  const router = useRouter();
  const [location, setLocation] = useState(initial);
  const [open, setOpen] = useState(false);
  const [places, setPlaces] = useState<Place[]>([]);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("");

  useEffect(() => setLocation(readLocationCookie()), []);

  useEffect(() => {
    if (!open) return;
    const handle = setTimeout(() => {
      callApi<Place[]>(`/v1/geo/geocode?q=${encodeURIComponent(query)}`).then(setPlaces).catch(() => setPlaces([]));
    }, 200);
    return () => clearTimeout(handle);
  }, [open, query]);

  async function save(next: BuyerLocation, body: object) {
    setLocation(next);
    writeLocationCookie(next);
    if (signedIn) {
      // Persist per account; the api stores only a public-grade point.
      await callApi("/v1/me/location", {
        method: "PUT",
        body: JSON.stringify({ ...body, radiusKm: next.radiusKm, anywhere: next.radiusKm === null }),
      }).catch(() => {});
    }
    setOpen(false);
    router.refresh();
  }

  function pickPlace(place: Place) {
    save({ ...location, lat: place.lat, lng: place.lng, area: place.name }, { placeId: place.id });
  }

  function useGps() {
    if (!navigator.geolocation) return setStatus("Location isn't available in this browser.");
    setStatus("Finding you…");
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        // Keep only ~1 km precision on this device too.
        const lat = Math.round(pos.coords.latitude * 100) / 100;
        const lng = Math.round(pos.coords.longitude * 100) / 100;
        setStatus("");
        save({ ...location, lat, lng, area: "Current location" }, { lat, lng });
      },
      () => setStatus("Couldn't get your location. Pick a city instead."),
      { maximumAge: 600_000, timeout: 10_000 },
    );
  }

  function setRadius(radiusKm: number | null) {
    save({ ...location, radiusKm }, { lat: location.lat, lng: location.lng });
  }

  return (
    <div className="loc">
      <button className="chip" onClick={() => setOpen(!open)} aria-expanded={open}>
        📍 {location.area} · {location.radiusKm === null ? "Anywhere" : `${location.radiusKm} km`}
      </button>
      {open && (
        <div className="popover" role="dialog" aria-label="Choose your location">
          <button className="btn small" onClick={useGps}>Use my location</button>
          {status && <div className="small muted">{status}</div>}
          <input
            className="input"
            placeholder="City or area"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            autoFocus
          />
          <ul className="places">
            {places.map((p) => (
              <li key={p.id}>
                <button onClick={() => pickPlace(p)}>
                  {p.name} <span className="muted small">{p.kind === "area" ? p.city : ""}</span>
                </button>
              </li>
            ))}
          </ul>
          <div className="small muted">Search radius</div>
          <div className="radii">
            {RADII.map((r) => (
              <button
                key={r ?? "any"}
                className={`chip${location.radiusKm === r ? " active" : ""}`}
                onClick={() => setRadius(r)}
              >
                {r === null ? "Anywhere" : `${r} km`}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
