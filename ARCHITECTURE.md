# Architecture & Decision Records

This document explains the system's structure and — more importantly — **why** it is built the way it is. Each ADR states the decision, the alternatives considered, and the trade-off accepted.

## System Overview

```
┌─────────────────────┐        ┌─────────────────────────────────────────────┐
│  React SPA           │  /api  │  Support.Api — controllers, auth, filters,  │
│  Vite·TS·Tailwind    │───────▶│  rate limiting, ProblemDetails, OTel, Serilog│
│  TanStack Query      │        │      │                                       │
└─────────────────────┘        │      ▼                                       │
                               │  Support.Application — CQRS handlers,        │
                               │  Result<T>+ErrorType, chunker, scorer,       │
                               │  safety inspector                            │
                               │      │                                       │
                               │      ▼                                       │
                               │  Support.Domain — entities, state machine,   │
                               │  invariants (zero dependencies)              │
                               │                                              │
                               │  Support.Infrastructure — EF Core, Gemini,   │
                               │  JWT, outbox worker, SLA monitor             │
                               └──────┬─────────────────────┬─────────────────┘
                                      ▼                     ▼
                                SQL Server            Google Gemini
                          (tickets, outbox, RAG     (classification,
                           chunks + embeddings)      drafting, embeddings)
```

**Async pipelines:** `ClassificationWorker` polls the DB outbox to classify new tickets; `SlaMonitorService` escalates SLA breaches and auto-closes resolved tickets. Both are idempotent.

**RAG pipeline:** publish → `MarkdownChunker` (heading + token-budget split with overlap) → embeddings (`text-embedding-004`) stored per chunk → query-time hybrid ranking (0.7·cosine + 0.3·keyword) → top-5 chunks injected into the draft prompt → cited sections returned with the draft → `DraftSafetyInspector` post-checks the output.

---

## ADR-1: Manual CQRS, no MediatR

**Decision:** Handlers are plain classes (`XHandler.Handle(command, ct)`) auto-registered by an assembly scan; no MediatR.
**Why:** MediatR 12+ moved to a commercial license; the indirection buys little in a codebase of this size. The handler-per-use-case shape is preserved, so migrating to a mediator later is mechanical.
**Trade-off:** No pipeline behaviors — cross-cutting checks live in a global `ValidationFilter` and the `ApiControllerBase.ToActionResult` mapping instead.

## ADR-2: `Result<T>` + `ErrorType` instead of exceptions for flow control

**Decision:** Handlers return `Result<T>` carrying a semantic `ErrorType` (Validation/Unauthorized/Forbidden/NotFound/Conflict); one shared mapper converts it to the HTTP status with an RFC 7807 body.
**Why:** Early versions string-matched error messages ("not found" → 404) — brittle and locale-dependent. A typed error channel makes the controller layer dumb and consistent.

## ADR-3: Passenger identity is provisioned on PNR verification

**Decision:** Verifying a PNR finds-or-creates a real `User` row; the JWT carries that Guid plus a `pnr` claim.
**Why:** The original design put `"pnr:ABC123"` in the NameIdentifier claim; every Guid parse fell back to `Guid.Empty`, giving all passengers one shared identity (IDOR). Real rows make ownership, audit, and authorization uniform across roles.
**Note:** Tokens are written with full-URI claim types (no outbound short-name mapping when constructing `JwtSecurityToken` directly); the frontend accepts both forms.

## ADR-4: Outbox pattern for AI classification (DB polling, no broker)

**Decision:** Ticket creation writes a `ClassificationOutboxItem` in the same `SaveChanges` as the ticket; a hosted worker polls pending rows (5s, batch 10, max 3 attempts with the error recorded).
**Why:** The earlier in-process `Channel` lost queued work on restart and could enqueue for a ticket whose insert failed. The outbox gives atomicity and durability without introducing a message broker the project doesn't yet need.
**Scale-out path:** swap the polling worker for a broker consumer; the outbox write stays identical.

## ADR-5: Brute-force cosine similarity behind `IPolicySearchService`, no vector DB

**Decision:** Embeddings are stored as JSON per chunk in SQL Server; search deserializes (with an in-memory cache) and scores all published chunks per query.
**Why:** At the current scale (tens to low thousands of chunks) exhaustive scoring is correct, simple, and fast enough. The seam is the interface: moving to pgvector/Qdrant/Azure AI Search replaces one class and a migration, nothing above it.
**Trade-off:** O(n) per query — measured as acceptable; revisit at ~100k chunks.

## ADR-6: Hybrid retrieval, weights 0.7 semantic / 0.3 keyword

**Decision:** Final score = 0.7·cosine + 0.3·normalized keyword score (shared `KeywordScorer`); threshold 0.25.
**Why:** Dense vectors catch paraphrases ("para iadesi" ≈ "refund") but can miss exact rare terms (fee names, codes); sparse matching anchors them. The weights favor semantics because policy questions are mostly paraphrastic; the retrieval eval (golden set, recall@5 ≥ 0.8) guards the choice.

## ADR-7: Token-budgeted chunking with overlap

**Decision:** Markdown is split by headings, then sections exceeding ~500 tokens are split at sentence boundaries with ~50 tokens of overlap; sub-chunks are titled `Section (i/n)`. Token count is approximated as chars/4.
**Why:** Whole-section chunks blow the embedding sweet spot and dilute similarity; hard cuts lose boundary context — overlap preserves it. Exact tokenization isn't needed for sizing, so no tokenizer dependency.

## ADR-8: Deterministic safety inspector on AI drafts

**Decision:** After generation, `DraftSafetyInspector` re-checks the draft for (a) specific monetary promises and (b) ≥6-word verbatim overlap with internal notes, surfacing findings as risk flags. It warns, never blocks.
**Why:** Prompt instructions are necessary but not sufficient; output-side checks are cheap and deterministic. The agent stays in the loop — flags inform review rather than censoring.

## ADR-9: Draft feedback as audit events, not a new table

**Decision:** 👍/👎 on AI drafts is recorded as `AuditEventType.DraftFeedback`; the dashboard derives the acceptance rate from these events.
**Why:** The append-only audit stream already has actor/timestamp semantics; a dedicated table earns its keep only when feedback gets structure (ratings, categories). Start with the simplest faithful record.

## ADR-10: OpenTelemetry tracing without the OTLP exporter package

**Decision:** Tracing is wired (ASP.NET Core + HttpClient instrumentation + a custom `Skydesk.AI` ActivitySource around Gemini calls), but the OTLP exporter package is not referenced.
**Why:** Every available exporter version carries open advisories via its transitive gRPC dependencies. Spans cost nothing without an exporter; add one alongside a collector (Jaeger/Tempo) deployment.

## ADR-11: CSS-only dashboard charts

**Decision:** The dashboard renders bar/column charts with plain divs + Tailwind, no chart library.
**Why:** The data is small categorical counts; a charting dependency (~100KB+) buys nothing here. Swap in a library when interactivity (zoom, tooltips, time series) is actually required.

## ADR-12: InMemory provider for integration tests

**Decision:** Integration tests run the real HTTP pipeline against EF's InMemory provider with mock AI services.
**Why:** Zero external dependencies → CI-trivial and fast (50+ tests in ~40s).
**Known gap:** InMemory doesn't enforce `RowVersion` or translate SQL — concurrency behavior is verified by code review and would need Testcontainers/SQL Server for full fidelity. Accepted consciously.
