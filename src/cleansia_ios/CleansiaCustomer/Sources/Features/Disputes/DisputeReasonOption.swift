import Foundation

/// The seven customer-facing dispute reasons (1-indexed, the `REASON_OPTIONS`
/// parity). The backend `DisputeReason` enum carries an eighth value the
/// customer surface does not expose — mirroring Android's 7-option picker.
struct DisputeReasonOption: Identifiable {
    let value: Int
    let label: String

    var id: Int {
        value
    }

    static var all: [DisputeReasonOption] {
        (1 ... 7).map { DisputeReasonOption(value: $0, label: L10n.Disputes.reason($0)) }
    }
}
