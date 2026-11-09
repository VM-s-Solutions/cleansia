import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class InvoicesListViewModel: ViewModel {
    @Published private(set) var state: UiState<[Invoice]> = .loading
    @Published private(set) var refreshPhase: RefreshPhase = .idle

    private let client: PartnerPayrollClient
    private let staleness: InvoicesStaleness
    private let snackbar: SnackbarController
    private var inFlight = false
    private var employeeId: String?

    init(client: PartnerPayrollClient, staleness: InvoicesStaleness, snackbar: SnackbarController) {
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
    }

    /// On-appear / on-resume: skip the network when the cache is warm (no-flash
    /// resume from the invoice detail); else a silent background fetch (the
    /// `ensureFreshOrCachedAsync` parity). User pulls go through `userRefresh`.
    func onAppear() async {
        await ensureFreshOrCached()
    }

    func userRefresh() async {
        await fetch(phase: .userRefreshing)
    }

    /// **A warm watermark alone may not suppress the fetch.** `InvoicesStaleness` is built once in
    /// `PartnerAppContainer` and outlives this view model, whose `state` is rebuilt at `.loading` every
    /// time `InvoicesListView` is. Guarding on the watermark alone therefore left a fresh view model
    /// spinning with nothing on the way. Same rule as `OrdersListViewModel.ensureFreshOrCached`.
    private func ensureFreshOrCached() async {
        guard state.loadedValue == nil || staleness.isStale else { return }
        await fetch(phase: .backgroundRefreshing)
    }

    private func fetch(phase: RefreshPhase) async {
        guard !inFlight else { return }
        inFlight = true
        defer { inFlight = false }

        if phase == .userRefreshing {
            refreshPhase = .userRefreshing
        } else if refreshPhase == .idle {
            refreshPhase = .backgroundRefreshing
        }
        defer { refreshPhase = .idle }

        let result: ApiResult<[Invoice]> = switch await ownEmployeeId() {
        case let .success(id):
            await client.getPagedInvoices(employeeId: id)
        case let .failure(error):
            .failure(error)
        }

        switch result {
        case let .success(invoices):
            state = .loaded(invoices)
            staleness.markFresh()
        case let .failure(error):
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    /// Never unscoped: the server keeps an administrator's filter as sent, so a read without the caller's own id
    /// would list the company's invoices. Kept once read, so a refresh has one request fewer to fail on.
    private func ownEmployeeId() async -> ApiResult<String> {
        if let employeeId { return .success(employeeId) }
        let result = await client.currentEmployeeId()
        if case let .success(id) = result {
            employeeId = id
        }
        return result
    }
}
