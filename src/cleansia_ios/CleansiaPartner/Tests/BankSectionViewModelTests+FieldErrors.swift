import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Every refusal is named under the field it is about — the device's checks and the server's answers
/// alike — and clears as that field is edited.
@MainActor
extension BankSectionViewModelTests {
    func message(_ key: String) -> String {
        ApiErrorLocalizer().message(for: ApiError(code: key))
    }

    func testABankCountryAndAnAccountNumberAreBothNamedInOnePass() async throws {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()

        await vm.save()

        XCTAssertEqual(try loadedForm(vm).bankCountryError, message("common.required"))
        XCTAssertEqual(try loadedForm(vm).accountError, message("validation.payout.account_number_required"))
        XCTAssertNil(client.bankCommand)

        vm.update(\.bankCountryId, to: "country-cz")
        XCTAssertNil(try loadedForm(vm).bankCountryError, "picking a country clears its error")
        XCTAssertNotNil(try loadedForm(vm).accountError)
    }

    /// The server's own rule: a bank code is exactly four ASCII digits.
    func testABankCodeThatIsNotFourDigitsIsRefusedOnTheDevice() async throws {
        let vm = makeVM()
        await vm.load()
        vm.update(\.accountNumber, to: "5885638003")

        for bankCode in ["080", ""] {
            vm.update(\.bankCode, to: bankCode)
            await vm.save()
            XCTAssertEqual(try loadedForm(vm).accountError, message("validation.payout.invalid_bank_code"), bankCode)
        }
        XCTAssertNil(client.bankCommand)
    }

    func testEditingAnyPartOfTheAccountClearsItsError() async throws {
        let vm = makeVM()
        await vm.load()
        for part in [\BankForm.accountPrefix, \BankForm.accountNumber, \BankForm.bankCode] {
            await vm.save()
            XCTAssertNotNil(try loadedForm(vm).accountError)
            vm.update(part, to: "1")
            XCTAssertNil(try loadedForm(vm).accountError)
            vm.update(part, to: "")
        }
    }

    /// A binding that writes back the value it already holds has fixed nothing.
    func testWritingTheSameValueBackKeepsTheError() async throws {
        let vm = makeVM()
        await vm.load()
        await vm.save()

        vm.update(\.accountNumber, to: "")

        XCTAssertNotNil(try loadedForm(vm).accountError)
    }

    func testEachPayoutRefusalIsNamedUnderTheFieldItIsAbout() async throws {
        let cases: [(key: String, field: KeyPath<BankForm, String?>)] = [
            ("validation.payout.swift_required", \.swiftError),
            ("validation.payout.invalid_swift", \.swiftError),
            ("validation.payout.country_not_supported", \.bankCountryError),
            ("validation.payout.scheme_not_supported", \.bankCountryError),
            ("validation.payout.looks_like_card", \.accountError),
            ("validation.payout.iban_mismatch", \.accountError),
            ("validation.payout.invalid_account_prefix", \.accountError)
        ]
        for refusal in cases {
            client.bankUpdateResult = .failure(ApiError(code: refusal.key, httpStatus: 400, keys: [refusal.key]))
            let vm = makeVM()
            await vm.load()
            vm.update(\.accountNumber, to: "5885638003")
            vm.update(\.bankCode, to: "5500")

            await vm.save()

            XCTAssertEqual(try loadedForm(vm)[keyPath: refusal.field], message(refusal.key), refusal.key)
        }
        XCTAssertNil(snackbar.current)
    }

    /// A save answers for itself: once it passes the device's checks, what the server refused about another
    /// field last time is off the screen, so a new refusal is never shown beside a stale one.
    func testASaveThatPassesTheDeviceChecksClearsTheServersEarlierRefusals() async throws {
        let vm = await loadWithEuropeanBanks()
        vm.update(\.bankCountryId, to: "country-de")
        vm.update(\.iban, to: "DE89370400440532013000")
        client.bankUpdateResult = .failure(ApiError(code: "validation.payout.swift_required", httpStatus: 400))
        await vm.save()
        XCTAssertEqual(try loadedForm(vm).swiftError, message("validation.payout.swift_required"))

        client.bankUpdateResult = .failure(ApiError(code: "validation.payout.looks_like_card", httpStatus: 400))
        await vm.save()

        XCTAssertNil(try loadedForm(vm).swiftError, "the SWIFT refusal answered the save before")
        XCTAssertEqual(try loadedForm(vm).ibanServerError, message("validation.payout.looks_like_card"))
    }

    /// Whether a SWIFT is needed, and which one, depends on the bank's country, so the server's refusal of it
    /// no longer holds once that country changes.
    func testARefusedSwiftClearsWhenTheBankCountryChanges() async throws {
        let vm = await loadWithEuropeanBanks()
        vm.update(\.bankCountryId, to: "country-de")
        vm.update(\.iban, to: "DE89370400440532013000")
        client.bankUpdateResult = .failure(ApiError(code: "validation.payout.swift_required", httpStatus: 400))
        await vm.save()
        XCTAssertEqual(try loadedForm(vm).swiftError, message("validation.payout.swift_required"))

        vm.update(\.bankCountryId, to: "country-de")
        XCTAssertNotNil(try loadedForm(vm).swiftError, "picking the same country again changes nothing")

        vm.update(\.bankCountryId, to: "country-cz")
        XCTAssertNil(try loadedForm(vm).swiftError)
    }

    func testAnIbanTheServerRefusesIsNamedUnderTheIbanAndClearsAsItIsEdited() async throws {
        client.bankUpdateResult = .failure(
            ApiError(code: "validation.payout.looks_like_card", httpStatus: 400)
        )
        let vm = await loadWithEuropeanBanks()
        vm.update(\.bankCountryId, to: "country-de")
        vm.update(\.iban, to: "DE89370400440532013000")

        await vm.save()

        XCTAssertEqual(try loadedForm(vm).ibanServerError, message("validation.payout.looks_like_card"))
        XCTAssertNil(try loadedForm(vm).accountError)
        XCTAssertNil(snackbar.current)

        vm.update(\.iban, to: "DE8937040044053201300")
        XCTAssertNil(try loadedForm(vm).ibanServerError)
    }

    func testARefusalNoFieldOwnsStaysASnackbar() async {
        client.bankUpdateResult = .failure(ApiError(code: "employee.not_allowed_to_update", httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        vm.update(\.accountNumber, to: "5885638003")
        vm.update(\.bankCode, to: "5500")

        await vm.save()

        XCTAssertNotNil(snackbar.current)
        guard case .idle = vm.action else { return XCTFail("expected idle after a rejected save") }
    }

    /// An empty IBAN is named under the field like any other the server would refuse.
    func testAnEmptyIbanIsNamedInsteadOfDoingNothing() async throws {
        let vm = await loadWithEuropeanBanks()
        vm.update(\.bankCountryId, to: "country-de")

        await vm.save()

        XCTAssertTrue(try loadedForm(vm).ibanChecked)
        XCTAssertEqual(try loadedForm(vm).ibanProblem, .invalid)
        XCTAssertNil(client.bankCommand)
    }
}
