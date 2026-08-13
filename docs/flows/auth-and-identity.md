# Auth and identity

Sign-in, sessions, and how a stolen session is ended.

## Sessions

An access token is short-lived; a refresh token is the long-lived credential and is **rotated on every
use**. Only a hash of it is stored.

| Host | Access token |
|---|---|
| Admin web | 15 min |
| Partner web, Customer web | 24 h |
| Both mobile hosts | 30 min |

The admin figure is deliberate: on web there is no device id, so the TTL *is* the revocation window.
Partner and customer web stay at 24 h by a separate, recorded decision.

## Rotation detects theft

```mermaid
flowchart TB
  A[Refresh token presented] --> B{Already rotated?}
  B -- no --> C{Revoked or expired?}
  C -- no --> D[Rotate: issue new, mark old 'rotated']
  C -- yes --> E[Refuse]
  B -- yes --> F["THEFT SIGNAL"]
  F --> G[Revoke the entire chain, commit immediately]
  G --> H[401 — all sessions ended]

  classDef bad fill:#fee2e2,stroke:#b91c1c,color:#7f1d1d
  class F,G,H bad
```

Presenting an already-rotated token means either a client that retried, or a thief. Both are handled
the same way: kill the chain.

**The chain revoke commits itself**, independently of the caller. The unit-of-work pipeline commits
only on success, and this path deliberately returns a failure — so without its own commit the security
revocation would be rolled back and every stolen token would stay valid.

Revocation is idempotent, so it retries on a concurrency collision rather than surfacing a 500. When
the retry budget is spent it falls back to a **set-based revoke that verifies termination** and throws
rather than reporting a revocation that provably did not complete. A kill switch cannot be outraced
into failing open.

## Immediate cutoff beyond the token TTL

Mobile hosts additionally consult polled directories of revoked devices and revoked users, so a
password reset or a device revocation takes effect without waiting for the token to expire. The web
hosts have no device id, which is why the admin TTL was shortened instead.

## Edge cases

| Case | What happens |
|---|---|
| Logout presenting an already-rotated token | Walks the successor chain and kills the live descendant — session-scoped, so a benign client race logs out one session rather than all devices. |
| Logout with an unknown token | Silently succeeds. Logout is idempotent and must not confirm whether a token exists. |
| Password reset while a thief holds a session | All sessions revoked; the revoke is committed before the password change, so the failure mode is "tokens dead, retry" and never "tokens alive". |
| Rotation racing a revoke | The revoke wins — the rotation's commit fails on the concurrency token and rolls back both the mark and the new token. |
| Device revoked | Only tokens carrying that device id are ended. A token with no device id survives to natural expiry rather than being killed by an unrelated device. |
| Google or Apple sign-in | Resolved by **subject**, never by email address. |
