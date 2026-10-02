import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class BankSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    private let czechPayout = MyPayoutDetails(
        bankCountryId: "country-cz",
        accountPrefix: "000019",
        accountNumber: "2000145399",
        bankCode: "0800",
        iban: "CZ6508000000192000145399"
    )

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
        client.employeeResult = .success(EmployeeItem(id: "emp-1", countryId: "country-cz"))
        client.allCountriesResult = .success([
            CountryListItem(id: "country-cz", isoCode: "CZE", name: "Czech Republic")
        ])
    }

    private func makeVM() -> BankSectionViewModel {
        BankSectionViewModel(client: client, snackbar: snackbar)
    }

    private func loadedForm(_ vm: BankSectionViewModel) throws -> BankForm {
        try XCTUnwrap(vm.state.loadedValue, "expected loaded")
    }

    func testLoadTransitionsToLoadedWithTheStoredPartsPaddingStripped() async throws {
        client.payoutResult = .success(czechPayout)
        let vm = makeVM()
        XCTAssertTrue(vm.state.isLoading)

        await vm.load()

        let form = try loadedForm(vm)
        XCTAssertEqual(form.employeeId, "emp-1")
        XCTAssertEqual(form.bankCountryId, "country-cz")
        XCTAssertEqual(form.accountPrefix, "19")
        XCTAssertEqual(form.accountNumber, "2000145399")
        XCTAssertEqual(form.bankCode, "0800")
        XCTAssertEqual(form.iban, "CZ6508000000192000145399")
    }

    func testACleanerWithNoPayoutDetailsYetGetsAnEmptyFormNotAnError() async throws {
        let vm = makeVM()
        await vm.load()

        let form = try loadedForm(vm)
        XCTAssertEqual(form.accountNumber, "")
        XCTAssertEqual(form.iban, "")
        // The bank is presumed to be in the country the cleaner lives in until they say otherwise.
        XCTAssertEqual(form.bankCountryId, "country-cz")
        XCTAssertNil(snackbar.current)
    }

    func testLoadFailureTransitionsToErrorAndSnackbars() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testAFailedPayoutReadIsAnErrorNotAnEmptyForm() async {
        client.payoutResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed read must not open an empty form") }
        XCTAssertNotNil(snackbar.current)
    }

    func testCountryLoadFailureStillLoadsTheForm() async throws {
        client.allCountriesResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        XCTAssertTrue(vm.countryOptions.isEmpty)
        XCTAssertEqual(try loadedForm(vm).employeeId, "emp-1")
    }

    func testTheLocalPartsAcceptDigitsOnly() async throws {
        let vm = makeVM()
        await vm.load()

        vm.update(\.accountPrefix, to: "19-")
        vm.update(\.accountNumber, to: "2000-145 399")
        vm.update(\.bankCode, to: "/0800")

        let form = try loadedForm(vm)
        XCTAssertEqual(form.accountPrefix, "19")
        XCTAssertEqual(form.accountNumber, "2000145399")
        XCTAssertEqual(form.bankCode, "0800")
    }

    func testIbanAndSwiftAreUpperCasedAndStrippedOfSeparators() async throws {
        let vm = makeVM()
        await vm.load()

        vm.update(\.iban, to: "cz65 0800 0000 1920 0014 5399")
        vm.update(\.swift, to: "gibacz-px")

        let form = try loadedForm(vm)
        XCTAssertEqual(form.iban, "CZ6508000000192000145399")
        XCTAssertEqual(form.swift, "GIBACZPX")
    }

    func testAnAccountWithNoIdentifierCannotBeSubmitted() async throws {
        let vm = makeVM()
        await vm.load()
        XCTAssertFalse(try loadedForm(vm).canSubmit)

        await vm.save()

        XCTAssertNil(client.bankCommand)
    }

    /// A CZ or SK bank takes the three parts; an IBAN alone is not its identifier.
    func testACzechBankTakesTheThreePartsAndAnIbanAloneDoesNotSubmit() async throws {
        let vm = makeVM()
        await vm.load()

        vm.update(\.iban, to: "CZ6508000000192000145399")

        XCTAssertTrue(try loadedForm(vm).usesDomesticAccount)
        XCTAssertFalse(try loadedForm(vm).canSubmit)
    }

    func testAnAccountNumberWithoutABankCountryCannotBeSubmitted() async throws {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()

        vm.update(\.accountNumber, to: "5885638003")

        let form = try loadedForm(vm)
        XCTAssertNil(form.bankCountryId)
        XCTAssertFalse(form.canSubmit)
    }

    /// Every field on the generated command is optional with a `nil` default, so a dropped
    /// mapper line compiles and leaves a field-by-field assertion green. Compare the WHOLE
    /// captured command: an omission then goes red on the struct, not on the one line
    /// somebody remembered to assert.
    ///
    /// A domestic account sends the parts and leaves the IBAN to the server: the stored IBAN is the one
    /// the server derived from the old parts, so sending it back with edited parts was refused as
    /// `validation.payout.iban_mismatch`.
    func testSaveSendsEveryFieldTheBackendNowTakes() async {
        let vm = makeVM()
        await vm.load()

        vm.update(\.bankCountryId, to: "country-cz")
        vm.update(\.accountPrefix, to: "19")
        vm.update(\.accountNumber, to: "2000145399")
        vm.update(\.bankCode, to: "0800")
        vm.update(\.iban, to: "CZ6508000000192000145399")
        vm.update(\.swift, to: "GIBACZPX")
        vm.update(\.bankName, to: " Česká spořitelna ")
        vm.update(\.holderName, to: " Jan Novák ")

        var emitted = false
        let token = vm.saved.sink { emitted = true }
        defer { token.cancel() }

        await vm.save()

        XCTAssertTrue(emitted)
        XCTAssertEqual(
            client.bankCommand,
            UpdateBankDetailsCommand(
                employeeId: "emp-1",
                iban: nil,
                bankCountryId: "country-cz",
                accountPrefix: "19",
                accountNumber: "2000145399",
                bankCode: "0800",
                swift: "GIBACZPX",
                bankName: "Česká spořitelna",
                holderName: "Jan Novák"
            )
        )
        XCTAssertFalse(vm.action.isSubmitting)
    }

    func testEmptyOptionalPartsAreSentAsNullNotAsEmptyStrings() async {
        let vm = makeVM()
        await vm.load()

        vm.update(\.accountNumber, to: "5885638003")
        vm.update(\.bankCode, to: "5500")

        await vm.save()

        XCTAssertEqual(
            client.bankCommand,
            UpdateBankDetailsCommand(
                employeeId: "emp-1",
                iban: nil,
                bankCountryId: "country-cz",
                accountPrefix: nil,
                accountNumber: "5885638003",
                bankCode: "5500",
                swift: nil,
                bankName: nil,
                holderName: nil
            )
        )
    }

    func testSaveFailureSnackbarsAndReturnsToIdle() async {
        client.bankUpdateResult = .failure(
            ApiError(code: "validation.payout.invalid_account_number", httpStatus: 400)
        )
        let vm = makeVM()
        await vm.load()
        vm.update(\.accountNumber, to: "5885638004")
        vm.update(\.bankCode, to: "5500")

        await vm.save()

        guard case .idle = vm.action else { return XCTFail("expected idle after a rejected save") }
        XCTAssertNotNil(snackbar.current)
    }

    func testSaveWithoutALoadedEmployeeIdSnackbarsInsteadOfCallingTheClient() async {
        client.employeeResult = .success(EmployeeItem(countryId: "country-cz"))
        let vm = makeVM()
        await vm.load()
        vm.update(\.accountNumber, to: "5885638003")
        vm.update(\.bankCode, to: "5500")

        await vm.save()

        XCTAssertNotNil(snackbar.current)
        XCTAssertNil(client.bankCommand)
    }

    func testCountriesBecomeDropdownOptions() async {
        let vm = makeVM()
        await vm.load()

        XCTAssertEqual(vm.countryOptions, [CleansiaDropdownOption(id: "country-cz", label: "Czech Republic")])
    }

    // MARK: A bank outside CZ and SK is entered as one IBAN (owner ruling 2026-10-02)

    private func loadWithEuropeanBanks() async -> BankSectionViewModel {
        client.allCountriesResult = .success([
            CountryListItem(id: "country-cz", isoCode: "CZE", isoAlpha2: "CZ", name: "Czech Republic"),
            CountryListItem(id: "country-sk", isoCode: "SVK", isoAlpha2: "SK", name: "Slovakia"),
            CountryListItem(id: "country-de", isoCode: "DEU", isoAlpha2: "DE", name: "Germany")
        ])
        let vm = makeVM()
        await vm.load()
        return vm
    }

    func testACzechOrSlovakBankKeepsThePartsAndAnyOtherTakesOneIban() async throws {
        let vm = await loadWithEuropeanBanks()
        XCTAssertTrue(try loadedForm(vm).usesDomesticAccount)
        vm.update(\.bankCountryId, to: "country-sk")
        XCTAssertTrue(try loadedForm(vm).usesDomesticAccount)

        vm.update(\.bankCountryId, to: "country-de")
        vm.update(\.accountNumber, to: "5885638003")
        XCTAssertFalse(try loadedForm(vm).usesDomesticAccount)
        XCTAssertFalse(try loadedForm(vm).canSubmit, "the parts are not this country's identifier")

        vm.update(\.iban, to: "DE89370400440532013000")
        XCTAssertTrue(try loadedForm(vm).canSubmit)
    }

    /// No country picked yet, or a country the list cannot name, keeps the three parts.
    func testABankCountryTheListCannotNameKeepsTheParts() async throws {
        let vm = makeVM()
        await vm.load()

        XCTAssertNil(try loadedForm(vm).bankCountryAlpha2, "the fixture's CZ carries no isoAlpha2")
        XCTAssertTrue(try loadedForm(vm).usesDomesticAccount)
    }

    func testAnIbanSaveSendsTheIbanWithoutTheParts() async {
        client.payoutResult = .success(czechPayout)
        let vm = await loadWithEuropeanBanks()

        vm.update(\.bankCountryId, to: "country-de")
        vm.update(\.iban, to: "de89 3704 0044 0532 0130 00")
        vm.update(\.swift, to: "cobadeff")
        await vm.save()

        XCTAssertEqual(
            client.bankCommand,
            UpdateBankDetailsCommand(
                employeeId: "emp-1",
                iban: "DE89370400440532013000",
                bankCountryId: "country-de",
                accountPrefix: nil,
                accountNumber: nil,
                bankCode: nil,
                swift: "COBADEFF",
                bankName: nil,
                holderName: nil
            )
        )
    }

    func testAnIbanTheServerWouldRefuseIsNamedUnderTheFieldInsteadOfBeingSent() async throws {
        let vm = await loadWithEuropeanBanks()
        vm.update(\.bankCountryId, to: "country-de")
        vm.update(\.iban, to: "DE89370400440532013001")
        XCTAssertFalse(try loadedForm(vm).ibanChecked, "no error before a save is tried")

        await vm.save()

        XCTAssertTrue(try loadedForm(vm).ibanChecked)
        XCTAssertEqual(try loadedForm(vm).ibanProblem, .invalid)
        XCTAssertNil(client.bankCommand)

        vm.update(\.iban, to: "DE89370400440532013000")
        XCTAssertNil(try loadedForm(vm).ibanProblem, "the error clears as the IBAN is fixed")
    }

    func testTheIbanIsCappedAtTheLongestIsoAllows() async throws {
        let vm = await loadWithEuropeanBanks()
        vm.update(\.iban, to: String(repeating: "A1", count: 20))

        XCTAssertEqual(try loadedForm(vm).iban.count, BankForm.ibanMaxLength)
    }

    func testAnIbanIsValidExactlyWhenTheServersIbanCalculatorTakesIt() {
        let valid = [
            "DE89370400440532013000": "DE",
            "GB82WEST12345698765432": "GB",
            "AT611904300234573201": "AT",
            "FR1420041010050500013M02606": "FR",
            "NL91ABNA0417164300": "NL",
            "PL61109010140000071219812874": "PL",
            "CH9300762011623852957": "CH",
            "UA213223130000026007233566001": "UA",
            // Not in the registry, so the generic 15–34 and the check digits decide, as on the server.
            "US64SVBKUS6S3300958879": "US"
        ]
        for (iban, country) in valid {
            XCTAssertNil(BankForm.ibanProblem(iban, bankCountryAlpha2: country), iban)
        }
        XCTAssertNil(
            BankForm.ibanProblem("de89 3704 0044 0532 0130 00", bankCountryAlpha2: "DE"),
            "spaces and lower case are a statement's, not a typo"
        )
    }

    func testEachRefusalNamesWhatTheCleanerCanFix() {
        XCTAssertEqual(BankForm.ibanProblem("CZ6508000000192000145399", bankCountryAlpha2: "DE"), .otherCountry)
        XCTAssertEqual(
            BankForm.ibanProblem("DE8937040044053201300", bankCountryAlpha2: "DE"),
            .wrongLength(expected: 22)
        )
        XCTAssertEqual(
            BankForm.ibanProblem("DE893704004405320130000", bankCountryAlpha2: "DE"),
            .wrongLength(expected: 22)
        )
        XCTAssertEqual(BankForm.ibanProblem("DE89370400440532013001", bankCountryAlpha2: "DE"), .invalid)
        XCTAssertEqual(BankForm.ibanProblem("0532013000", bankCountryAlpha2: "DE"), .invalid)
        XCTAssertEqual(BankForm.ibanProblem("", bankCountryAlpha2: "DE"), .invalid)
        XCTAssertEqual(
            BankForm.ibanProblem("DEX9370400440532013000", bankCountryAlpha2: "DE"),
            .invalid,
            "check digits are digits"
        )
        XCTAssertEqual(
            BankForm.ibanProblem("US64SVBK", bankCountryAlpha2: "US"),
            .invalid,
            "outside the registry the generic bound holds"
        )
    }

    /// The client's table is the server's: a length only one of them knows refuses on one side only.
    func testTheRegistryLengthsAreTheServers() throws {
        let source = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Cleansia.Core.Domain/Payouts/IbanCalculator.cs")
        let text = try String(contentsOf: source, encoding: .utf8)
        let table = try XCTUnwrap(
            text.components(separatedBy: "RegistryLengths").dropFirst().first?
                .components(separatedBy: "};").first,
            "RegistryLengths not found — the parser needs updating"
        )
        let regex = try NSRegularExpression(pattern: #"\["([A-Z]{2})"\]\s*=\s*(\d+)"#)
        var server: [String: Int] = [:]
        for match in regex.matches(in: table, range: NSRange(table.startIndex..., in: table)) {
            guard let code = Range(match.range(at: 1), in: table), let length = Range(match.range(at: 2), in: table)
            else { continue }
            server[String(table[code])] = Int(table[length])
        }
        XCTAssertFalse(server.isEmpty, "the parser found no registry entries")
        XCTAssertEqual(server, BankForm.ibanRegistryLengths)
    }

    func testAnIbanIsDrawnInGroupsOfFour() {
        XCTAssertEqual(BankForm.groupedIban("DE89370400440532013000"), "DE89 3704 0044 0532 0130 00")
        XCTAssertEqual(BankForm.groupedIban("DE89"), "DE89")
        XCTAssertEqual(BankForm.groupedIban("DE893"), "DE89 3")
        XCTAssertEqual(BankForm.groupedIban(""), "")
    }
}
