import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class PeriodPayViewModel: ViewModel {
    @Published private(set) var state: UiState<PeriodPaySummary> = .loading

    /// The currency the screen was opened with — the Earnings tab's resolved code or an invoice's —
    /// which only labels the figures until the summary names its own (`PeriodPayScreen.kt` parity).
    let currencyCode: String?

    /// The currency view the server is asked for — the invoice's when opened from one, nil from the
    /// Earnings tab so the server answers in the cleaner's resolved currency.
    let currencyId: String?

    var displayCurrencyCode: String? {
        state.loadedValue?.currencyCode ?? currencyCode
    }

    private let payPeriodId: String
    private let client: PartnerPayrollClient
    private let snackbar: SnackbarController

    init(
        payPeriodId: String,
        currencyCode: String?,
        currencyId: String?,
        client: PartnerPayrollClient,
        snackbar: SnackbarController
    ) {
        self.payPeriodId = payPeriodId
        self.currencyCode = currencyCode
        self.currencyId = currencyId
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

        switch await client.getPeriodPays(employeeId: employeeId, payPeriodId: payPeriodId, currencyId: currencyId) {
        case let .success(summary):
            state = .loaded(summary)
        case let .failure(error):
            snackbar.showApiError(error)
            state = .error(error)
        }
    }
}
