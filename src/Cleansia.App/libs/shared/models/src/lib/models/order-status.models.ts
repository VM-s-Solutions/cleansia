/**
 * The order/payment status integers, declared where a `scope:shared` lib may
 * read them.
 *
 * The three generated clients each emit their own copy from their own host's
 * OpenAPI spec, so none of them is canonical — the canonical declaration is the
 * backend domain enum (`Cleansia.Core.Domain.Enums.OrderStatus` /
 * `PaymentStatus`), which all four renderings mirror. Shared code must not
 * reach into any one app's client for it: that is a `scope:shared →
 * scope:partner` break, reported as a circular dependency.
 *
 * `order-status-enum-parity.spec.ts` reads the three generated clients off disk
 * and fails if any of them drifts from this file, so a regen that changes an
 * integer cannot land silently.
 *
 * `Pending` is dead on the write side (ADR-0037 D5) but stays on the wire and
 * in legacy rows, so readers keep handling it.
 */
export enum OrderStatus {
  New = 0,
  Pending = 1,
  Confirmed = 2,
  OnTheWay = 3,
  InProgress = 4,
  Completed = 5,
  Cancelled = 6,
}

export enum PaymentStatus {
  Pending = 1,
  Paid = 2,
  Failed = 3,
  Refunded = 4,
  Disputed = 5,
  PartiallyRefunded = 6,
}
