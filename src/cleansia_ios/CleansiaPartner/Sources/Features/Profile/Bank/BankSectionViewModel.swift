import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// The payout destination is captured as the parts a Czech or Slovak cleaner reads off their
/// statement — prefix, account number, bank code — because the server derives the IBAN from
/// them. Everything the server can check (the account checksum, the bank code, whether a
/// supplied IBAN agrees, whether a card number was typed in) is left to the server: it owns
/// the rules, its answers are wired to `error.validation.payout.*`, and a second copy of a
/// checksum here would gate a cleaner's income on a client that disagrees.
struct BankForm: Equatable {
    var employeeId = ""
    var bankCountryId: String?
    var accountPrefix = "" {
        didSet { accountPrefix = accountPrefix.payoutDigits }
    }

    var accountNumber = "" {
        didSet { accountNumber = accountNumber.payoutDigits }
    }

    var bankCode = "" {
        didSet { bankCode = bankCode.payoutDigits }
    }

    var iban = "" {
        didSet { iban = iban.bankReference }
    }

    var swift = "" {
        didSet { swift = swift.bankReference }
    }

    var bankName = ""
    var holderName = ""

    /// The bank's country plus something that identifies the account — the rest is the server's call.
    var canSubmit: Bool {
        guard let bankCountryId, !bankCountryId.isBlank else { return false }
        return !accountNumber.isBlank || !iban.isBlank
    }
}

@MainActor
final class BankSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<BankForm> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var countryOptions: [CleansiaDropdownOption] = []

    let saved = PassthroughSubject<Void, Never>()

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        countryOptions = await (client.getAllCountries()).valueOrNil?.compactMap(Self.option) ?? []

        let employee: EmployeeItem
        switch await client.getCurrentEmployee() {
        case let .success(value):
            employee = value
        case let .failure(error):
            return failLoad(error)
        }

        let payout: MyPayoutDetails?
        switch await client.getMyPayoutDetails() {
        case let .success(value):
            payout = value
        case let .failure(error):
            return failLoad(error)
        }

        state = .loaded(BankForm(
            employeeId: employee.id ?? "",
            // The bank is usually in the country the cleaner lives in, so pre-fill it and
            // let them change it — the same zero-tap default the identification section uses.
            bankCountryId: payout?.bankCountryId ?? employee.countryId,
            accountPrefix: PayoutAccountSummary.withoutPadding(payout?.accountPrefix),
            accountNumber: PayoutAccountSummary.withoutPadding(payout?.accountNumber),
            bankCode: payout?.bankCode ?? "",
            iban: payout?.iban ?? "",
            swift: payout?.swift ?? "",
            bankName: payout?.bankName ?? "",
            holderName: payout?.holderName ?? ""
        ))
    }

    func update<Value>(_ keyPath: WritableKeyPath<BankForm, Value>, to value: Value) {
        guard case var .loaded(form) = state else { return }
        form[keyPath: keyPath] = value
        state = .loaded(form)
    }

    func save() async {
        guard case let .loaded(form) = state, !action.isSubmitting else { return }
        guard !form.employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }
        guard form.canSubmit else { return }

        action = .submitting
        let command = UpdateBankDetailsCommand(
            employeeId: form.employeeId,
            iban: form.iban.trimmedOrNil,
            bankCountryId: form.bankCountryId,
            accountPrefix: form.accountPrefix.trimmedOrNil,
            accountNumber: form.accountNumber.trimmedOrNil,
            bankCode: form.bankCode.trimmedOrNil,
            swift: form.swift.trimmedOrNil,
            bankName: form.bankName.trimmedOrNil,
            holderName: form.holderName.trimmedOrNil
        )
        switch await client.updateBankDetails(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            action = .idle
            snackbar.showApiError(error)
        }
    }

    private func failLoad(_ error: ApiError) {
        snackbar.showApiError(error)
        state = .error(error)
    }

    private static func option(_ country: CountryListItem) -> CleansiaDropdownOption? {
        guard let id = country.id else { return nil }
        return CleansiaDropdownOption(id: id, label: country.localizedName())
    }
}

private extension String {
    var payoutDigits: String {
        filter(\.isNumber)
    }

    var bankReference: String {
        uppercased().filter { $0.isLetter || $0.isNumber }
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
