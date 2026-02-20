"use client";
import { useState } from "react";

const API = process.env.NEXT_PUBLIC_API_BASE_URL;

type Chunk = { index: number; text: string };

export default function Home() {
  const [status, setStatus] = useState("");
  const [docId, setDocId] = useState("");
  const [chunks, setChunks] = useState<Chunk[]>([]);

  async function upload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    setStatus("Uploading...");
    setChunks([]);
    setDocId("");

    const form = new FormData();
    form.append("file", file);

    const res = await fetch(`${API}/upload`, { method: "POST", body: form });
    const data = await res.json();

    if (!res.ok) {
      setStatus(`Error: ${JSON.stringify(data)}`);
      return;
    }

    setStatus(
      `Uploaded ${data.filename} • ${data.charCount} chars • ${data.chunkCount} chunks`,
    );
    setDocId(data.docId);

    const chunksRes = await fetch(`${API}/docs/${data.docId}/chunks`);
    const chunksData = await chunksRes.json();
    setChunks(chunksData.chunks);
  }

  return (
    <main className="p-10 space-y-6">
      <div className="space-y-2">
        <h1 className="text-2xl font-bold">DocMind</h1>
        <input type="file" onChange={upload} />
        <p className="text-sm opacity-80">{status}</p>
        {docId && <p className="text-sm">docId: {docId}</p>}
      </div>

      {chunks.length > 0 && (
        <div className="space-y-3">
          <h2 className="text-xl font-semibold">Chunks</h2>
          {chunks.map((c) => (
            <div key={c.index} className="rounded border p-3">
              <div className="text-xs opacity-60 mb-2">Chunk {c.index}</div>
              <pre className="whitespace-pre-wrap text-sm">{c.text}</pre>
            </div>
          ))}
        </div>
      )}
    </main>
  );
}
