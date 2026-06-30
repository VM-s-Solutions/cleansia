import CleansiaCore
import SwiftUI

/// Status accent color keyed off the backend `DisputeStatus.value` (1-indexed,
/// the `disputeStatusColor` parity): 1 Pending → warning, 2/3 UnderReview /
/// WaitingForResponse → primary, 4 Resolved → success, 5 Closed → neutral,
/// 6 Escalated → error, nil/unknown → outline.
enum DisputeStatusPresentation {
    static func color(_ statusValue: Int?) -> Color {
        switch statusValue {
        case 1: CleansiaColors.warningStar
        case 2, 3: CleansiaColors.primary
        case 4: CleansiaColors.successText
        case 5: CleansiaColors.onSurfaceVariant
        case 6: CleansiaColors.error
        default: CleansiaColors.outlineVariant
        }
    }

    static func label(_ name: String?) -> String {
        guard let name, !name.isBlank else { return "—" }
        return name
    }
}
