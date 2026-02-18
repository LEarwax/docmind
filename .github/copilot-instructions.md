## DocMind — Copilot Instructions

This repository has two primary parts: a Next.js frontend and a minimal .NET backend.

High-level architecture
- Frontend: Next.js (app router) in [frontend](frontend) — UI and file-upload client.
- Backend: .NET minimal API in [backend](backend) — small HTTP API serving `/ping`, `/weatherforecast`, and `/upload`.
- File uploads are saved to the `uploads` folder under the backend project root.

Quick start (developer commands)
- Start backend: `cd backend && dotnet run` (launches at http://localhost:5237 by default — see [backend/Properties/launchSettings.json](backend/Properties/launchSettings.json)).
- Start frontend: `cd frontend && npm install && npm run dev` (Next.js dev server on http://localhost:3000).
- Example upload via curl:

```
curl -F "file=@/path/to/file.pdf" http://localhost:5237/upload
```

Key files to inspect
- [backend/Program.cs](backend/Program.cs) — all server endpoints, CORS policy, and OpenAPI mapping.
- [backend/backend.csproj](backend/backend.csproj) — target framework and package references.
- [frontend/app/page.tsx](frontend/app/page.tsx) — upload UI and fetch to `http://localhost:5237/upload`.
- [frontend/package.json](frontend/package.json) — scripts and dependency versions (Next 16 / React 19).
- [setup.md](setup.md) — repository-level setup notes.

Project-specific patterns and conventions
- Backend uses the .NET "minimal API" style (top-level statements and `app.MapGet/MapPost`). Add routes directly to `Program.cs` when small; larger features should be moved to separate files/extension methods.
- CORS is configured with a default policy that allows `http://localhost:3000`. If you change frontend host/port, update the origins in [backend/Program.cs](backend/Program.cs).
- Upload handling: the backend expects multipart form-data and uses `form.Files["file"]`. Filenames are sanitized using `Path.GetFileName` and written to `uploads/`.
- OpenAPI is enabled in Development (`builder.Services.AddOpenApi()` + `app.MapOpenApi()`), useful for exploring endpoints locally.

Editing tips & examples
- Add a new API route: modify `Program.cs` using `app.MapGet` or `app.MapPost`. For more structure, factor routes into extension methods and separate classes.
- To test uploads from the UI: run backend then frontend, open http://localhost:3000 and use the file input in the main page.

Common issues & troubleshooting
- Backend not reachable from frontend: ensure the backend is running and the port matches [backend/Properties/launchSettings.json](backend/Properties/launchSettings.json). Also confirm CORS origins.
- Missing dependencies for frontend: run `npm install` in [frontend](frontend) before `npm run dev`.
- If you need hot-reload for the backend, run `dotnet watch run` inside `backend`.

What I (the AI) should do when editing code
- Prefer minimal, focused changes that follow the repo's current style (keep `Program.cs` minimal API style unless adding a clear reason to refactor).
- When adding endpoints, update `setup.md` or this file if runtime assumptions change (ports, CORS, upload locations).
- Provide runnable commands and small examples (curl, npm, dotnet) in PR descriptions.

If anything here is unclear or you'd like more detail (example PR, tests, or refactor suggestions), tell me which area to expand.
