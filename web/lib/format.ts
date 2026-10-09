export function money(amount: number | null | undefined, currency = "USD") {
  if (amount === null || amount === undefined) return "";
  return new Intl.NumberFormat("en-US", { style: "currency", currency }).format(amount);
}

/** Rounded distance from the API: 0 means under 1 km. Never shows decimals. */
export function distance(km: number | null | undefined) {
  if (km === null || km === undefined) return "";
  return km === 0 ? "< 1 km" : `${km} km`;
}

export const deliveryLabel: Record<string, string> = {
  pickup: "Pickup only",
  both: "Pickup or shipping",
  ship: "Shipping only",
};

export function timeLeft(endsAt: string | Date, now: number) {
  const ms = new Date(endsAt).getTime() - now;
  if (ms <= 0) return "Ended";
  const s = Math.floor(ms / 1000);
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  if (d > 0) return `${d}d ${h}h`;
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m ${sec.toString().padStart(2, "0")}s`;
}

export function dateTime(value: string | null | undefined) {
  if (!value) return "";
  return new Date(value).toLocaleString("en-US", { dateStyle: "medium", timeStyle: "short" });
}

export const orderStatusLabel: Record<string, string> = {
  agreement_pending: "Agreement pending",
  agreed: "Agreed",
  deal_paid: "Deal paid",
  received: "Received",
  cancelled: "Cancelled",
};

export const paymentLabel: Record<string, string> = {
  cash: "Cash on handover",
  bankTransfer: "Bank transfer",
  mobileQr: "Mobile / QR payment",
  other: "Other",
};
