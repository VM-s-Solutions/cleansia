import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct InvoiceStatusBadge: View {
    let status: EmployeeInvoiceStatus?

    /// "Approved" is a solid sky-700 pill with a white label in both modes, as Android's: white read 4.10:1 on
    /// the light primary (sky-600), and the dark primary's own ink 4.42:1 on sky-400; white on sky-700 is 5.93:1.
    static let approvedFill = Color(red: 0x03 / 255, green: 0x69 / 255, blue: 0xA1 / 255)

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
        case ._2: .white
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
