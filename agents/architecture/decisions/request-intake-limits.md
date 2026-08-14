# Request intake limits — living decision doc

> **Status: NOTHING BELOW IS IMPLEMENTED YET.** This documents the decided *shape* for T-0557 and the
> facts it rests on. The immutable record is
> `agents/archive/2026-08/adr-deliberation/drafts/NNNN-host-request-intake-ceiling.md` (**`proposed`**, number not allocated,
> **defense panel owed** — T-0557 AC1 requires distinct author/challenger/lead instances).
> **Ticket:** T-0557. **Related:** ADR-0003 (rate limiting — the pipeline order this depends on),
> T-0548 (avatar size cap, shipped), T-0556 (`SaveMyDocuments`, shipped).

---

## 1. The three bounds, and why conflating them is the recurring mistake

| Bound | Where it lives | What it protects | What it CANNOT do |
|---|---|---|---|
| **Transport ceiling** | Kestrel, `CleansiaStartupBase` | the machine — refuses bytes before the app owns them | give a legible answer; it has no error key |
| **Per-item size cap** | `BlobFileSize` via `ImageFileValidator` / `DocumentFileValidator` | correctness of the *answer* — 400 + `file.size_exceeded` | prevent the allocation; validation runs after buffering |
| **Per-request count cap** | each command's `Validator` | rows written + blob uploads per request | bound bytes at all |

Every ticket in this area so far has fixed one and been read as having fixed another. The catalog
sentence that comes out of it: **an intake bound is a host property; a per-request count/size answer is
a validator property; they are different guarantees and a change owes both.**

## 2. State at HEAD (2026-08-05)

**Ceiling: none chosen.** `MaxRequestBodySize` / `RequestSizeLimit` / `MultipartBodyLengthLimit` /
`DisableRequestSizeLimit` appear nowhere in `src/**`. Effective ceiling = **Kestrel's 30,000,000 B
(≈28.6 MiB)** on all five hosts. Linux App Service (`DOTNETCORE|10.0`), so no IIS limit is in play.

**Ten base64 intake routes across four hosts**, enumerated by `Base64UploadIntakeRosterTests` (Admin has
none). Per-item and per-request state:

| Command | Per-item | Count cap | Note |
|---|---|---|---|
| `UpdateCurrentUser` (avatar) | 10 MiB (`ImageFileValidator`) | n/a (single) | T-0548 |
| `SaveMyDocuments` | 10 MiB (`DocumentFileValidator`) | **10** | T-0556 |
| `UpdateEmployee.Documents` | 10 MiB (`DocumentFileValidator`) | **10** | |
| `SaveOrderPhotos.Photos` | 10 MiB (`BlobFileSize`) | **none** | the last uncapped array |

**The contradiction that has always been there:** the stated per-request policy is
`10 files × 13.33 MiB wire ≈ 133 MiB` (base64 is +33 % over the 10 MiB decoded cap), against a 28.6 MiB
transport ceiling — **2 max-size files fit, the 3rd does not**. It has never been reported because the
refusal is a bare 413 that every client renders as *"An error occurred. Please try again."*

**Client shapes differ, which matters for who feels the ceiling:**
- web **batches** (`profile-documents.facade.ts:209-220`, `order-photos.facade.ts:43-48`), per-file
  validation only (`cleansia-file.component.ts:36`);
- iOS and Android send **one item per request** (`DocumentsSectionViewModel.swift:46-56`,
  `PartnerOrderClient.swift:212-222`) and are effectively unaffected.

## 3. The amplifier — the fact that reframed the ticket

```
CleansiaStartupBase.Configure
  :136-140  EnableBuffering()            unbounded → 30 KiB memory, remainder to a TEMP FILE
  :166      RequestLoggingMiddleware     reads the WHOLE body to a UTF-16 string (≈2× bytes, LOH)
  :168      UseExceptionHandler          500 + plain text for everything
  :180      UseAuthentication()          ← the read already happened
  :181      UseRateLimiter()             ← the read already happened
```

`RequestLoggingMiddleware.ReadRequestBodyAsync` (`:129-144`) materializes the whole body; `SafeBody`
(`:166-179`) then **throws it away** for anything over `RedactionScanLimit` (64 KiB). So the expensive
allocation happens exactly for the bodies whose content is never used, from a position an anonymous
caller reaches, on a plan that is **S1 (1.75 GB) in prod shared by 5 APIs + SSR + Functions**.

The middleware already bounded the redaction **CPU** at 64 KiB (`:14-16`) and left the **allocation**
unbounded — same defect, half-fixed. Five copies exist, one per host.

**Consequence for the decision:** a transport ceiling alone is not a resource-exhaustion control here.
It caps the multiplier; bounding the read is what removes the amplifier.

## 4. The decided shape

1. **One ceiling**, in `CleansiaStartupBase.ConfigureServices`, via `KestrelServerOptions.Limits`,
   default in source (**32 MiB**) and overridable by configuration key per environment/host.
2. **32 MiB** because: ≥ today's accidental ceiling (nothing that works stops working), admits the
   two-max-size-file batch the endpoints' own policy implies, and stays inside the S1 heap budget once
   §3 is fixed. Provisional by construction — the config key is the instrument for revising it on
   evidence that does not exist yet.
3. **Failure contract: 413, no body.** Distinguishable from the validator's 400 by **status only**.
   Each client's error mapper gains a status-413 branch ahead of the body read, resolving to the
   **existing** `file.size_exceeded` key — no new key, so the `error-contract-parity` guards are
   untouched. **This ships with the ceiling, not after it.**
4. **Bound the pre-auth read** to `RedactionScanLimit + 1` bytes (log output byte-identical) and give
   `EnableBuffering` explicit bounds. Own ticket; **this is where the security value is**.
5. **No per-endpoint `[RequestSizeLimit]`** — in this pipeline it cannot function (the body read starts
   upstream of MVC's resource filters, and the size feature is read-only once the read starts). A
   *lower* per-endpoint bound belongs in a validator, which can answer 400 with a key.
6. **Guard**: csproj walk for `Sdk="Microsoft.NET.Sdk.Web"` with a `>= 5` non-vacuity floor + every
   discovered host's `Startup` derives from `CleansiaStartupBase` + the registration is asserted through
   resolved options. **Not** `WireSurface.HostAssemblies()` — that is a hand-maintained roster and
   cannot notice a new host.

## 5. Open / owed

- **Panel.** T-0557 AC1 needs distinct author/challenger/lead instances. Not run.
- **⚠ The one runtime claim to pin first.** Whether Kestrel's over-limit 413 survives this pipeline or is
  converted to `500 "An unexpected error occurred."` by `UseExceptionHandler` (`:168`). The client work
  in §4.3 depends on the answer; an integration/HostTests case owes it before that work starts. If it is
  a 500, the rejected alternative A4 (a `Content-Length` check in middleware returning ProblemDetails)
  becomes the answer.
- **`SaveOrderPhotos` count cap** — own ticket, mirroring the `SaveMyDocuments` shape.
- **Client-side aggregate staging budget on web** — the actual user-facing repair; refuse the batch
  before the upload rather than after.
- **Egress mirror, unexamined.** `RequestLoggingMiddleware:35-36` swaps the response body for a
  `MemoryStream`, so every response is buffered in memory — invoice PDFs, GDPR exports. Same shared
  plan, opposite direction. Nobody has looked.
- **`Cleansia.Functions`** is not a `Cleansia.Web.*` host and inherits nothing from
  `CleansiaStartupBase`. Whether its triggers have an intake surface needing the same ruling is unasked.
