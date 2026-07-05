import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class PersonalSectionViewModel: ViewModel {
    struct Loaded: Equatable {
        var employeeId: String
        var firstName: String
        var lastName: String
        var birthDate: Date?
        var phone: String
        var email: String
        var firstNameError: String?
        var lastNameError: String?
    }

    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published var form = Loaded(
        employeeId: "",
        firstName: "",
        lastName: "",
        birthDate: nil,
        phone: "",
        email: ""
    )

    let saved = PassthroughSubject<Void, Never>()

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
            form = Loaded(
                employeeId: employee.id ?? "",
                firstName: employee.firstName ?? "",
                lastName: employee.lastName ?? "",
                birthDate: employee.birthDate?.wrappedDate,
                phone: employee.phoneNumber ?? "",
                email: employee.email ?? ""
            )
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func save() async {
        guard !action.isSubmitting else { return }
        form.firstNameError = nil
        form.lastNameError = nil

        var hasError = false
        if form.firstName.isBlank {
            form.firstNameError = L10n.Profile.errorFirstNameRequired
            hasError = true
        }
        if form.lastName.isBlank {
            form.lastNameError = L10n.Profile.errorLastNameRequired
            hasError = true
        }
        if hasError { return }
        guard !form.employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }

        action = .submitting
        let command = UpdatePersonalInfoCommand(
            employeeId: form.employeeId,
            firstName: form.firstName.trimmed,
            lastName: form.lastName.trimmed,
            birthDate: OpenAPIDateWithoutTime(wrappedDate: form.birthDate),
            phone: form.phone.trimmedOrNil,
            email: form.email.trimmedOrNil
        )
        switch await client.updatePersonalInfo(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }
}
