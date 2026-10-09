import { notFound, redirect } from "next/navigation";
import { api, ApiError, getSessionUser } from "@/lib/api";
import { REALTIME_PUBLIC_URL } from "@/lib/config";
import type { Order } from "@/lib/types";
import AgreementCard from "./AgreementCard";

export const dynamic = "force-dynamic";

export default async function OrderPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!(await getSessionUser())) redirect(`/signin?next=/orders/${id}`);

  let order: Order;
  try {
    order = await api<Order>(`/v1/orders/${id}`);
  } catch (e) {
    if (e instanceof ApiError && (e.status === 404 || e.status === 403)) notFound();
    throw e;
  }

  return <AgreementCard initial={order} realtimeUrl={REALTIME_PUBLIC_URL} />;
}
