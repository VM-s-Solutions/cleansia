import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class PeriodPayViewModel: ViewModel {
    @Published private(set) var state: UiState<PeriodPaySummaryDto> = .loading

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

        // E1/E2: resolve the caller's OWN employeeId server-side and pass only
        // that. A nil/unresolvable id never hits GetPeriodPays — no foreign-id
        // echo, no network call.
        guard case let .success(employeeId) = await client.currentEmployeeId() else {
            state = .error(ApiError(code: "payroll.employee_id_missing"))
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
