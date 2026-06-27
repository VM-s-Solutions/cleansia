import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class BankSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var ibanError: String?
    @Published var iban = "" {
        didSet { normalizeIban() }
    }

    let saved = PassthroughSubject<Void, Never>()

    private(set) var employeeId = ""
    private var normalizing = false

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            employeeId = employee.id ?? ""
            iban = employee.iban ?? ""
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func save() async {
        guard !action.isSubmitting else { return }
        ibanError = nil
        guard !iban.isBlank else {
            ibanError = L10n.Profile.errorIbanRequired
            return
        }
        guard !employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }

        action = .submitting
        let command = UpdateBankDetailsCommand(employeeId: employeeId, iban: iban.trimmed)
        switch await client.updateBankDetails(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }

    private func normalizeIban() {
        guard !normalizing else { return }
        let cleaned = iban.uppercased().filter { $0.isLetter || $0.isNumber }
        if cleaned != iban {
            normalizing = true
            iban = cleaned
            normalizing = false
        }
        ibanError = nil
    }
}
