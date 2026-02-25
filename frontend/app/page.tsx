"use client";
import { useEffect, useMemo, useState } from "react";

const API = process.env.NEXT_PUBLIC_API_BASE_URL;

type Chunk = { index: number; text: string };

type DocSummary = {
  id: string;
  filename: string;
  createdUtc: string;
  chunkCount: number;
};

type AskSource = {
  chunkIndex: number;
  score: number;
  preview: string;
};

export default function Home() {
  const [status, setStatus] = useState("");
  const [docId, setDocId] = useState("");
  const [docName, setDocName] = useState("");
  const [chunks, setChunks] = useState<Chunk[]>([]);

  const [docs, setDocs] = useState<DocSummary[]>([]);
  const [loadingDocs, setLoadingDocs] = useState(false);

  const [question, setQuestion] = useState("");
  const [asking, setAsking] = useState(false);
  const [answer, setAnswer] = useState("");
  const [sources, setSources] = useState<AskSource[]>([]);

  const seedQuestions = useMemo(
    () => [
      "Summarize this document in 5 bullets.",
      "What are the key technologies/skills mentioned?",
      "What company/role is referenced, if any?",
    ],
    [],
  );

  async function refreshDocs() {
    setLoadingDocs(true);
    try {
      const res = await fetch(`${API}/docs`, { cache: "no-store" });
      const data = await res.json();
      if (res.ok) setDocs(data);
    } finally {
      setLoadingDocs(false);
    }
  }

  async function loadDoc(id: string, filename?: string) {
    setStatus("");
    setAnswer("");
    setSources([]);
    setQuestion("");
    setChunks([]);

    setDocId(id);
    setDocName(filename ?? "");

    const chunksRes = await fetch(`${API}/docs/${id}/chunks`, {
      cache: "no-store",
    });
    const chunksData = await chunksRes.json();
    if (!chunksRes.ok) {
      setStatus(`Error loading chunks: ${JSON.stringify(chunksData)}`);
      return;
    }
    setChunks(chunksData.chunks);
  }

  async function upload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    setStatus("Uploading...");
    setChunks([]);
    setDocId("");
    setDocName("");
    setAnswer("");
    setSources([]);
    setQuestion("");

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

    await loadDoc(data.docId, data.filename);
    await refreshDocs();
  }

  async function ask() {
    if (!docId) {
      setStatus("Upload or select a document first.");
      return;
    }
    if (!question.trim()) return;

    setAsking(true);
    setStatus("");
    setAnswer("");
    setSources([]);

    try {
      const res = await fetch(`${API}/docs/${docId}/ask`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ question, topK: 5 }),
      });
      const data = await res.json();

      if (!res.ok) {
        setStatus(`Ask error: ${JSON.stringify(data)}`);
        return;
      }

      setAnswer(data.answer ?? "");
      setSources(data.sources ?? []);
    } finally {
      setAsking(false);
    }
  }

  useEffect(() => {
    if (!API) {
      setStatus("Missing NEXT_PUBLIC_API_BASE_URL");
      return;
    }
    refreshDocs();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <main className="p-10 space-y-8 w-full mx-auto"> 
      {/* Header */}
      <div className="space-y-2">
        <h1 className="text-3xl font-bold">DocMind</h1>
        <p className="text-sm opacity-80">
          Upload a PDF/TXT, then ask questions grounded in the document.
        </p>
      </div>

      {/* Controls */}
      <div className="grid gap-6 md:grid-cols-2">
        {/* Upload */}
        <div className="rounded border p-4 space-y-3">
          <div className="font-semibold">1) Upload</div>
          <input type="file" onChange={upload} />
          {status && <p className="text-sm opacity-80">{status}</p>}
          {docId && (
            <div className="text-sm">
              <div className="opacity-70">Selected doc</div>
              <div className="font-mono break-all">{docId}</div>
              {docName && (
                <div className="opacity-80 truncate" title={docName}>
                  {docName}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Docs list */}
        <div className="rounded border p-4 space-y-3">
          <div className="flex items-center justify-between">
            <div className="font-semibold">Recent docs</div>
            <button
              className="text-sm underline opacity-80"
              onClick={refreshDocs}
              disabled={loadingDocs}
            >
              {loadingDocs ? "Refreshing..." : "Refresh"}
            </button>
          </div>

          {docs.length === 0 ? (
            <p className="text-sm opacity-70">
              No docs yet. Upload something to begin.
            </p>
          ) : (
            <div className="space-y-2">
              {docs.slice(0, 8).map((d) => (
                <button
                  key={d.id}
                  className={`w-full text-left rounded border px-3 py-2 hover:bg-black/5 min-w-0 ${
                    d.id === docId ? "border-black" : "border-black/20"
                  }`}
                  onClick={() => loadDoc(d.id, d.filename)}
                >
                  <div className="text-sm font-medium truncate" title={d.filename}>
                    {d.filename}
                  </div>
                  <div className="text-xs opacity-70">
                    {new Date(d.createdUtc).toLocaleString()} • {d.chunkCount}{" "}
                    chunks
                  </div>
                  <div className="text-xs font-mono opacity-60">
                    {d.id.slice(0, 6)}…{d.id.slice(-6)}
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Ask */}
      <div className="rounded border p-4 space-y-4">
        <div className="font-semibold">2) Ask</div>

        <div className="flex flex-wrap gap-2">
          {seedQuestions.map((q) => (
            <button
              key={q}
              className="text-xs rounded border px-2 py-1 hover:bg-black/5"
              onClick={() => setQuestion(q)}
            >
              {q}
            </button>
          ))}
        </div>

        <div className="flex gap-2">
          <input
            className="flex-1 rounded border px-3 py-2"
            placeholder="Ask a question about the selected document..."
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") ask();
            }}
          />
          <button
            className="rounded border px-4 py-2 hover:bg-black/5 disabled:opacity-50"
            onClick={ask}
            disabled={asking || !question.trim() || !docId}
          >
            {asking ? "Asking..." : "Ask"}
          </button>
        </div>

        {/* Answer */}
        {answer && (
          <div className="space-y-2">
            <div className="text-sm font-semibold">Answer</div>
            <div className="rounded border p-3 text-sm whitespace-pre-wrap">
              {answer}
            </div>
          </div>
        )}

        {/* Sources */}
        {sources.length > 0 && (
          <div className="space-y-2">
            <div className="text-sm font-semibold">Sources</div>
            <div className="space-y-2">
              {sources.map((s) => (
                <div key={s.chunkIndex} className="rounded border p-3">
                  <div className="text-xs font-semibold mb-2">
                    [Chunk {s.chunkIndex}] • score {s.score.toFixed(3)}
                  </div>
                  <div className="text-sm whitespace-pre-wrap">
                    {s.preview}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Chunks (optional, but nice for transparency) */}
      {chunks.length > 0 && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-semibold">Chunks</h2>
            <div className="text-xs opacity-60">
              Showing first {Math.min(chunks.length, 50)}
            </div>
          </div>

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