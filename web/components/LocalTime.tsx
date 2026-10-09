"use client";

import { useEffect, useState } from "react";
import { dateTime } from "@/lib/format";

/**
 * A timestamp in the viewer's own time zone. Rendered after mount: the server doesn't know the
 * browser's zone, and formatting on both sides would cause a hydration mismatch.
 */
export default function LocalTime({ value }: { value: string | null | undefined }) {
  const [text, setText] = useState("");
  useEffect(() => setText(dateTime(value)), [value]);
  return <time dateTime={value ?? undefined}>{text}</time>;
}
