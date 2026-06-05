# Open Questions — escalation inbox

Any agent appends a question here when it needs an owner decision. The PM surfaces `blocking: yes`
entries at the next checkpoint. When the owner answers, the entry moves to `answered.md` and the
decision is locked into the relevant artifact (ADR / story / charter) so it's never re-asked.

Format:

```
### Q-NNNN — [blocking: yes|no] <short title>
- Raised by: <agent> (<ticket id>)
- Date: YYYY-MM-DD
- Question: <the precise decision needed>
- Why it matters: <the lasting consequence of getting it wrong>
- Default taken (if non-blocking): <the defensible assumption proceeded with>
- Answer: _(owner fills in)_
```

---

_(no open questions — Q-0001…Q-0005 and Q-RATELIMIT-01/02/03 all answered 2026-06-01; see
`answered.md`. Key outcomes: staff dispute replies = Admin-only (ADR-0001); prod proxy = 1 hop,
`ForwardLimit=1`, rate-limit cleared for prod (ADR-0003); Wave 0 ships rate-limit per-IP-only with
BSP-4b as a fast-follow.)_
