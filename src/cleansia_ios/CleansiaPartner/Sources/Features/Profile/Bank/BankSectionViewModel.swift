import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// The payout destination. A Czech or Slovak cleaner enters the parts they read off their statement —
/// prefix, account number, bank code — and the server derives the IBAN, so everything about those parts
/// (the account checksum, the bank code, a card number typed in) is left to it, and its answers are
/// wired to `error.validation.payout.*`. Any other bank country takes one IBAN (owner ruling
/// 2026-10-02), checked here as the server's IbanCalculator checks it (`ibanProblem`) so a typo is named
/// before the round trip; the server still checks it again. Android's `BankForm`, case for case.
struct BankForm: Equatable {
    /// Why the server's IbanCalculator would refuse an IBAN for the bank country picked (ADR-0034 D4).
    enum IbanProblem: Equatable {
        /// Not an IBAN's shape, or its check digits do not add up: the server's `validation.payout.invalid_iban`.
        case invalid
        /// Another country's IBAN: the server's `validation.payout.iban_country_mismatch`.
        case otherCountry
        /// The country's IBANs are `expected` characters long. The server folds this into `invalid_iban`.
        case wrongLength(expected: Int)
    }

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
        didSet { iban = String(iban.bankReference.prefix(Self.ibanMaxLength)) }
    }

    var swift = "" {
        didSet { swift = swift.bankReference }
    }

    var bankName = ""
    var holderName = ""

    /// The bank country's ISO code, which its IBANs start with. Set by the view model from the country
    /// list, so it follows `bankCountryId`.
    var bankCountryAlpha2: String?

    /// Whether a save has been tried, after which the IBAN's problem shows under it as it is edited.
    var ibanChecked = false

    /// The bank countries whose accounts are entered as `prefix – number / bank code`: the server's
    /// CzskDomesticWithIban scheme, which derives the IBAN from those parts (ADR-0034 D5.2). The paste
    /// splitter in CleansiaBankAccountField reads a CZ or SK IBAN into the parts for the same two.
    private static let domesticAccountCountries: Set<String> = ["CZ", "SK"]

    /// Whether the account is entered as the three domestic parts rather than an IBAN: a CZ or SK bank,
    /// and a country the list cannot name (no country picked yet, or the list failed to load).
    var usesDomesticAccount: Bool {
        bankCountryAlpha2.map(Self.domesticAccountCountries.contains) ?? true
    }

    /// What the server would refuse about the IBAN; nil when it would take it, or for a domestic account.
    var ibanProblem: IbanProblem? {
        guard !usesDomesticAccount, let bankCountryAlpha2 else { return nil }
        return Self.ibanProblem(iban, bankCountryAlpha2: bankCountryAlpha2)
    }

    /// The bank's country plus the account in the form that country takes.
    var canSubmit: Bool {
        guard let bankCountryId, !bankCountryId.isBlank else { return false }
        return usesDomesticAccount ? !accountNumber.isBlank : !iban.isBlank
    }

    /// ISO 13616 registry lengths, the server's `IbanCalculator.RegistryLengths` entry for entry
    /// (BankSectionViewModelTests reads the C# table). A country absent from it takes the generic 15–34.
    static let ibanRegistryLengths: [String: Int] = [
        "AT": 20, "BE": 16, "BG": 22, "CH": 21, "CY": 28, "CZ": 24, "DE": 22, "DK": 18, "EE": 20,
        "ES": 24, "FI": 18, "FR": 27, "GB": 22, "GR": 27, "HR": 21, "HU": 28, "IE": 22, "IT": 27,
        "LT": 20, "LU": 20, "LV": 21, "MT": 31, "NL": 18, "NO": 15, "PL": 28, "PT": 25, "RO": 24,
        "SE": 24, "SI": 19, "SK": 24, "UA": 29
    ]

    /// The longest IBAN ISO 13616 allows, and the field's cap.
    static let ibanMaxLength = 34

    /// What the server would refuse about `iban` for a bank in `bankCountryAlpha2`, in the order a
    /// cleaner can act on it: another country's IBAN first, then a length that is not the country's, then
    /// the shape and the ISO 7064 mod-97 check digits. Nil when the server's IbanCalculator would take it.
    static func ibanProblem(_ iban: String, bankCountryAlpha2: String) -> IbanProblem? {
        let value = Array(iban.bankReference)
        guard value.count >= 2, value[0].isAsciiUppercaseLetter, value[1].isAsciiUppercaseLetter else {
            return .invalid
        }
        let country = String(value[0 ..< 2])
        guard country == bankCountryAlpha2.uppercased() else { return .otherCountry }
        if let expected = ibanRegistryLengths[country], value.count != expected {
            return .wrongLength(expected: expected)
        }
        let shaped = (15 ... ibanMaxLength).contains(value.count)
            && value[2].isAsciiDigit && value[3].isAsciiDigit
            && value.allSatisfy { $0.isAsciiUppercaseLetter || $0.isAsciiDigit }
        guard shaped else { return .invalid }
        return mod97(Array(value[4...] + value[0 ..< 4])) == 1 ? nil : .invalid
    }

    /// `DE89370400440532013000` drawn as `DE89 3704 0044 0532 0130 00`, as statements print it.
    static func groupedIban(_ iban: String) -> String {
        var grouped = ""
        for (index, character) in iban.enumerated() {
            if index > 0, index.isMultiple(of: 4) { grouped.append(" ") }
            grouped.append(character)
        }
        return grouped
    }

    /// ISO 7064 MOD 97-10 over an IBAN rearranged to `BBAN + country + check digits`, A = 10 … Z = 35.
    private static func mod97(_ characters: [Character]) -> Int {
        characters.reduce(0) { remainder, character in
            let ascii = Int(character.asciiValue ?? 0)
            return character.isAsciiDigit
                ? (remainder * 10 + ascii - 48) % 97
                : (remainder * 100 + ascii - 55) % 97
        }
    }
}

