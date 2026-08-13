# Request logging and PII redaction

Every host logs request bodies. Doing that safely is four decisions, each of which was a defect first.

## The scan is bounded, and bounded in characters {#scan-limit}

The middleware reads at most `RedactionScanLimit + 1` characters — never the whole body.

It runs **before authentication and before the rate limiter**. Reading to the end therefore made an
anonymous request of Kestrel's maximum size a ~121 MB allocation that was then discarded: 423× the
body, with nothing upstream able to throttle it.

The bound is in **characters, not bytes**, because the verdict downstream is `string.Length`. One
character past the cap decides it exactly as the whole body would, so no log line changes. A byte bound
would put a multi-byte body under the cap and log what must be suppressed.

## Redact before truncating {#redact-before-truncate}

The redaction regex matches a **complete quoted value**. Truncate first and any secret whose closing
quote falls past the cut leaves its raw prefix visible.

Redacting first costs a scan of the whole body, so anything past the scan limit is **suppressed
outright** rather than scanned or truncated. That bounds the per-request cost without reopening the
prefix leak.

## Two passes, because they are two different defects {#two-passes}

| | Value size | What collapsing it does |
|---|---|---|
| Credential / payload | unbounded — a base64 image, a signed URL, a JWT | **frees hundreds of bytes of window**, dragging whatever follows into the log |
| Contact identity | bounded, tens of bytes | shifts the window by tens of bytes; for short values it *lengthens* the body |

Keeping them in one alternation would make every string member of every DTO read as "unmasked" — which
is how a guard stops being informative.

## Contact identity is matched by shape, not enumerated {#matched-by-shape}

Enumerating it **was** the defect. The leak was never one endpoint — it was the body-logging helper,
generic over every route — so a per-route or per-exact-name entry fixes the instance and leaves the
class open. Measured when this was written: **152 members on 80+ routes** carry one of these names.

The alternation is quote-anchored on both sides so a name must match **whole**. That keeps
`emailTemplateId` (an id) out while catching `customerEmail`, and it is why `*email` takes no suffix
while `*phone*` does. The value group matches a quoted string or `null` and nothing else, so a boolean
neighbour like `isEmailConfirmed` is structurally unreachable.

## What makes a denylist fail closed {#fail-closed}

Not the list. A guard test walks **every wire DTO on all five hosts** and reddens CI when a PII-shaped
member is neither matched by the regex, nor on a suppressed route, nor excepted in writing.

It reads the regex itself, so the two cannot drift.
