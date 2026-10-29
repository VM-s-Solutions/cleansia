import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

enum PdfState: Equatable {
    case idle
    case downloading

    var isDownloading: Bool {
        self == .downloading
    }
}

@MainActor
final class InvoiceDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<InvoiceDetail> = .loading
    @Published private(set) var pdfState: PdfState = .idle

    /// One-shot: a downloaded local PDF URL ready for the view to present via
    /// QuickLook. The VM never presents/navigates itself.
    let presentPdf = PassthroughSubject<URL, Never>()

    private let invoiceId: String
    private let client: PartnerPayrollClient
    private let snackbar: SnackbarController
    private var isLoading = false

    init(invoiceId: String, client: PartnerPayrollClient, snackbar: SnackbarController) {
        self.invoiceId = invoiceId
        self.client = client
        self.snackbar = snackbar
    }

    var canOpenPdf: Bool {
        guard let invoice = state.loadedValue else { return false }
        return invoice.pdfBlobName?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false
    }

    var pdfMessage: String? {
        guard let invoice = state.loadedValue else { return nil }
        if invoice.pdfGenerationFailed {
            return canOpenPdf ? L10n.Invoices.pdfPreviousWarning : L10n.Invoices.pdfUnavailable
        }
        return canOpenPdf ? nil : L10n.Invoices.pdfPending
    }

    func load() async {
        guard !isLoading else { return }
        isLoading = true
        defer { isLoading = false }
        if state.loadedValue == nil {
            state = .loading
        }
        switch await client.getInvoice(id: invoiceId) {
        case let .success(invoice):
            state = .loaded(invoice)
        case let .failure(error):
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    func openPdf() async {
        guard canOpenPdf, pdfState == .idle else { return }
        pdfState = .downloading
        // E3: download only THIS invoice's id — the one the VM was constructed
        // with from the caller's own list/detail; never a synthesized id.
        switch await client.downloadInvoicePdf(id: invoiceId) {
        case let .success(url):
            pdfState = .idle
            presentPdf.send(url)
        case let .failure(error):
            snackbar.showApiError(error)
            pdfState = .idle
        }
    }
}
