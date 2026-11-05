---
id: T-0898
title: Compact Customer order facts and address
size: M
owner: Michael Chaban
created: 2026-10-30
updated: 2026-10-30
depends_on: []
blocks: []
stories: [US-mobile-20261030]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

On 30 October 2026, Mike requested two compact Customer order-detail changes from the supplied screenshot: align the country/currency context with the price when there is no discount, and let the full address share one naturally wrapping text block. This extends the current Appointment confirmation and Home gallery work without reopening the accepted cleaning-status-bar design.

## Acceptance criteria

- [x] **AC1** — On a nondiscounted Customer order, country/currency context and the order's actual total share a row at ordinary phone text size, with the price aligned at the trailing edge. Country/currency comes from the order's existing market lookup; unavailable values retain the existing honest fallback. A discounted order keeps its discount chips, current total and struck original subtotal.
- [x] **AC2** — Street, postal code, city and country form one inline address block, wrapping naturally when width or text size requires it. Blank optional components do not reserve a separate row, and no populated address component is removed.
- [x] **AC3** — Both native Customer apps remain readable in light/dark themes, supported locales and enlarged text. Native captures verify a nondiscounted order, discounted regression and long/full address without clipped amounts or address text. Existing price calculations, payment behavior, map destination and navigation remain unchanged.

## Out of scope

Backend, monetary or market-selection rules; Partner presentation; the cleaning tracker; PRO access; deployment, uploads, push and merge.

## Implementation notes

Keep the existing total/currency and discount presenters. Compose may wrap the context within its available width; SwiftUI may move the facts to a vertical fallback when the horizontal composition does not fit. The address remains ordinary wrapping text rather than a forced street/locality row split.

## Work record

- 2026-10-30 — Ground-truthed Customer Android `OrderDetailHeader.kt:317`, `OrderDetailHeroAndAddress.kt:40` and the facts binding in `OrderDetailScreen.kt:707`; iOS `OrderDetailSummary.swift:230`, `OrderDetailHeroAndAddress.swift:5` and the binding in `OrderDetailContent.swift:53`. The compact source change is present locally; native acceptance is pending at this checkpoint. Evidence is retained in `/Users/michael/cleansia-work.noindex/appointment-gallery-20261030/`.

## Review

Native layout acceptance remains unchecked until the current-source captures and relevant regression checks have been inspected. Historical compact-detail decisions and earlier validation remain separate records.

## Current local verification — 30 October 2026

Root executed the Customer Android unit suite: **1,817 tests, zero failures/errors**, including all 14 new appointment/rotation checks. The expected old-latch RED failures and exact prior-source iOS indicator/status/overnight failures are retained separately. The iOS app-hosted focused suite passed **95 tests, zero failures** on the dedicated iOS 16.4 simulator; the built host's loopback API was verified before test launch. Formatting, strict lint, symbol, localization-preservation, docs-reference, catalog-claims and backlog checks passed.

Root's isolated Android native run retained 30 captures and passed real forward, late backward and motion-off swipe assertions: the selected offer stays through the fresh dwell, its visible capsule fills, and automatic paging resumes. Six final Appointment captures additionally verify the paired sky palette and same-row code/total at ordinary size. The iOS native pager test observes a real changed selection, a stable midpoint and a later automatic selection; captures show the selected fill advancing. Eight iOS and eight Android compact-detail captures preserve market/price, discount amounts and full wrapping addresses across both themes and large text. Independent review approved the current visual/source scope with no blocking issues.

Evidence: `/Users/michael/cleansia-work.noindex/appointment-gallery-20261030/native-final-visual-review.json`, `android-all-receipt.json`, `native-retry1/android-appointment-gallery-receipt.json`, `native-retry2/android-appointment-gallery-receipt.json` and `native-ios-retry2/ios-customer-focused-receipt.json`. Android's final changes after its full unit pass are presentation-only palette/breakpoint adjustments and were rebuilt and recaptured. Actual iOS finger-contact/physical-device smoothness is not claimed by the programmatic native pager check. The existing extreme-large-text Android discount-chip edge clipping is recorded separately and was not changed. No commit or rollout is part of this follow-up.
