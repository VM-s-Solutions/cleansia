import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class PeriodPayViewModel: ViewModel {
    @Published private(set) var state: UiState<PeriodPaySummary> = .loading

    let currencyCode: String?

    private let payPeriodId: String
    private let client: PartnerPayrollClient
    private let snackbar: SnackbarController

    init(
        payPeriodId: String,
        currencyCode: String?,
        client: PartnerPayrollClient,
        snackbar: SnackbarController
    ) {
        self.payPeriodId = payPeriodId
        self.currencyCode = currencyCode
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading

        // E1/E2: resolve the caller's OWN employeeId server-side and pass only that. A
        // nil/unresolvable id never hits GetPeriodPays — no foreign-id echo, no network call.
        // The refusal is carried, never replaced: an expired session and a wire the server broke
        // are different failures, and substituting one guess for both reported the wrong subsystem
        // for every one of them.
        let employeeId: String
        switch await client.currentEmployeeId() {
        case let .success(id):
            employeeId = id
        case let .failure(error):
            snackbar.showApiError(error)
            state = .error(error)
            return
        }

        switch await client.getPeriodPays(employeeId: employeeId, payPeriodId: payPeriodId) {
        case let .success(summary):
            state = .loaded(summary)
        case let .failure(error):
            snackbar.showApiError(error)
            state = .error(error)
        }
    }
}
