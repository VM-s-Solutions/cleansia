import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class InvoicesListViewModel: ViewModel {
    @Published private(set) var state: UiState<[EmployeeInvoiceDto]> = .loading
    @Published private(set) var refreshPhase: RefreshPhase = .idle

    private let client: PartnerPayrollClient
    private let staleness: InvoicesStaleness
    private let snackbar: SnackbarController
    private var inFlight = false

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

    private func ensureFreshOrCached() async {
        guard staleness.isStale else { return }
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

        // E1: resolve the caller's OWN employeeId server-side and query only
        // that. An unresolvable id yields an empty list — never a blind or
        // foreign-scoped query.
        guard case let .success(employeeId) = await client.currentEmployeeId() else {
            state = .loaded([])
            return
        }

        switch await client.getPagedInvoices(employeeId: employeeId) {
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
}
