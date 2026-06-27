import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class EmergencySectionViewModel: ViewModel {
    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published var name = ""
    @Published var phone = ""
    @Published private(set) var nameError: String?
    @Published private(set) var phoneError: String?

    let saved = PassthroughSubject<Void, Never>()

    private(set) var employeeId = ""

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
            name = employee.emergencyContactName ?? ""
            phone = employee.emergencyContactPhone ?? ""
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func save() async {
        guard !action.isSubmitting else { return }
        nameError = nil
        phoneError = nil

        var hasError = false
        if name.isBlank {
            nameError = L10n.Profile.errorEmergencyNameRequired
            hasError = true
        }
        if phone.isBlank {
            phoneError = L10n.Profile.errorEmergencyPhoneRequired
            hasError = true
        }
        if hasError { return }
        guard !employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }

        action = .submitting
        let command = UpdateEmergencyContactCommand(
            employeeId: employeeId,
            emergencyName: name.trimmed,
            emergencyPhone: phone.trimmed
        )
        switch await client.updateEmergencyContact(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }
}
