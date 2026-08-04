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

    func testAnIbanOnItsOwnIsSubmittable() async throws {
        let vm = makeVM()
        await vm.load()

        vm.update(\.iban, to: "DE89370400440532013000")

        XCTAssertTrue(try loadedForm(vm).canSubmit)
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
                iban: "CZ6508000000192000145399",
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
}
