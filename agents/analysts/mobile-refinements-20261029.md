# Mobile refinements — 29 October 2026

Mike’s new request covers local generated-build cleanup, fresh Xcode source/version guidance and the listed Customer/Partner native improvements. Android and iOS stay aligned. Functional server enforcement and minimal web payment-policy parity accompany the native changes.

The cash rule now requires a usable verified saved card and a one-cleaner job. This explicitly supersedes the earlier 4 October no-card requirement. It does not authorize automatically charging that card. Planned required crew remains authoritative before assignment; zero assigned cleaners is not permission for a two-cleaner cash job.

Status work has two deliverables: restore pulse/overrun behavior in the current component, and offer distinct new visual concepts for Mike to choose. Final replacement layout awaits that choice.

User-visible copy becomes concise while validation, safety and accessibility labels remain clear. Existing platform behavior is preserved where the reported defect is absent; physical iCloud Keychain behavior is reported separately from synthetic simulator coverage.

Work items: T-0884–T-0896. All current source edits are local on codex/mobile-refinements-20261029, preceded by the preserved closure commit323e8b511. No new rollout is authorized.

## Local verification review

Server: solution build;9142 unit,1038 PostgreSQL and518 route tests pass. The original unit pass is retained with a byte-level proof that its only subsequent C# changes are seven integration/route fixture corrections; full database/route successors pass. Named phone, saved-card, Android stale-read and iOS credential-rotation mutation checks fail at the intended assertion, restore exact bytes and pass.

Native: optimized Core864, Partner1269 and Customer1916 tests pass with one existing VoiceOver skip. Targeted iOS16.4 tests pass80/64/94; full SwiftFormat/SwiftLint pass. Android347 Core +1004 Partner +1803 Customer =3154 tests pass without skips. Native capture fixtures use synthetic data and loopback API hosts; Android's owned read-only emulator is also network-disabled and stopped after use.

Web:1061 tests in six configurations, scoped lint and all three production builds pass. Documentation builds after installing its locked dependencies; the lock is unchanged. Initial failures and successful successors are both retained in the local report. Three status concepts pass36 browser cases, positive pulse and reduced-motion checks; the choice remains Mike's.

Physical Keychain/highlight appearance and physical-device animation smoothness remain acceptance limits (T-0885 AC2 and T-0889 AC1). The mascot probe uses actual display callbacks with an injected ticker, not physical display measurements. Initial hosted Debug startup traffic is unknown; no zero-DEV claim is made. Current/final native hosts use loopback. Cash terms alignment is required before a future deployment. Graphify rebuild was attempted but its Python module is unavailable.

T-0884–T-0896 are in review, with those explicit acceptance limits; they are not marked merged or released.
