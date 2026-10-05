import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit

struct InvoiceStatusBadge: View {
    let status: EmployeeInvoiceStatus?

    /// "Approved" is a solid pill: white on sky-700 in light mode (5.93:1, as Android's), where white read 4.10:1
    /// on the primary's sky-600; sky-950 on sky-400 in dark (6.48:1), where the primary's own ink, sky-900, read
    /// 4.42:1. Dark keeps the sky-400 fill so the pill stays apart from Pending's container, sky-700 there.
    static let approvedFill = color(light: 0x0369A1, dark: 0x38BDF8)
    static let approvedInk = color(light: 0xFFFFFF, dark: 0x082F49)

    private static func color(light: UInt32, dark: UInt32) -> Color {
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

    private var label: String {
        switch status {
        case ._1: L10n.Invoices.statusPending
        case ._2: L10n.Invoices.statusApproved
        case ._3: L10n.Invoices.statusPaid
        case ._4: L10n.Invoices.statusDisputed
        case ._5: L10n.Invoices.statusRejected
        case ._6: L10n.Invoices.statusCancelled
        case .none: "—"
        }
    }

    var background: Color {
        switch status {
        case ._1: CleansiaColors.primaryContainer
        case ._2: Self.approvedFill
        case ._3: CleansiaColors.successBg
        case ._4, ._5: CleansiaColors.errorContainer
        case ._6, .none: CleansiaColors.surfaceVariant
        }
    }

    var foreground: Color {
        switch status {
        case ._1: CleansiaColors.primaryTextOnContainer
        case ._2: Self.approvedInk
        case ._3: CleansiaColors.successText
        case ._4, ._5: CleansiaColors.error
        case ._6, .none: CleansiaColors.onSurfaceVariant
        }
    }

    var body: some View {
        Text(label)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(foreground)
            .padding(.horizontal, 10)
            .padding(.vertical, Spacing.xxs)
            .background(background, in: Capsule())
    }
}
