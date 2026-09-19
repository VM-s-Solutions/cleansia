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
        var businessCountryId: String?
        var registrationNumber: String
        /// Display only. An operator may onboard a company by hand, and what the server holds for
        /// such a row is shown, never edited or sent from here — a cleaner contracts as a natural
        /// person and the server refuses anything else on this write.
        var storedLegalEntityName: String?
    }

    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published var form = Loaded(
        employeeId: "",
        nationalityId: nil,
        passportId: "",
        businessCountryId: nil,
        registrationNumber: "",
        storedLegalEntityName: nil
    )
    @Published private(set) var countryOptions: [CleansiaDropdownOption] = []

    /// What the chosen BUSINESS country calls its registration number and VAT id.
    ///
    /// These have been in `CountryConfiguration` since it was seeded and nothing returned them, so the
    /// label was hardcoded to the Czech "IČO" in all five translation files — which a Polish or
    /// Ukrainian partner then read, and Poland has no IČO. `nil` means the platform holds no
    /// configuration for the country and the view falls back to its own neutral wording.
    @Published private(set) var fieldLabels: GetCountryFieldLabelsCountryFieldLabelsDto?

    /// The country the labels on screen belong to. Guards against a slow answer for an
    /// earlier pick overwriting a newer one when the picker is tapped twice.
    private var fieldLabelsFor: String?

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
        let countries = await (client.getAllCountries()).valueOrNil ?? []
        countryOptions = countries.compactMap { country in
            guard let id = country.id else { return nil }
            return CleansiaDropdownOption(id: id, label: country.localizedName())
        }
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            form = Loaded(
                employeeId: employee.id ?? "",
                nationalityId: employee.nationalityId,
                passportId: employee.passportId ?? "",
                businessCountryId: employee.countryId,
                registrationNumber: employee.registrationNumber ?? "",
                storedLegalEntityName: employee.entityType == ._2 ? employee.legalEntityName : nil
            )
            state = .loaded(())
            await loadFieldLabels(for: employee.countryId)
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    /// Silent on failure and silent on 404. A country we hold no configuration for is one whose word
    /// for this we do not know — the neutral fallback is the honest answer, and an error banner would
    /// be reporting a missing translation as a broken form.
    func loadFieldLabels(for countryId: String?) async {
        guard let countryId, !countryId.isBlank, fieldLabelsFor != countryId else { return }
        fieldLabelsFor = countryId
        let result = await client.getCountryFieldLabels(countryId: countryId)
        // Only publish if the picker has not moved on since this call was made.
        guard fieldLabelsFor == countryId else { return }
        // .flatMap, not `?? nil`: ApiResult<T?>.valueOrNil is a double optional and SwiftLint's
        // redundant_nil_coalescing is syntactic — it fires on the literal `?? nil` regardless.
        // ProfileViewModel does the same flatten on getMyPayoutDetails.
        fieldLabels = result.valueOrNil.flatMap { $0 }
    }

    func save() async {
        guard case .loaded = state, !action.isSubmitting else { return }
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

        action = .submitting
        let command = UpdateIdentificationInfoCommand(
            employeeId: form.employeeId,
            nationalityId: nationalityId,
            passportId: form.passportId.trimmed,
            entityType: ._1,
            businessCountryId: businessCountryId,
            registrationNumber: form.registrationNumber.trimmed,
            legalEntityName: nil
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
