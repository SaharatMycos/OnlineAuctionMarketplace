"use client";

import { useEffect, useRef, useState } from "react";
import { callApi } from "@/lib/client";
import { MAX_PHOTO_BYTES } from "@/lib/config";

interface UploadResponse { id: string; url: string; width: number; height: number }
interface Photo { key: string; file: File; preview: string; id?: string; status: "pending" | "uploading" | "ready" | "failed" | "removing"; error?: string }

export default function PhotoUploader({ disabled, onChange }: {
  disabled: boolean;
  onChange: (ids: string[], blocked: boolean) => void;
}) {
  const [photos, setPhotos] = useState<Photo[]>([]);
  const [error, setError] = useState("");
  const [dragging, setDragging] = useState(false);
  const uploading = useRef(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const previews = useRef(new Set<string>());
  const latest = useRef(photos);
  latest.current = photos;

  useEffect(() => () => { for (const url of previews.current) URL.revokeObjectURL(url); }, []);
  useEffect(() => {
    onChange(photos.flatMap((p) => p.id ? [p.id] : []), photos.some((p) => p.status !== "ready"));
  }, [photos, onChange]);

  useEffect(() => {
    const next = photos.find((p) => p.status === "pending");
    if (!next || uploading.current) return;
    uploading.current = true;
    setPhotos((ps) => ps.map((p) => p.key === next.key ? { ...p, status: "uploading" } : p));
    const body = new FormData();
    body.append("file", next.file);
    void callApi<UploadResponse>("/v1/listing-photos", { method: "POST", body })
      .then(async (response) => {
        if (!latest.current.some((p) => p.key === next.key)) {
          await callApi("/v1/listing-photos/" + response.id, { method: "DELETE" }).catch(() => {});
          return;
        }
        setPhotos((ps) => ps.map((p) => p.key === next.key ? { ...p, id: response.id, status: "ready" } : p));
      })
      .catch((e: unknown) => setPhotos((ps) => ps.map((p) => p.key === next.key
        ? { ...p, status: "failed", error: e instanceof Error ? e.message : "Upload failed." } : p)))
      .finally(() => { uploading.current = false; setPhotos((ps) => [...ps]); });
  }, [photos]);

  function select(files: FileList | null) {
    if (disabled || !files) return;
    setError("");
    const incoming: Photo[] = [];
    for (const file of Array.from(files)) {
      if (photos.length + incoming.length >= 24) { setError("You can add up to 24 photos."); break; }
      if (!["image/jpeg", "image/png", "image/webp"].includes(file.type) || file.size === 0 || file.size > MAX_PHOTO_BYTES) {
        setError("Choose JPEG, PNG or WebP files up to 10 MiB each."); continue;
      }
      const preview = URL.createObjectURL(file);
      previews.current.add(preview);
      incoming.push({ key: crypto.randomUUID(), file, preview, status: "pending" });
    }
    setPhotos((ps) => [...ps, ...incoming]);
  }

  async function remove(photo: Photo) {
    if (photo.id) {
      setPhotos((ps) => ps.map((p) => p.key === photo.key ? { ...p, status: "removing" } : p));
      try { await callApi("/v1/listing-photos/" + photo.id, { method: "DELETE" }); }
      catch { setPhotos((ps) => ps.map((p) => p.key === photo.key ? { ...p, status: "ready" } : p)); setError("Couldn't remove the photo. Try again."); return; }
    }
    URL.revokeObjectURL(photo.preview);
    previews.current.delete(photo.preview);
    setPhotos((ps) => ps.filter((p) => p.key !== photo.key));
  }

  function move(index: number, direction: number) {
    setPhotos((ps) => { const copy = [...ps]; [copy[index], copy[index + direction]] = [copy[index + direction], copy[index]]; return copy; });
  }

  return (
    <div>
      <div className={`photo-dropzone${dragging ? " dragging" : ""}`}
        onDragOver={(e) => { e.preventDefault(); if (!disabled && photos.length < 24) setDragging(true); }}
        onDragLeave={(e) => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setDragging(false); }}
        onDrop={(e) => { e.preventDefault(); setDragging(false); select(e.dataTransfer.files); }}>
        <span className="photo-dropzone-icon" aria-hidden="true">＋</span>
        <p><strong>Show buyers what you&apos;re selling</strong></p>
        <button type="button" className="btn primary" aria-describedby="listing-photos-help" disabled={disabled || photos.length >= 24} onClick={() => fileInput.current?.click()}>
          Choose photos
        </button>
        <p className="small muted">or drag and drop them here</p>
        <p className="small muted" id="listing-photos-help">Optional, up to 24 photos. JPEG, PNG or WebP, up to 10 MiB each. Location metadata is removed.</p>
      </div>
      <p className="small muted">{photos.length}/24 photos · The first photo is your cover. Use the arrows to reorder.</p>
      <input ref={fileInput} id="listing-photos" aria-label="Upload photos" hidden type="file" multiple accept="image/jpeg,image/png,image/webp" disabled={disabled || photos.length >= 24}
        onChange={(e) => { select(e.target.files); e.target.value = ""; }} />
      {error && <p className="error" role="alert">{error}</p>}
      <ol aria-label="Listing photos" className="photo-grid">
        {photos.map((photo, index) => (
          <li key={photo.key} className="panel photo-preview">
            <img src={photo.preview} alt={"Photo " + (index + 1) + ": " + photo.file.name} width={240} height={180} />
            <p className="small" aria-live="polite">{index === 0 ? "Cover · " : ""}{photo.status === "ready" ? "Uploaded" : photo.status === "failed" ? photo.error : photo.status === "uploading" ? "Uploading…" : photo.status === "removing" ? "Removing…" : "Waiting…"}</p>
            <div className="radio-row">
              <button type="button" className="btn" disabled={disabled || index === 0} aria-label={"Move photo " + (index + 1) + " earlier"} onClick={() => move(index, -1)}>←</button>
              <button type="button" className="btn" disabled={disabled || index === photos.length - 1} aria-label={"Move photo " + (index + 1) + " later"} onClick={() => move(index, 1)}>→</button>
              {photo.status === "failed" && <button type="button" className="btn" disabled={disabled} onClick={() => setPhotos((ps) => ps.map((p) => p.key === photo.key ? { ...p, status: "pending", error: undefined } : p))}>Retry</button>}
              <button type="button" className="btn" disabled={disabled || photo.status === "removing"} onClick={() => void remove(photo)}>Remove</button>
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
