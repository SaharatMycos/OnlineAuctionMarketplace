import { redirect } from "next/navigation";
import { api, getSessionUser } from "@/lib/api";
import type { Category, Place } from "@/lib/types";
import SellForm from "./SellForm";

export const dynamic = "force-dynamic";

export default async function SellPage() {
  if (!(await getSessionUser())) redirect("/signin?next=/sell");
  const [categories, places] = await Promise.all([api<Category[]>("/v1/categories"), api<Place[]>("/v1/geo/geocode?q=")]);

  return (
    <div className="narrow">
      <h1>Open an auction</h1>
      <p className="muted">
        Buyers near you see it first. Your exact address stays private: buyers see the area and an approximate
        distance, and only the winning buyer gets the address once you both accept the deal agreement.
      </p>
      <SellForm categories={categories} places={places} />
    </div>
  );
}
