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
    @Published private(set) var state: UiState<EmployeeInvoiceDetailDto> = .loading
    @Published private(set) var pdfState: PdfState = .idle

    /// One-shot: a downloaded local PDF URL ready for the view to present via
    /// QuickLook. The VM never presents/navigates itself.
    let presentPdf = PassthroughSubject<URL, Never>()

    private let invoiceId: String
    private let client: PartnerPayrollClient
    private let snackbar: SnackbarController

    init(invoiceId: String, client: PartnerPayrollClient, snackbar: SnackbarController) {
        self.invoiceId = invoiceId
        self.client = client
        self.snackbar = snackbar
    }

    /// True only when the backend produced a PDF for this invoice. Guards the
    /// "Open invoice PDF" affordance — a failed generation has nothing to open.
    var canOpenPdf: Bool {
        guard let invoice = state.loadedValue else { return false }
        return !(invoice.pdfGenerationFailed ?? false)
    }

    func load() async {
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
        guard pdfState == .idle else { return }
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
