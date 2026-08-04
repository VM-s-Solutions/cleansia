import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

struct ProfileData: Equatable {
    let employee: EmployeeItem
    let contractStatus: ContractStatus?
    var payoutSummary: String?
}

@MainActor
final class ProfileViewModel: ViewModel {
    @Published private(set) var state: UiState<ProfileData> = .loading
    @Published private(set) var action: ActionState = .idle

    let signedOut = PassthroughSubject<Void, Never>()

    private let client: PartnerProfileClient
    private let authClient: AuthClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerProfileClient, authClient: AuthClient, snackbar: SnackbarController) {
        self.client = client
        self.authClient = authClient
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            // Status and payout destination are fire-and-forget — a failed fetch just hides
            // the chip and leaves the bank row on its placeholder.
            let status = await (client.checkCurrentEmployee()).valueOrNil
            let payout = await (client.getMyPayoutDetails()).valueOrNil.flatMap { $0 }
            state = .loaded(ProfileData(
                employee: employee,
                contractStatus: status?.contractStatus,
                payoutSummary: PayoutAccountSummary.text(for: payout)
            ))
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func signOut() async {
        guard !action.isSubmitting else { return }
        action = .submitting
        await authClient.logout()
        action = .idle
        signedOut.send()
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
