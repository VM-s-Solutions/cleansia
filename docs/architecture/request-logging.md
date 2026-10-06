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

## The query string keeps its names and loses every value {#query-string}

The request line — `[{RequestId}] {Method} {Path}{QueryString} | User | IP | Body`, at Information — used
to mask only a query value whose name contained *email*. Everything else went out raw. That included the
guest access token on `GET api/Order/Lookup?token=`, the address a customer types into
`api/AddressSearch/search?q=`, the home coordinates on the static map the booking wizard draws
(`api/AddressSearch/map?lat=&lng=`), and an administrator's search for a name, e-mail or phone in the
paged lists. `IsSensitivePath` suppresses only the body, never the line. On DEV the
`Cleansia` category logs at Information, so the line reached Application Insights and rode Sentry
events as a breadcrumb; on production `Cleansia=Warning` hid it, one setting away from live.

**Since 2026-10-06 every value is masked and every name kept**, on all five hosts:
`QueryParamValueRegex` (`([?&][^=&]*=)[^&]*`) in each `RequestLoggingMiddleware` rewrites the line to
`GET /api/AddressSearch/map?lat=***REDACTED***&lng=***REDACTED***`. That matches the OpenTelemetry server
span, which redacts `url.query` values by default. The five copies stay separate on purpose, and the
request-logging harness runs every assertion against all five, so they cannot drift. Filter and paging
values are gone from DEV logs with the rest; the names say what was asked. A query segment with no `=`
(`?<value>`) still prints as written, because the mask starts after `=`; no first-party client sends
that shape.

**Sentry keeps no query string and only four request headers.** Sentry.AspNetCore copies the raw query
string and every inbound header but the cookie onto each event and transaction, and `SendDefaultPii =
false` covers neither. The headers include the client IP (`X-Forwarded-For`), the CSRF token, the device
id and, behind the App Service front end, headers that carry the original URL. `WithoutRequestDetail` in
`ConfigureSentry` (`ServiceDefaults/Extensions.cs`), hooked into both `SetBeforeSend` and
`SetBeforeSendTransaction`, clears the query string and drops every header but `User-Agent`,
`Content-Type`, `Accept` and `Accept-Language`. It is an allowlist because the platform, not the repo,
decides which proxy headers arrive; a header someone later needs for triage is added on purpose. The
route, the path, the request id and the stack trace stay. The Functions worker shares `ConfigureSentry`
and has no request, so it changes nothing there.

**The other inbound sinks were already closed.** ASP.NET Core's own *Request starting* line, which prints
the query, is in the `Microsoft.AspNetCore` category, which every host's `appsettings*.json` pins at
Warning and no deployment overrides. The OpenTelemetry span redacts query values unless an environment
variable says otherwise, and nothing sets it. HTTP logging and Serilog are not used.

The request line still prints the client IP at Information on every host. The S6 list does not name it,
and whether it stays is an open question for the owner.
→ [Security rules — S6](/architecture/security-rules#inbound-query)
