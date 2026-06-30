import Foundation

/// Pure predicate gating the dispute reply bar + the Add-evidence affordance
/// (the `DisputeFormatters.kt` `disputeAllowsMessages` parity). Resolved (4) /
/// Closed (5) / Escalated (6) are terminal from the customer's perspective;
/// every other value — including nil/unknown — defaults to allowing messages,
/// since the backend rejects a write on a disallowed status and surfacing the
/// input is the safer default.
public enum DisputeMessagePolicy {
    public static func allowsMessages(statusValue: Int?) -> Bool {
        switch statusValue {
        case 4, 5, 6: false
        default: true
        }
    }
}
