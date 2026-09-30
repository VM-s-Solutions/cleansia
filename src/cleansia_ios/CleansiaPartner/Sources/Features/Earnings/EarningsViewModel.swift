import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class EarningsViewModel: ViewModel {
    @Published private(set) var state: UiState<DashboardStats> = .loading
    /// The company cash the cleaner holds, read beside the stats; empty until it lands or when it fails.
    @Published private(set) var cashHeld: [CashHeld] = []

    private let client: PartnerDashboardClient
    private let payrollClient: PartnerPayrollClient
    private let snackbar: SnackbarController

    init(client: PartnerDashboardClient, payrollClient: PartnerPayrollClient, snackbar: SnackbarController) {
        self.client = client
        self.payrollClient = payrollClient
        self.snackbar = snackbar
    }

    func load() async {
        if state.loadedValue == nil {
            state = .loading
        }

        async let cash = readCashHeld()
        let employeeId = await client.getCurrentEmployee().loadedEmployeeId

        let statsLoaded: Bool
        switch await client.getStats(employeeId: employeeId) {
        case let .success(stats):
            state = .loaded(stats)
            statsLoaded = true
        case let .failure(error):
            if state.loadedValue == nil {
                state = .error(error)
            }
            statsLoaded = false
        }

        // A screen that could not read its stats has already said so; the cash read failing too is not
        // a second message.
        switch await cash {
        case let .success(rows):
            cashHeld = rows
        case let .failure(error):
            if statsLoaded {
                snackbar.showApiError(error)
            }
        }
    }

    private func readCashHeld() async -> ApiResult<[CashHeld]> {
        await payrollClient.getCashHeld()
    }
}

private extension ApiResult where Success == EmployeeItem {
    var loadedEmployeeId: String? {
        try? get().id
    }
}
