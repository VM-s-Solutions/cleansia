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

    /// Greenwich, because that is what a value decoded off the wire carries — the generated decoder
    /// stores the day at midnight UTC and stamps the formatter's zone, never the handset's.
    private let someBirthDate = OpenAPIDateWithoutTime(
        wrappedDate: Date(timeIntervalSince1970: 662_688_000),
        timezone: .gmt
    )

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
        XCTAssertNil(client.personalCommand)
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
            birthDate: someBirthDate
        ))
        client.personalUpdateResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        guard case .error = vm.action else { return XCTFail("expected action error") }
        XCTAssertNotNil(snackbar.current)
    }
}
