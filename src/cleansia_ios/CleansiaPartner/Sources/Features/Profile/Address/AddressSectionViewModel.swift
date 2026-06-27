import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class AddressSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var picked: GeocodedAddress?

    let saved = PassthroughSubject<Void, Never>()

    private(set) var employeeId = ""
    private var countries: [CountryListItem] = []

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    var summaryLine1: String? {
        guard let picked else { return nil }
        let street = picked.street.isBlank
            ? String(picked.formatted.prefix(while: { $0 != "," }))
            : picked.street
        return street.isBlank ? nil : street
    }

    var summaryLine2: String? {
        guard let picked else { return nil }
        let parts = [picked.zipCode, picked.city, picked.country].filter { !$0.isBlank }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    var canSave: Bool {
        picked != nil && !action.isSubmitting
    }

    func load() async {
        state = .loading
        countries = await (client.getServicedCountries()).valueOrNil ?? []
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            employeeId = employee.id ?? ""
            picked = reconstructAddress(from: employee)
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func applyPick(_ address: GeocodedAddress) {
        picked = address
    }

    func save() async {
        guard !action.isSubmitting else { return }
        guard let picked else {
            snackbar.showError(L10n.Profile.errorAddressNotPicked)
            return
        }
        guard !employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }
        guard let countryId = resolveCountryId(for: picked.countryIsoCode) else {
            snackbar.showError(L10n.Profile.errorCountryNotServiced)
            return
        }

        action = .submitting
        let hasCoords = picked.latitude != 0 && picked.longitude != 0
        let command = UpdateAddressInfoCommand(
            employeeId: employeeId,
            street: picked.street.trimmed,
            city: picked.city.trimmed,
            zipCode: picked.zipCode.trimmed,
            countryId: countryId,
            state: nil,
            latitude: hasCoords ? picked.latitude : nil,
            longitude: hasCoords ? picked.longitude : nil
        )
        switch await client.updateAddressInfo(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }

    private func reconstructAddress(from employee: EmployeeItem) -> GeocodedAddress? {
        guard let street = employee.street, !street.isBlank else { return nil }
        let country = countries.first { $0.id == employee.countryId }
        return GeocodedAddress(
            latitude: 0,
            longitude: 0,
            street: street,
            city: employee.city ?? "",
            zipCode: employee.zipCode ?? "",
            country: country?.name ?? "",
            countryIsoCode: country?.isoCode ?? "",
            formatted: [employee.street, employee.city, employee.zipCode]
                .compactMap { $0 }
                .filter { !$0.isBlank }
                .joined(separator: ", ")
        )
    }

    private func resolveCountryId(for isoCode: String) -> String? {
        let code = isoCode.lowercased()
        guard !code.isEmpty else { return nil }
        return countries.first { country in
            let iso = (country.isoCode ?? "").lowercased()
            return iso == code || iso.hasPrefix(code)
        }?.id
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
