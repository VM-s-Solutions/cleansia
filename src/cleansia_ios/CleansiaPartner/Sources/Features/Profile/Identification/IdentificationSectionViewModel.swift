import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class IdentificationSectionViewModel: ViewModel {
    struct Loaded: Equatable {
        var employeeId: String
        var nationalityId: String?
        var passportId: String
        var entityType: EmployeeEntityType
        var businessCountryId: String?
        var registrationNumber: String
        var vatNumber: String
        var legalEntityName: String
    }

    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published var form = Loaded(
        employeeId: "",
        nationalityId: nil,
        passportId: "",
        entityType: ._1,
        businessCountryId: nil,
        registrationNumber: "",
        vatNumber: "",
        legalEntityName: ""
    )
    @Published private(set) var countryOptions: [CleansiaDropdownOption] = []

    let saved = PassthroughSubject<Void, Never>()

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    var isLegalEntity: Bool {
        form.entityType == ._2
    }

    func load() async {
        state = .loading
        let countries = await (client.getAllCountries()).valueOrNil ?? []
        countryOptions = countries.compactMap { country in
            guard let id = country.id, let name = country.name else { return nil }
            return CleansiaDropdownOption(id: id, label: name)
        }
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            form = Loaded(
                employeeId: employee.id ?? "",
                nationalityId: employee.nationalityId,
                passportId: employee.passportId ?? "",
                entityType: employee.entityType ?? ._1,
                businessCountryId: employee.countryId,
                registrationNumber: employee.registrationNumber ?? "",
                vatNumber: employee.vatNumber ?? "",
                legalEntityName: employee.legalEntityName ?? ""
            )
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func setEntityType(_ type: EmployeeEntityType) {
        form.entityType = type
        if type != ._2 {
            form.legalEntityName = ""
        }
    }

    func save() async {
        guard !action.isSubmitting else { return }
        guard !form.employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }
        guard let nationalityId = form.nationalityId, !nationalityId.isBlank else {
            snackbar.showError(L10n.Profile.errorNationalityRequired)
            return
        }
        guard !form.passportId.isBlank else {
            snackbar.showError(L10n.Profile.errorPassportRequired)
            return
        }
        guard let businessCountryId = form.businessCountryId, !businessCountryId.isBlank else {
            snackbar.showError(L10n.Profile.errorBusinessCountryRequired)
            return
        }
        guard !form.registrationNumber.isBlank else {
            snackbar.showError(L10n.Profile.errorRegistrationNumberRequired)
            return
        }
        if isLegalEntity, form.legalEntityName.isBlank {
            snackbar.showError(L10n.Profile.errorLegalEntityNameRequired)
            return
        }

        action = .submitting
        let command = UpdateIdentificationInfoCommand(
            employeeId: form.employeeId,
            nationalityId: nationalityId,
            passportId: form.passportId.trimmed,
            entityType: form.entityType,
            businessCountryId: businessCountryId,
            registrationNumber: form.registrationNumber.trimmed,
            vatNumber: form.vatNumber.trimmedOrNil,
            legalEntityName: form.legalEntityName.trimmedOrNil
        )
        switch await client.updateIdentificationInfo(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
