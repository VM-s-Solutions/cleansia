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

    private let someBirthDate = OpenAPIDateWithoutTime(
        wrappedDate: Date(timeIntervalSince1970: 662_688_000),
        timezone: .current
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

    func testSaveSendsBirthDateOnCommand() async {
        client.employeeResult = .success(EmployeeItem(
            id: "emp-1",
            firstName: "Jana",
            lastName: "N",
            birthDate: someBirthDate
        ))
        let vm = makeVM()
        await vm.load()
        await vm.save()

        XCTAssertEqual(client.personalCommand?.birthDate, someBirthDate)
        XCTAssertNil(vm.form.birthDateError)
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
