"use client";

import Link from "next/link";
import LocalTime from "@/components/LocalTime";
import { useCallback, useState } from "react";
import { callApi, ProblemError } from "@/lib/client";
import { money, orderStatusLabel, paymentLabel } from "@/lib/format";
import { useRealtime } from "@/lib/realtime";
import type { Order } from "@/lib/types";

const STEPS = ["agreement_pending", "agreed", "deal_paid", "received"] as const;

/**
 * The deal agreement (feature design §6): fixed price, editable handover/payment terms, both parties
 * accept, both confirm payment, buyer marks received. No money moves through the app.
 */
export default function AgreementCard({ initial, realtimeUrl }: { initial: Order; realtimeUrl: string }) {
  const [order, setOrder] = useState(initial);
  const [editing, setEditing] = useState(false);
  const [terms, setTerms] = useState({
    handover: initial.agreement.handover,
    handoverDate: initial.agreement.handoverDate,
    paymentMethod: initial.agreement.paymentMethod,
    notes: initial.agreement.notes,
  });
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const a = order.agreement;
  const c = order.currency;
  const isBuyer = order.role === "buyer";
  const myAccepted = isBuyer ? a.buyerAcceptedAt : a.sellerAcceptedAt;
  const theirAccepted = isBuyer ? a.sellerAcceptedAt : a.buyerAcceptedAt;
  const myConfirmed = isBuyer ? a.buyerPaidAt : a.sellerReceivedAt;
  const theirConfirmed = isBuyer ? a.sellerReceivedAt : a.buyerPaidAt;
  const counterparty = isBuyer ? order.sellerHandle : order.buyerHandle;

  const refresh = useCallback(async () => {
    const fresh = await callApi<Order>(`/v1/orders/${order.id}`);
    setOrder(fresh);
    if (!editing)
      setTerms({ handover: fresh.agreement.handover, handoverDate: fresh.agreement.handoverDate, paymentMethod: fresh.agreement.paymentMethod, notes: fresh.agreement.notes });
  }, [order.id, editing]);

  useRealtime(realtimeUrl, {
    "order.updated": (e: { orderId: string }) => {
      if (e.orderId === order.id) refresh();
    },
  });

  async function act(path: string, body?: object) {
    setBusy(true);
    setError("");
    try {
      const updated = await callApi<Order>(`/v1/orders/${order.id}${path}`, {
        method: path === "/agreement" ? "PATCH" : "POST",
        body: body ? JSON.stringify(body) : undefined,
      });
      setOrder(updated);
      setEditing(false);
    } catch (e) {
      setError(e instanceof ProblemError ? e.message : "Something went wrong.");
      if (e instanceof ProblemError && e.code === "stale_version") await refresh();
    } finally {
      setBusy(false);
    }
  }

  const stepIndex = order.status === "cancelled" ? -1 : STEPS.indexOf(order.status);

  return (
    <div className="agreement-page">
      <p className="small"><Link href={`/item/${order.listingId}`}>← {order.title}</Link></p>
      <h1>
        Deal agreement {a.number} <span className={`pill ${order.status}`}>{orderStatusLabel[order.status]}</span>
      </h1>
      <p className="muted">
        You are the <strong>{order.role}</strong>. {isBuyer ? "Seller" : "Buyer"}: @{counterparty}.
      </p>

      <ol className="steps">
        {STEPS.map((s, i) => (
          <li key={s} className={i < stepIndex ? "done" : i === stepIndex ? "current" : ""}>{orderStatusLabel[s]}</li>
        ))}
      </ol>

      <div className="agreement">
        <div className="terms">
          <div><span className="muted small">Agreed price (locked)</span><div className="big-price tnum">{money(a.agreedPrice, c)}</div></div>
          {order.shippingCost !== null && <div><span className="muted small">Shipping</span><div className="tnum">{money(order.shippingCost, c)}</div></div>}
          <div><span className="muted small">Total</span><div className="tnum"><strong>{money(order.total, c)}</strong></div></div>
          <div><span className="muted small">Handover</span><div>{a.handover === "pickup" ? `Pickup in ${a.areaName}` : "Shipping"}</div></div>
          <div><span className="muted small">Handover date</span><div>{a.handoverDate}</div></div>
          <div><span className="muted small">Payment</span><div>{paymentLabel[a.paymentMethod]} <span className="muted small">(directly to the seller)</span></div></div>
          {a.notes && <div className="wide"><span className="muted small">Notes</span><div className="pre">{a.notes}</div></div>}
          <div className="wide">
            <span className="muted small">Pickup address</span>
            <div>
              {a.pickupAddress ?? (a.handover === "pickup" ? "Shared here once you both accept the agreement." : "Not needed for shipping.")}
            </div>
          </div>
        </div>

        <div className="acceptance">
          <span className={`badge ${a.buyerAcceptedAt ? "good" : ""}`}>Buyer {a.buyerAcceptedAt ? "accepted ✓" : "hasn't accepted"}</span>
          <span className={`badge ${a.sellerAcceptedAt ? "good" : ""}`}>Seller {a.sellerAcceptedAt ? "accepted ✓" : "hasn't accepted"}</span>
          <span className="muted small">Version {a.version}</span>
        </div>

        {order.status === "agreement_pending" && (
          <>
            {editing ? (
              <form
                className="form"
                onSubmit={(e) => {
                  e.preventDefault();
                  act("/agreement", { version: a.version, ...terms });
                }}
              >
                <label>
                  Handover
                  <select className="input" value={terms.handover} onChange={(e) => setTerms({ ...terms, handover: e.target.value as typeof terms.handover })}>
                    {order.delivery !== "ship" && <option value="pickup">Pickup</option>}
                    {order.delivery !== "pickup" && <option value="shipping">Shipping</option>}
                  </select>
                </label>
                <label>
                  Handover date
                  <input className="input" type="date" value={terms.handoverDate} onChange={(e) => setTerms({ ...terms, handoverDate: e.target.value })} />
                </label>
                <label>
                  Payment method
                  <select className="input" value={terms.paymentMethod} onChange={(e) => setTerms({ ...terms, paymentMethod: e.target.value as typeof terms.paymentMethod })}>
                    {Object.entries(paymentLabel).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
                  </select>
                </label>
                <label>
                  Notes
                  <textarea className="input" rows={2} maxLength={1000} value={terms.notes} onChange={(e) => setTerms({ ...terms, notes: e.target.value })} />
                </label>
                <p className="small muted">Changing the terms resets both acceptances; the price can&apos;t change.</p>
                <div className="row">
                  <button className="btn primary" disabled={busy}>Save terms</button>
                  <button type="button" className="btn" onClick={() => setEditing(false)}>Cancel</button>
                </div>
              </form>
            ) : (
              <div className="row">
                {!myAccepted && (
                  <button className="btn primary" disabled={busy} onClick={() => act("/agreement/accept", { version: a.version })}>
                    Accept agreement (v{a.version})
                  </button>
                )}
                <button className="btn" onClick={() => setEditing(true)}>Change terms</button>
              </div>
            )}
            <p className="small muted">
              {myAccepted ? (
                theirAccepted ? "" : `You accepted. Waiting for @${counterparty}.`
              ) : (
                <>Accept by <LocalTime value={order.acceptBy} />. Accepting records your agreement to these terms.</>
              )}
            </p>
          </>
        )}

        {(order.status === "agreed" || order.status === "deal_paid") && (
          <div className="confirmations">
            <p>
              Pay as agreed, outside the app. Then both of you confirm: the buyer that they paid, the seller that they
              received it.
            </p>
            <div className="acceptance">
              <span className={`badge ${a.buyerPaidAt ? "good" : ""}`}>Buyer paid {a.buyerPaidAt ? "✓" : "—"}</span>
              <span className={`badge ${a.sellerReceivedAt ? "good" : ""}`}>Seller received payment {a.sellerReceivedAt ? "✓" : "—"}</span>
            </div>
            {!myConfirmed && (
              <button className="btn primary" disabled={busy} onClick={() => act("/confirm-paid")}>
                {isBuyer ? "I paid the seller" : "I received the payment"}
              </button>
            )}
            {myConfirmed && !theirConfirmed && <p className="small muted">Waiting for @{counterparty} to confirm.</p>}
          </div>
        )}

        {order.status === "deal_paid" && isBuyer && (
          <button className="btn primary" disabled={busy} onClick={() => act("/mark-received")}>I received the item</button>
        )}
        {order.status === "deal_paid" && !isBuyer && <p className="small muted">Waiting for the buyer to mark the item as received.</p>}
        {order.status === "received" && <p className="success">Deal complete. Thanks for trading locally!</p>}
        {order.status === "cancelled" && <p className="error">This deal was cancelled because it wasn&apos;t completed in time.</p>}
        {order.canCancel && (
          <button className="btn danger" disabled={busy} onClick={() => confirm("Cancel this deal? The buyer gets a strike.") && act("/cancel")}>
            Cancel the deal (not completed in time)
          </button>
        )}
        {error && <p className="error">{error}</p>}
      </div>

      <h3>History</h3>
      <ol className="history">
        {order.history.map((h, i) => (
          <li key={i}>
            <span>{h.action.replaceAll("_", " ")}{h.by && <span className="muted small"> by {h.by}</span>}{h.detail && <span className="muted small"> ({h.detail})</span>}</span>
            <span className="muted small">v{h.version}</span>
            <span className="muted small"><LocalTime value={h.at} /></span>
          </li>
        ))}
      </ol>
    </div>
  );
}
