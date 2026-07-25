import ActivityKit
import Foundation

/// The ActivityKit contract shared by the CleansiaCustomer APP (which starts/ends the activity) and the
/// CleansiaCustomerLiveActivity WIDGET (which renders it). This file is a member of BOTH targets — the app
/// target picks it up under `Sources`, the widget target includes it explicitly (see project.yml) — which
/// is also why the ETA decision below lives here: the app produces exactly the window the widget counts on,
/// and only this file is compiled into both.
///
/// It must match the backend wire contract EXACTLY (ADR-0029 D4):
///   • the type name `CleanOrderAttributes` is the `attributes-type` the server sends on push-to-start
///     (LiveActivityPayloadFactory.AttributesType) — renaming it breaks server-started activities;
///   • `ContentState`'s keys {v, status, orderNumber, scheduledStart, scheduledEnd} are the content-state
///     the server pushes on every order-status change (LiveActivityContentState). Pinned both-sides by
///     src/Cleansia.Tests/Functions/Fixtures/live-activity-content-state.json.
///
/// Guarded @available(iOS 16.1, *): ActivityKit is 16.1+, while the customer app floor stays 16.0.
@available(iOS 16.1, *)
struct CleanOrderAttributes: ActivityAttributes {
    /// Dynamic — replaced on every server push. `v` is the content-state schema version, serialized 1:1 as
    /// the wire key "v" (backend LiveActivityContentState.V); it stays single-char to match the byte-stable
    /// contract (`v` is allowlisted in .swiftlint.yml identifier_name).
    struct ContentState: Codable, Hashable {
        var v: Int
        var status: String // onTheWay | inProgress | completed | cancelled (unknown → generic)
        var orderNumber: String
        var scheduledStart: Date
        var scheduledEnd: Date
        /// The ACTUAL phase window: when the cleaner really went on-the-way / started, and the finish
        /// projected from that real start. OPTIONAL in both directions on purpose — synthesized Codable
        /// decodes a missing key as nil, so a new app still decodes an old push and an old app still
        /// decodes a new one.
        var phaseStart: Date?
        var phaseEnd: Date?
    }

    /// Static — set once at activity start (mirrors the backend LiveActivityStartAttributes).
    var orderNumber: String
}

@available(iOS 16.1, *)
extension CleanOrderAttributes.ContentState {
    var etaWindow: EtaWindow {
        EtaWindow(
            scheduledStart: scheduledStart,
            scheduledEnd: scheduledEnd,
            phaseStart: phaseStart,
            phaseEnd: phaseEnd
        )
    }
}

// MARK: - ETA

/// What the live ETA can count on: the booked appointment window (always known) plus the actual phase
/// timestamps when they are known.
struct EtaWindow: Equatable {
    let scheduledStart: Date
    let scheduledEnd: Date
    let phaseStart: Date?
    let phaseEnd: Date?

    /// The instant the countdown hits 00:00 — the actual finish when it is known, else the booked one.
    /// Doubles as the activity's staleDate: `Text(timerInterval:)` CLAMPS at its upper bound, so this is
    /// exactly the moment the card stops telling the truth and the system must re-render it into the
    /// count-up form instead of leaving it frozen at zero.
    var countdownEnd: Date {
        phaseEnd ?? scheduledEnd
    }
}

/// How the widget renders the ETA. Both ticking forms are system-drawn, so they advance on a locked device
/// with no push and no app running.
enum EtaPresentation: Equatable {
    /// Count DOWN to the upper bound. The range always opens at or before "now".
    case countdown(ClosedRange<Date>)
    /// Count UP from a fixed anchor in the past — unbounded, so it can never clamp.
    case elapsed(since: Date)
    /// A final line; a terminal status has nothing left to time.
    case label(String)
}

enum LiveActivityEta {
    /// A countdown that would reach 00:00 within this long isn't worth starting: `Text(timerInterval:)` is
    /// system-drawn and CLAMPS at its bounds, so it would then sit frozen at 00:00 until the next push.
    static let countdownFloor: TimeInterval = 60

    /// Pass a non-nil `terminalLabel` for completed/cancelled — the card then shows that line instead of a
    /// number. Otherwise the actual phase window wins over the booked one, so a clean that started late
    /// counts down to a finish that is still ahead rather than to an end that has already passed.
    static func presentation(window: EtaWindow, terminalLabel: String?, now: Date) -> EtaPresentation {
        if let terminalLabel { return .label(terminalLabel) }
        let start = window.phaseStart ?? window.scheduledStart
        let end = window.countdownEnd
        // Anchor at or before `now`: a range that opens later renders frozen at its full width until it does.
        let anchor = min(start, now)
        guard end > now.addingTimeInterval(countdownFloor) else { return .elapsed(since: anchor) }
        return .countdown(anchor ... end)
    }
}
