import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct InvoicesListView: View {
    @StateObject private var vm: InvoicesListViewModel
    private let onOpenInvoice: (String) -> Void

    init(
        client: PartnerPayrollClient,
        staleness: InvoicesStaleness,
        snackbar: SnackbarController,
        onOpenInvoice: @escaping (String) -> Void
    ) {
        _vm = StateObject(wrappedValue: InvoicesListViewModel(
            client: client,
            staleness: staleness,
            snackbar: snackbar
        ))
        self.onOpenInvoice = onOpenInvoice
    }

    var body: some View {
        content
            .navigationTitle(L10n.Invoices.title)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            // On-appear fires on first show AND on pop-back from the detail —
            // the silent-stale watermark makes the warm case a no-op.
            .onAppear { Task { await vm.onAppear() } }
            .refreshable { await vm.userRefresh() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background.ignoresSafeArea())
        case let .error(error):
            InvoicesErrorView(error: error) { Task { await vm.userRefresh() } }
        case let .loaded(invoices):
            InvoicesListContent(invoices: invoices, onOpenInvoice: onOpenInvoice)
                .background(CleansiaColors.background.ignoresSafeArea())
        }
    }
}

private struct InvoicesErrorView: View {
    let error: ApiError
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "exclamationmark.triangle")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.error)
            Text(ApiErrorLocalizer().message(for: error))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct InvoicesListView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                InvoicesListContent(invoices: sample, onOpenInvoice: { _ in })
                    .background(CleansiaColors.background)
                    .previewDisplayName("Loaded")
                InvoicesListContent(invoices: [], onOpenInvoice: { _ in })
                    .background(CleansiaColors.background)
                    .previewDisplayName("Empty")
            }
        }

        private static var sample: [EmployeeInvoiceDto] {
            [
                EmployeeInvoiceDto(
                    id: "inv-1",
                    payPeriodLabel: "1 – 15 Jun 2026",
                    invoiceNumber: "INV-2026-001",
                    totalOrders: 3,
                    totalAmount: 4200,
                    currencyCode: "CZK",
                    status: ._3,
                    paidAt: Date()
                ),
                EmployeeInvoiceDto(
                    id: "inv-2",
                    payPeriodLabel: "16 – 31 May 2026",
                    invoiceNumber: "INV-2026-000",
                    totalOrders: 2,
                    totalAmount: 2800,
                    currencyCode: "CZK",
                    status: ._1,
                    generatedAt: Date()
                )
            ]
        }
    }
#endif
