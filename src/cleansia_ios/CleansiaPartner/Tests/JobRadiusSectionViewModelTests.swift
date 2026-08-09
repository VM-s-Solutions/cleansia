import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class JobRadiusSectionViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> JobRadiusSectionViewModel {
        JobRadiusSectionViewModel(client: client, snackbar: snackbar)
    }

    private func seed(id: String = "emp-1", radiusKm: Int?) {
        client.profileResult = .success(
            EmployeeProfile(employee: EmployeeItem(id: id), jobRadiusKm: radiusKm)
        )
    }

    func testLoadSeedsTheControlFromTheEmployeeReadModel() async {
        seed(radiusKm: 60)
        let vm = makeVM()

        await vm.load()

        XCTAssertNotNil(vm.state.loadedValue)
        XCTAssertEqual(vm.form.selection, .within(60))
    }

    func testAnUnsetRadiusLoadsAsTheAnywhereChoice() async {
        seed(radiusKm: nil)
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.form.selection, .anywhere)
    }

    func testLoadFailureBecomesTheErrorStateAndSnackbars() async {
        client.profileResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testSaveSendsTheChosenDistanceForTheSessionEmployee() async {
        seed(radiusKm: nil)
        client.jobRadiusResult = .success(75)
        let vm = makeVM()
        await vm.load()

        vm.setLimited(true)
        vm.setKilometres(75)
        await vm.save()

        XCTAssertEqual(client.jobRadiusCommand?.employeeId, "emp-1")
        XCTAssertEqual(client.jobRadiusCommand?.radiusKm, 75)
    }

    func testClearingTheLimitSendsNullRatherThanZero() async {
        seed(radiusKm: 30)
        client.jobRadiusResult = .success(nil)
        let vm = makeVM()
        await vm.load()

        vm.setLimited(false)
        await vm.save()

        XCTAssertNotNil(client.jobRadiusCommand)
        XCTAssertNil(client.jobRadiusCommand?.radiusKm)
    }

    func testSaveEmitsTheServerValueAndLeavesTheActionIdle() async {
        seed(radiusKm: nil)
        client.jobRadiusResult = .success(45)
        let vm = makeVM()
        await vm.load()

        var emitted: [Int?] = []
        let token = vm.saved.sink { emitted.append($0) }
        defer { token.cancel() }

        vm.setLimited(true)
        vm.setKilometres(45)
        await vm.save()

        XCTAssertEqual(emitted.count, 1)
        XCTAssertEqual(emitted.first, 45)
        XCTAssertFalse(vm.action.isSubmitting)
    }

    func testSaveFailureSurfacesOnTheActionStateAndEmitsNothing() async {
        seed(radiusKm: nil)
        client.jobRadiusResult = .failure(
            ApiError(code: "employee.job_radius_out_of_range", httpStatus: 400)
        )
        let vm = makeVM()
        await vm.load()

        var emitted = false
        let token = vm.saved.sink { _ in emitted = true }
        defer { token.cancel() }

        vm.setLimited(true)
        await vm.save()

        XCTAssertFalse(emitted)
        XCTAssertNotNil(snackbar.current)
        guard case let .error(message) = vm.action else { return XCTFail("expected an error action state") }
        XCTAssertFalse(message.isEmpty)
    }

    func testTheOutOfRangeBusinessKeyResolvesToRealCopyRatherThanTheRawKey() {
        let message = ApiErrorLocalizer().message(
            for: ApiError(code: "employee.job_radius_out_of_range", message: "raw", httpStatus: 400)
        )

        XCTAssertNotEqual(message, "employee.job_radius_out_of_range")
        XCTAssertNotEqual(message, "raw")
    }

    /// A failed reload keeps the employee id, so an unguarded save would write the previous form over
    /// a profile the app could not read.
    func testSaveRefusesAfterAReloadFailed() async {
        seed(radiusKm: 30)
        let vm = makeVM()
        await vm.load()

        client.profileResult = .failure(ApiError(httpStatus: 500))
        await vm.load()
        await vm.save()

        XCTAssertNil(client.jobRadiusCommand)
    }
}
