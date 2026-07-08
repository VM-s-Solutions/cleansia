import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// Country-level slice of the Android `ServiceAreaStatus` tri-state law:
/// UNKNOWN (couldn't check) is a distinct state from "not serviced" and must
/// never be rendered — or acted on — as a block. The city-level states
/// (in/outside serviced city) need a serviced-cities endpoint the partner
/// client doesn't expose yet.
enum ServiceAreaStatus: Equatable {
    case unknown
    case countryServiced
    case countryNotServiced
}

@MainActor
final class AddressSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var picked: GeocodedAddress?
    @Published private(set) var serviceAreaStatus: ServiceAreaStatus = .unknown

    let saved = PassthroughSubject<Void, Never>()

    private(set) var employeeId = ""
    /// nil = the serviced-countries fetch failed (UNKNOWN) — not the same as
    /// a loaded-but-empty list, which is the server saying "none serviced".
    private var countries: [CountryListItem]?

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
        countries = await (client.getServicedCountries()).valueOrNil
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            employeeId = employee.id ?? ""
            picked = reconstructAddress(from: employee)
            refreshServiceAreaStatus()
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func applyPick(_ address: GeocodedAddress) {
        picked = address
        refreshServiceAreaStatus()
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
        if countries == nil {
            switch await client.getServicedCountries() {
            case let .success(fetched):
                countries = fetched
                refreshServiceAreaStatus()
            case let .failure(error):
                // The list never loaded, so the answer is UNKNOWN — surface
                // the fetch failure, never "country not serviced".
                snackbar.showError(localizer.message(for: error))
                return
            }
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
        let country = countries?.first { $0.id == employee.countryId }
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

    /// The geocoder gives alpha-2 ("sk"), the backend stores alpha-3 ("SVK");
    /// only the Core normaliser matches them — a prefix heuristic doesn't
    /// ("svk" does not start with "sk").
    private func resolveCountryId(for isoCode: String) -> String? {
        let code = IsoCountryCodes.toAlpha2(isoCode)
        guard !code.isEmpty else { return nil }
        return countries?.first { IsoCountryCodes.toAlpha2($0.isoCode) == code }?.id
    }

    private func refreshServiceAreaStatus() {
        guard let picked, let countries else {
            serviceAreaStatus = .unknown
            return
        }
        let code = IsoCountryCodes.toAlpha2(picked.countryIsoCode)
        guard !code.isEmpty else {
            serviceAreaStatus = .unknown
            return
        }
        serviceAreaStatus = countries.contains { IsoCountryCodes.toAlpha2($0.isoCode) == code }
            ? .countryServiced
            : .countryNotServiced
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
