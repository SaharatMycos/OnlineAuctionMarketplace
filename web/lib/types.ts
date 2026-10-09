// Shapes returned by the api (camelCase JSON). Keep in sync with the C# view records.

export type Delivery = "pickup" | "both" | "ship";

export interface Card {
  listingId: string;
  auctionId: string | null;
  title: string;
  emoji: string;
  thumbnailUrl: string | null;
  categoryName: string;
  condition: string;
  delivery: Delivery;
  shippingCost: number | null;
  areaName: string;
  /** Whole km from the fuzzed public point; 0 means under 1 km. */
  distanceKm: number | null;
  isLocal: boolean | null;
  currency: string;
  price: number;
  bidCount: number;
  buyNowPrice: number | null;
  endsAt: string;
  status: "live" | "sold" | "unsold";
  sellerHandle: string;
}

export interface Feed {
  near: Card[];
  shipsToYou: Card[];
  endingSoon: Card[];
}

export interface Category {
  id: number;
  name: string;
  slug: string;
  emoji: string;
}

export interface Listing {
  id: string;
  sellerHandle: string;
  isMine: boolean;
  category: Category;
  title: string;
  description: string;
  condition: string;
  photoUrls: string[];
  specs: Record<string, string>;
  delivery: Delivery;
  shippingCost: number | null;
  areaName: string;
  currency: string;
  startPrice: number;
  reservePrice: number | null;
  buyNowPrice: number | null;
  durationMinutes: number;
  status: "draft" | "published" | "ended";
  publishedAt: string | null;
  endsAt: string | null;
}

export interface You {
  isSeller: boolean;
  hasBid: boolean;
  isLeader: boolean;
  yourMax: number | null;
  won: boolean;
}

export interface Auction {
  id: string;
  listingId: string;
  title: string;
  sellerHandle: string;
  currency: string;
  status: "live" | "sold" | "unsold";
  startPrice: number;
  currentPrice: number;
  minNextBid: number;
  bidCount: number;
  buyNowPrice: number | null;
  hasReserve: boolean;
  reserveMet: boolean;
  startsAt: string;
  endsAt: string;
  serverTime: string;
  leader: string | null;
  finalPrice: number | null;
  seq: number;
  you: You | null;
}

export interface BidHistoryItem {
  seq: number;
  amount: number;
  bidder: string;
  kind: "manual" | "proxy" | "buynow";
  at: string;
}

export interface MyBidding {
  auctionId: string;
  listingId: string;
  title: string;
  currency: string;
  currentPrice: number;
  yourMax: number;
  bidCount: number;
  endsAt: string;
  auctionStatus: string;
  standing: "winning" | "outbid" | "won" | "lost";
}

export type OrderStatus = "agreement_pending" | "agreed" | "deal_paid" | "received" | "cancelled";

export interface Agreement {
  number: string;
  version: number;
  agreedPrice: number;
  handover: "pickup" | "shipping";
  handoverDate: string;
  paymentMethod: "cash" | "bankTransfer" | "mobileQr" | "other";
  notes: string;
  areaName: string;
  pickupAddress: string | null;
  buyerAcceptedAt: string | null;
  sellerAcceptedAt: string | null;
  buyerPaidAt: string | null;
  sellerReceivedAt: string | null;
  receivedAt: string | null;
}

export interface Order {
  id: string;
  auctionId: string;
  listingId: string;
  title: string;
  status: OrderStatus;
  role: "buyer" | "seller";
  buyerHandle: string;
  sellerHandle: string;
  currency: string;
  delivery: Delivery;
  itemPrice: number;
  shippingCost: number | null;
  total: number;
  acceptBy: string;
  createdAt: string;
  completedAt: string | null;
  canCancel: boolean;
  agreement: Agreement;
  history: { action: string; by: string | null; version: number; detail: string | null; at: string }[];
}

export interface OrderSummary {
  id: string;
  number: string;
  listingId: string;
  title: string;
  status: OrderStatus;
  role: "buyer" | "seller";
  counterparty: string;
  currency: string;
  total: number;
  createdAt: string;
}

export interface Place {
  id: number;
  name: string;
  city: string;
  kind: "city" | "area";
  lat: number;
  lng: number;
}

export interface Me {
  id: string;
  email: string;
  displayName: string;
  handle: string;
}

/** Buyer location kept in the mp_loc cookie. radiusKm null = anywhere. */
export interface BuyerLocation {
  lat: number;
  lng: number;
  area: string;
  radiusKm: number | null;
}