@MainActor
final class BankSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<BankForm> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var countryOptions: [CleansiaDropdownOption] = []
    /// Country id → ISO alpha-2, the code its IBANs start with.
    private var countryAlpha2: [String: String] = [:]

    let saved = PassthroughSubject<Void, Never>()

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        let countries = await (client.getAllCountries()).valueOrNil ?? []
        countryOptions = countries.compactMap(Self.option)
        countryAlpha2 = Dictionary(
            countries.compactMap { country in
                country.id.flatMap { id in country.isoAlpha2?.trimmedOrNil.map { (id, $0.uppercased()) } }
            },
            uniquingKeysWith: { first, _ in first }
        )

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

        state = .loaded(withBankCountryCode(BankForm(
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
        )))
    }

    func update<Value>(_ keyPath: WritableKeyPath<BankForm, Value>, to value: Value) {
        guard case var .loaded(form) = state else { return }
        form[keyPath: keyPath] = value
        state = .loaded(withBankCountryCode(form))
    }

    private func withBankCountryCode(_ form: BankForm) -> BankForm {
        var form = form
        form.bankCountryAlpha2 = form.bankCountryId.flatMap { countryAlpha2[$0] }
        return form
    }

    func save() async {
        guard case let .loaded(form) = state, !action.isSubmitting else { return }
        guard !form.employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }
        guard form.canSubmit else { return }
        guard form.ibanProblem == nil else {
            update(\.ibanChecked, to: true)
            return
        }

        action = .submitting
        // Each scheme sends only its own identifier. A domestic IBAN is the server's to derive, and the
        // stored one sent back with edited parts was refused as a mismatch; an IBAN account has no parts.
        let domestic = form.usesDomesticAccount
        let command = UpdateBankDetailsCommand(
            employeeId: form.employeeId,
            iban: domestic ? nil : form.iban.trimmedOrNil,
            bankCountryId: form.bankCountryId,
            accountPrefix: domestic ? form.accountPrefix.trimmedOrNil : nil,
            accountNumber: domestic ? form.accountNumber.trimmedOrNil : nil,
            bankCode: domestic ? form.bankCode.trimmedOrNil : nil,
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

private extension Character {
    var isAsciiUppercaseLetter: Bool {
        isASCII && isUppercase
    }

    var isAsciiDigit: Bool {
        isASCII && isNumber
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
