# Flows

How a thing actually happens, end to end, across every layer it touches.

A flow page answers one question a reader has at 2am: *what happens when…* — and, more usefully,
*what happens when it goes wrong*. Each carries the happy path, every branch, the edge cases and how
they are handled, the code seams, and the failure modes.

This is the section the rest of the site hangs off. `/domain` says what the nouns are, `/decisions`
says why a shape was chosen, `/architecture` says how a layer is built — but only a flow page tells
you what a customer tapping *Book* sets in motion.

## The map

```mermaid
flowchart LR
  subgraph Identity
    A[Auth &amp; identity]
  end

  subgraph Booking
    B[Booking &amp; pricing]
    C[Payment &amp; fiscal]
  end

  subgraph Fulfilment
    D[Offerability, hold &amp; take]
    E[Execution &amp; completion]
  end

  subgraph Money
    F[Cancellation, refund, dispute]
    G[Pay, periods, invoices, payouts]
  end

  subgraph Retention
    H[Loyalty, memberships, referrals]
  end

  subgraph Compliance
    I[GDPR, retention, audit]
  end

  A --> B --> C --> D --> E
  E --> G
  B -.-> H
  C --> F
  E --> F
  A -.-> I
  E -.-> I

  classDef money fill:#fde68a,stroke:#b45309,color:#1f2937
  class C,F,G money
```

Solid edges are the ordinary path. Dashed edges are couplings that are easy to miss and expensive to
forget — a booking touches the loyalty ledger, and completion leaves a trail the erasure sweep has to
reckon with.

Shaded boxes move money. Those three carry the flows where a mistake is measured in currency rather
than in a confused user, and they are the ones to read first.

## Cross-cutting

Some concerns do not belong to a single flow and are documented once rather than repeated in each:
tenancy scoping, the outbox and its drainer, consumer idempotency, notification dispatch, and rate
limiting.

## The pages

| Flow | |
|---|---|
| **[Auth and identity](/flows/auth-and-identity)** | sign-in, session rotation, theft detection, revocation |
| **[Booking and pricing](/flows/booking-and-pricing)** | quote, create, express surcharge, recurring, guest lookup |
| **[Payment and fiscal](/flows/payment-and-fiscal)** | the Stripe webhook, replay, receipts, the stale-checkout sweep |
| **[Offerability and the take](/flows/offerability-and-take)** | the board, the preferred hold, the seat race |
| **[Execution and completion](/flows/execution-and-completion)** | on-the-way to completed, photos, what a browsing cleaner sees |
| **[Cancellation, refund and dispute](/flows/cancellation-refund-dispute)** | the fee ladder, refund bounds, the dispute guard |
| **[Pay, periods, invoices and payouts](/flows/pay-and-payouts)** | pay rows, period states, claimed numbering, payout disclosure |
| **[Loyalty, memberships and referrals](/flows/loyalty-and-memberships)** | points, Plus, the metered express waiver |
| **[GDPR, retention and audit](/flows/gdpr-and-audit)** | anonymise-in-place, what survives it, the audit trail |
| **[Cross-cutting concerns](/flows/cross-cutting)** | tenancy, outbox, idempotency, notifications, rate limiting |

Each was written from the end-to-end walk recorded in the cleanup track's gap register, and each
carries the edge cases as a table — including the ones that are **accepted rather than fixed**, with
the reason stated. A residue nobody wrote down is indistinguishable from a bug nobody found.
