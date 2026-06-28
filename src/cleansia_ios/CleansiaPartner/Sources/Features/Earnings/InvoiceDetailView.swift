import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
#if canImport(UIKit)
    import UIKit
#endif

private struct PreviewablePdf: Identifiable {
    let url: URL
    var id: String {
        url.absoluteString
    }
}

struct InvoiceDetailView: View {
    @StateObject private var vm: InvoiceDetailViewModel
    @State private var pdf: PreviewablePdf?
    private let snackbar: SnackbarController
    private let onOpenPeriodPay: (String, String?) -> Void

    init(
        invoiceId: String,
        client: PartnerPayrollClient,
        snackbar: SnackbarController,
        onOpenPeriodPay: @escaping (String, String?) -> Void
    ) {
        _vm = StateObject(wrappedValue: InvoiceDetailViewModel(
            invoiceId: invoiceId,
            client: client,
            snackbar: snackbar
        ))
        self.snackbar = snackbar
        self.onOpenPeriodPay = onOpenPeriodPay
    }

    var body: some View {
        content
            .navigationTitle(L10n.Invoices.details)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            .onReceive(vm.presentPdf) { pdf = PreviewablePdf(url: $0) }
            .sheet(item: $pdf) { item in
                pdfPreview(item.url)
            }
    }

    @ViewBuilder
    private func pdfPreview(_ url: URL) -> some View {
        #if canImport(UIKit)
            // E4: the QuickLook coordinator deletes the cached PDF on dismissal.
            QuickLookPreview(url: url, onDismiss: { pdf = nil })
                .ignoresSafeArea()
        #else
            EmptyView()
        #endif
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background.ignoresSafeArea())
        case let .error(error):
            InvoiceDetailErrorView(error: error) { Task { await vm.load() } }
        case let .loaded(invoice):
            InvoiceDetailContent(
                invoice: invoice,
                canOpenPdf: vm.canOpenPdf,
                isDownloading: vm.pdfState.isDownloading,
                onOpenPeriodPay: onOpenPeriodPay,
                onOpenPdf: { Task { await vm.openPdf() } },
                onCopy: copyToClipboard
            )
            .background(CleansiaColors.background.ignoresSafeArea())
        }
    }

    private func copyToClipboard(_ value: String) {
        #if canImport(UIKit)
            UIPasteboard.general.string = value
        #endif
        snackbar.showSuccess(L10n.Invoices.fieldCopied)
    }
}

private struct InvoiceDetailErrorView: View {
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
    struct InvoiceDetailView_Previews: PreviewProvider {
        static var previews: some View {
            InvoiceDetailContent(
                invoice: sample,
                canOpenPdf: true,
                isDownloading: false,
                onOpenPeriodPay: { _, _ in },
                onOpenPdf: {},
                onCopy: { _ in }
            )
            .background(CleansiaColors.background)
        }

        private static var sample: EmployeeInvoiceDetailDto {
            EmployeeInvoiceDetailDto(
                id: "inv-1",
                payPeriodId: "pp-1",
                payPeriodLabel: "1 – 15 Jun 2026",
                invoiceNumber: "INV-2026-001",
                variableSymbol: "20260001",
                paymentReference: "REF-001",
                totalOrders: 3,
                subTotal: 4000,
                bonusAmount: 250,
                deductionAmount: 50,
                totalAmount: 4200,
                currencyCode: "CZK",
                status: ._3,
                generatedAt: Date(),
                approvedAt: Date(),
                paidAt: Date(),
                adminNotes: "Approved by admin.",
                bankTransferNote: "Reference your VS."
            )
        }
    }
#endif
