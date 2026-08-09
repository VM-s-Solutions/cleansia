import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class JobRadiusSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var form = JobRadiusForm(radiusKm: nil)

    let saved = PassthroughSubject<Int?, Never>()

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
            form = JobRadiusForm(radiusKm: employee.jobRadiusKm)
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func setLimited(_ limited: Bool) {
        form.setLimited(limited)
    }

    func setKilometres(_ value: Double) {
        form.setKilometres(value)
    }

    func save() async {
        guard case .loaded = state, !action.isSubmitting else { return }
        guard !employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }

        action = .submitting
        let command = UpdateJobRadiusCommand(employeeId: employeeId, radiusKm: form.selection.wireValue)
        switch await client.updateJobRadius(command) {
        case let .success(radiusKm):
            action = .idle
            form = JobRadiusForm(radiusKm: radiusKm)
            saved.send(radiusKm)
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }
}
