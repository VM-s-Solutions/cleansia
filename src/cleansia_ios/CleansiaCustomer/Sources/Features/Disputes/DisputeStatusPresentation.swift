import CleansiaCore
import SwiftUI
import UIKit

/// Status accent color keyed off the backend `DisputeStatus.value` (1-indexed,
/// the `disputeStatusColor` parity): 1 Pending → amber, 2/3 UnderReview /
/// WaitingForResponse → blue, 4 Resolved → green, 5 Closed → neutral,
/// 6 Escalated → error, nil/unknown → neutral. The colour is the pill's ink and its 14 % wash, so each one
/// reads 4.5:1 or more on its own wash over the card in both modes (finding 2026-10-05: the pending amber
/// read 1.93:1 on its wash, the resolved green 4.15:1 in light and 2.57:1 in dark mode).
enum DisputeStatusPresentation {
    static func color(_ statusValue: Int?) -> Color {
        switch statusValue {
        case 1: pendingInk
        case 2, 3: CleansiaColors.primaryText
        case 4: resolvedInk
        case 6: CleansiaColors.error
        default: neutralInk
        }
    }

    /// amber-800 in light mode (5.70:1), the warning amber-500 in dark (5.33:1).
    static let pendingInk = ink(light: 0x92400E, dark: 0xF59E0B)
    /// green-800 in light mode (5.75:1), green-400 in dark (6.19:1).
    static let resolvedInk = ink(light: 0x166534, dark: 0x4ADE80)
    /// slate-600 in light mode (6.13:1), slate-300 in dark (6.94:1), where `onSurfaceVariant`'s slate-400 read
    /// 4.47:1; Android's pair. A status the app does not know yet reads in it too, rather than in an outline
    /// colour (1.2:1).
    static let neutralInk = ink(light: 0x475569, dark: 0xCBD5E1)

    static func label(_ name: String?) -> String {
        guard let name, !name.isBlank else { return "—" }
        return name
    }

    private static func ink(light: UInt32, dark: UInt32) -> Color {
        Color(UIColor { traits in
            let hex = traits.userInterfaceStyle == .dark ? dark : light
            return UIColor(
                red: CGFloat((hex >> 16) & 0xFF) / 255,
                green: CGFloat((hex >> 8) & 0xFF) / 255,
                blue: CGFloat(hex & 0xFF) / 255,
                alpha: 1
            )
        })
    }
}
