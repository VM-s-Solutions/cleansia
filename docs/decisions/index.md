# Decisions

The architecture and business decisions that shaped the platform, and the arguments that settled them.

## What a decision record is for

Code shows *what* was built. Tests show what it must keep doing. Neither shows **what else was
considered and why it lost** — and that is the thing a future reader needs before changing something
that looks arbitrary.

Every record here states the context, the decision, and the consequences the team accepted. Several
also carry the alternative that was rejected and the reason, which is usually the most valuable part.

## How they are referenced

A decision keeps a stable id — `ADR-0037` — and roughly six hundred source files already cite ids in
that form. The id is the reference, not the file path, so a record can be retitled or moved without
breaking a single citation.

When code needs to explain itself, it names the id and links here rather than restating the argument:

```csharp
// Offerability is a payment-qualified status rule, not a status list. → /decisions/adr-0037
```

## Status

A record is `accepted`, `superseded` or `superseded-in-part`. Superseded records are kept rather than
deleted: a citation in code may point at one, and the fact that a decision was reversed is itself
part of the history.

## What is here now

The records are being migrated in. Until they land they live in the repository under
`agents/backlog/adr/`.
