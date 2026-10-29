import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class PersonalSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    // swiftlint:disable calendar_day_needs_greenwich
    /// Greenwich, because that is what a value decoded off the wire carries — the generated decoder
    /// stores the day at midnight UTC and stamps the formatter's zone, never the handset's. Built the way
    /// the decoder builds it, so through the generated initializer.
    private let someBirthDate = OpenAPIDateWithoutTime(
        wrappedDate: Date(timeIntervalSince1970: 662_688_000),
        timezone: .gmt
    )
    // swiftlint:enable calendar_day_needs_greenwich

    private func makeVM() -> PersonalSectionViewModel {
        PersonalSectionViewModel(client: client, snackbar: snackbar)
    }

    func testLoadSuccessMapsFields() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            email: "jana@example.com",
            firstName: "Jana",
            lastName: "Nováková",
            phoneNumber: "+420123",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("expected loaded") }
        XCTAssertEqual(vm.form.employeeId, "emp-1")
        XCTAssertEqual(vm.form.firstName, "Jana")
        XCTAssertEqual(vm.form.lastName, "Nováková")
        XCTAssertEqual(vm.form.email, "jana@example.com")
        XCTAssertEqual(vm.form.phone, "+420123")
        XCTAssertEqual(vm.form.birthDate, someBirthDate.wrappedDate)
    }

    func testLoadFailureSetsErrorAndSnackbars() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    /// The employee id survives a failed reload, so nothing else stops the command from going out
    /// carrying whatever the form happens to hold — over a profile we could not read.
    func testAFailedReloadRefusesToSaveTheStaleForm() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "Nováková",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()

        client.employeeResult = .failure(ApiError(httpStatus: 500))
        await vm.load()
        await vm.save()

        XCTAssertNil(client.personalCommand)
        XCTAssertEqual(vm.action, .idle)
    }

    func testRetryingAFailedLoadRecoversTheForm() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "Jana", lastName: "Nováková"))
        await vm.load()

        guard case .loaded = vm.state else { return XCTFail("retry left the section in the error state") }
        XCTAssertEqual(vm.form.firstName, "Jana")
    }

    func testSaveSuccessEmitsSavedEffect() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "N",
            phoneNumber: "+420777111222",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()

        var emitted = false
        let token = vm.saved.sink { emitted = true }
        defer { token.cancel() }

        await vm.save()
        XCTAssertTrue(emitted)
        XCTAssertEqual(vm.action, .idle)
        XCTAssertEqual(client.personalCommand?.firstName, "Jana")
    }

    func testSaveValidationFailureSetsFieldErrorAndSkipsNetwork() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "", lastName: ""))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        XCTAssertNotNil(vm.form.firstNameError)
        XCTAssertNotNil(vm.form.lastNameError)
        XCTAssertNotNil(vm.form.birthDateError)
        XCTAssertEqual(vm.form.phoneError, L10n.Profile.errorPhoneRequired)
        XCTAssertNil(client.personalCommand)
        XCTAssertNil(snackbar.current, "a field error is shown on its field, not in a snackbar")
    }

    func testSaveWithoutBirthDateSetsFieldErrorAndSkipsNetwork() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "Jana", lastName: "N"))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        XCTAssertNotNil(vm.form.birthDateError)
        XCTAssertNil(vm.form.firstNameError)
        XCTAssertNil(vm.form.lastNameError)
        XCTAssertNil(client.personalCommand)
        XCTAssertEqual(vm.action, .idle)
    }

    /// Asserted as the DAY, not as another `OpenAPIDateWithoutTime`: that type's `==` reads only
    /// `wrappedDate`, which every initializer stores untouched, so comparing two of them holds whatever
    /// zone the command was built with — including the one that sends the day before.
    func testSaveSendsTheBirthDayItselfOnTheCommand() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "N",
            phoneNumber: "+420777111222",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        XCTAssertEqual(client.personalCommand?.birthDate?.rawValue, "1991-01-01")
        XCTAssertNil(vm.form.birthDateError)
    }

    /// The command reads the day in Greenwich whatever instant the form is holding — it must not depend
    /// on the picker having normalized it. An instant late in a UTC day is the case that separates the
    /// two: re-offsetting it by any positive device offset rolls it into the next day.
    func testSaveSendsTheDayEvenWhenTheStoredInstantIsLateInIt() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "N",
            phoneNumber: "+420777111222",
            // A stored value as the decoder hands it over, late in its day.
            // swiftlint:disable:next calendar_day_needs_greenwich
            birthDate: OpenAPIDateWithoutTime(
                wrappedDate: Date(timeIntervalSince1970: 662_772_600),
                timezone: .gmt
            )
        ))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        XCTAssertEqual(client.personalCommand?.birthDate?.rawValue, "1991-01-01")
    }

    /// The email is loaded for the read-only field but never sent — the command has no email field,
    /// so the wire shape itself is the guarantee; this only pins that the display stays populated.
    func testLoadPopulatesTheReadOnlyEmailForDisplay() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            email: "jana@example.com",
            firstName: "Jana",
            lastName: "N",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        XCTAssertEqual(vm.form.email, "jana@example.com")
    }

    func testSaveApiFailureSetsActionErrorAndSnackbars() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "N",
            phoneNumber: "+420777111222",
            birthDate: someBirthDate
        ))
        client.personalUpdateResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        guard case .error = vm.action else { return XCTFail("expected action error") }
        XCTAssertNotNil(snackbar.current)
    }

    // MARK: Every rule the server applies is checked on the device first

    func testInvalidPersonalDataNeverSavesThePhoto() async {
        let vm = await loadValidForm()
        vm.form.phone = ""
        var photoWrites = 0
        await vm.save(photo: { photoWrites += 1
            return true
        })
        XCTAssertEqual(photoWrites, 0)
        XCTAssertNil(client.personalCommand)
    }

    func testDuplicatePhoneKeepsThePhotoDraftAndDoesNotAdvance() async {
        let vm = await loadValidForm()
        client.personalUpdateResult = .failure(ApiError(code: "user.existing_phone_number", httpStatus: 400))
        var photoWrites = 0
        var advanced = false
        let observation = vm.saved.sink { advanced = true }
        defer { observation.cancel() }
        await vm.save(photo: { photoWrites += 1
            return true
        })
        XCTAssertEqual(photoWrites, 0)
        XCTAssertFalse(advanced)
        XCTAssertNotNil(vm.form.phoneError)
    }

    func testBottomSaveWaitsForThePhotoAndStaysOnTheFormIfItFails() async {
        let vm = await loadValidForm()
        var advanced = false
        let observation = vm.saved.sink { advanced = true }
        defer { observation.cancel() }
        await vm.save(photo: {
            XCTAssertNotNil(self.client.personalCommand)
            XCTAssertTrue(vm.action.isSubmitting)
            return false
        })
        XCTAssertFalse(advanced)
        XCTAssertEqual(vm.action, .idle)
        await vm.save(photo: { true })
        XCTAssertTrue(advanced)
    }

    private func loadValidForm() async -> PersonalSectionViewModel {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "N",
            phoneNumber: "+420777111222",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()
        return vm
    }

    private func greenwichDaysAgo(years: Int, days: Int = 0, from now: Date = Date()) throws -> Date {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .gmt
        let today = calendar.startOfDay(for: now)
        let shifted = try XCTUnwrap(calendar.date(byAdding: .year, value: -years, to: today))
        return try XCTUnwrap(calendar.date(byAdding: .day, value: -days, to: shifted))
    }

    func testSaveWithoutPhoneSetsFieldErrorAndSkipsNetwork() async {
        let vm = await loadValidForm()
        vm.form.phone = ""

        await vm.save()

        XCTAssertEqual(vm.form.phoneError, L10n.Profile.errorPhoneRequired)
        XCTAssertNil(client.personalCommand)
        XCTAssertEqual(vm.action, .idle)
        XCTAssertNil(snackbar.current)

        vm.form.phone = "+420777111222"
        XCTAssertNil(vm.form.phoneError, "the error clears as the phone is typed")
        await vm.save()
        XCTAssertEqual(client.personalCommand?.phone, "+420777111222")
    }

    /// What the device can tell without the server: a number without its country code, or with fewer or more
    /// digits than its country's numbers have.
    func testANumberNotInInternationalFormOrOfALengthItsCountryDoesNotUseIsRefusedOnTheDevice() async {
        let vm = await loadValidForm()
        for phone in ["777111222", "+1", "+4201", "+4219051234567"] {
            vm.form.phone = phone
            await vm.save()
            XCTAssertEqual(vm.form.phoneError, L10n.Profile.errorPhoneInvalid, phone)
        }
        XCTAssertNil(client.personalCommand)
        XCTAssertNil(snackbar.current)
    }

    /// The server's phone metadata is newer than the device's, so a number of a length its country uses, in a
    /// range the device does not know, is sent for the server to judge instead of being refused with no way past.
    func testANumberOfAPossibleLengthIsLeftToTheServer() async {
        let vm = await loadValidForm()
        vm.form.phone = "+420609123456"

        await vm.save()

        XCTAssertNil(vm.form.phoneError)
        XCTAssertEqual(client.personalCommand?.phone, "+420609123456")
    }

    /// The server's refusals of the phone are named under it, in their own words, and clear as it is edited.
    func testTheServersPhoneRefusalIsNamedUnderThePhone() async {
        for key in ["validation.invalid_phone_number", "user.existing_phone_number", "common.required"] {
            client.personalUpdateResult = .failure(ApiError(code: key, httpStatus: 400, keys: [key]))
            let vm = await loadValidForm()

            await vm.save()

            XCTAssertEqual(vm.form.phoneError, ApiErrorLocalizer().message(for: ApiError(code: key)), key)
            XCTAssertEqual(vm.action, .idle, key)
            XCTAssertNil(snackbar.current, key)

            vm.form.phone = "+420777111223"
            XCTAssertNil(vm.form.phoneError, key)
        }
    }

    func testAStoredNumberWithSeparatorsIsSentAsStored() async {
        let vm = await loadValidForm()
        vm.form.phone = "+420 728 089 247"

        await vm.save()

        XCTAssertEqual(client.personalCommand?.phone, "+420 728 089 247")
    }

    func testAnUnderageBirthDateIsRefusedOnTheDevice() async throws {
        let vm = await loadValidForm()
        vm.form.birthDate = try greenwichDaysAgo(years: 17)

        await vm.save()

        XCTAssertEqual(vm.form.birthDateError, L10n.Profile.errorMinAge)
        XCTAssertNil(client.personalCommand)
        XCTAssertNil(snackbar.current)
    }

    func testABirthDateMoreThanAHundredAndTwentyYearsBackIsRefusedOnTheDevice() async throws {
        let vm = await loadValidForm()
        vm.form.birthDate = try greenwichDaysAgo(years: 121)

        await vm.save()

        XCTAssertEqual(
            vm.form.birthDateError,
            ApiErrorLocalizer().message(for: ApiError(code: "validation.invalid_age"))
        )
        XCTAssertNil(client.personalCommand)
    }

    /// The server's bounds, day for day: 18 years ago today is old enough, and 120 years ago today is
    /// not too old.
    func testTheBirthDateBoundsAreTheServers() throws {
        let now = Date(timeIntervalSince1970: 1_792_843_200)
        let eighteenToday = try greenwichDaysAgo(years: 18, from: now)
        let eighteenTomorrow = try greenwichDaysAgo(years: 18, days: -1, from: now)
        let hundredTwentyToday = try greenwichDaysAgo(years: 120, from: now)
        let hundredTwentyYesterday = try greenwichDaysAgo(years: 120, days: 1, from: now)

        func problem(_ birthDate: Date?) -> String? {
            PersonalSectionViewModel.birthDateProblem(birthDate, today: now)
        }

        XCTAssertNil(problem(eighteenToday))
        XCTAssertEqual(problem(eighteenTomorrow), L10n.Profile.errorMinAge)
        XCTAssertNil(problem(hundredTwentyToday))
        XCTAssertNotNil(problem(hundredTwentyYesterday))
        XCTAssertEqual(problem(nil), L10n.Profile.errorBirthDateRequired)
    }

    /// Counted as the server counts, in UTF-16: 26 emoji are 26 characters to Swift and 52 to the validator.
    func testANameOverFiftyCharactersIsRefusedOnTheDevice() async {
        let vm = await loadValidForm()
        vm.form.firstName = String(repeating: "a", count: 51)
        vm.form.lastName = String(repeating: "😀", count: 26)

        await vm.save()

        XCTAssertEqual(vm.form.firstNameError, L10n.Profile.errorNameTooLong)
        XCTAssertEqual(vm.form.lastNameError, L10n.Profile.errorNameTooLong)
        XCTAssertNil(client.personalCommand)

        vm.form.firstName = String(repeating: "a", count: 50)
        vm.form.lastName = " " + String(repeating: "😀", count: 25) + " "
        await vm.save()

        XCTAssertNil(vm.form.firstNameError)
        XCTAssertNil(vm.form.lastNameError)
        XCTAssertNotNil(client.personalCommand)
    }

    func testEachErrorClearsWhenItsOwnFieldChanges() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        vm.form.birthDate = someBirthDate.wrappedDate
        XCTAssertNil(vm.form.birthDateError)
        XCTAssertNotNil(vm.form.firstNameError)
        XCTAssertNotNil(vm.form.lastNameError)
        XCTAssertNotNil(vm.form.phoneError)

        vm.form.firstName = "Jana"
        XCTAssertNil(vm.form.firstNameError)
        XCTAssertNotNil(vm.form.lastNameError)

        vm.form.lastName = "N"
        XCTAssertNil(vm.form.lastNameError)
        XCTAssertNotNil(vm.form.phoneError)

        vm.form.phone = "+4"
        XCTAssertNil(vm.form.phoneError)
    }

    /// A binding that writes back the value it already holds has fixed nothing.
    func testWritingTheSameValueBackKeepsTheError() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        vm.form.phone = vm.form.phone
        vm.form.firstName = vm.form.firstName

        XCTAssertNotNil(vm.form.phoneError)
        XCTAssertNotNil(vm.form.firstNameError)
    }
}
