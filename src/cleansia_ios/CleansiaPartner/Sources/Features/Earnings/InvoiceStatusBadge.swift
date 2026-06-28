import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct InvoiceStatusBadge: View {
    let status: EmployeeInvoiceStatus?

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

    private var background: Color {
        switch status {
        case ._1: CleansiaColors.primaryContainer
        case ._2: CleansiaColors.primary
        case ._3: CleansiaColors.successBg
        case ._4, ._5: CleansiaColors.errorContainer
        case ._6, .none: CleansiaColors.surfaceVariant
        }
    }

    private var foreground: Color {
        switch status {
        case ._1: CleansiaColors.primary
        case ._2: CleansiaColors.onPrimary
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
