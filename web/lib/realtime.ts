"use client";

import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useEffect, useRef } from "react";

let connection: HubConnection | null = null;
let starting: Promise<HubConnection> | null = null;

/** One shared SignalR connection per tab; signed-in users also receive their user:{id} events. */
function getConnection(url: string): Promise<HubConnection> {
  if (connection?.state === HubConnectionState.Connected) return Promise.resolve(connection);
  if (starting) return starting;

  connection ??= new HubConnectionBuilder()
    .withUrl(`${url}/hubs/auctions`, {
      accessTokenFactory: async () => {
        const res = await fetch("/api/session", { cache: "no-store" });
        const { token } = (await res.json()) as { token: string | null };
        return token ?? "";
      },
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  const conn = connection;
  starting = conn
    .start()
    .then(() => conn)
    .finally(() => {
      starting = null;
    });
  return starting;
}

// Each handler declares its own payload shape; SignalR delivers parsed JSON.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Handlers = Record<string, (payload: any) => void>;

/**
 * Subscribes to realtime events while the component is mounted. Pass <c>auctionId</c> to watch an
 * auction's group. Handlers can change between renders without resubscribing.
 */
export function useRealtime(url: string, handlers: Handlers, auctionId?: string) {
  const ref = useRef(handlers);
  ref.current = handlers;

  useEffect(() => {
    let disposed = false;
    const names = Object.keys(ref.current);
    const wrappers = names.map((name) => [name, (payload: unknown) => ref.current[name]?.(payload)] as const);
    let conn: HubConnection | null = null;

    getConnection(url)
      .then(async (c) => {
        if (disposed) return;
        conn = c;
        wrappers.forEach(([name, fn]) => c.on(name, fn));
        if (auctionId) await c.invoke("Watch", auctionId);
        // Re-join the auction group after an automatic reconnect.
        c.onreconnected(() => {
          if (!disposed && auctionId) c.invoke("Watch", auctionId).catch(() => {});
        });
      })
      .catch(() => {
        /* realtime is an enhancement; pages still work by polling/refresh */
      });

    return () => {
      disposed = true;
      if (conn) {
        wrappers.forEach(([name, fn]) => conn!.off(name, fn));
        if (auctionId && conn.state === HubConnectionState.Connected) conn.invoke("Unwatch", auctionId).catch(() => {});
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [url, auctionId]);
}
