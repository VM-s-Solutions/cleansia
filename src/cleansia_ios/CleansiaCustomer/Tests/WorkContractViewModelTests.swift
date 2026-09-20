import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The read behind "Read the contract": the accepted text in the UI language, keyed on the acceptance.
/// A refused read is the error state with nothing of the order on it.
@MainActor
final class WorkContractViewModelTests: XCTestCase {
    private var languageTag = "cs"

    private func makeVM(
        acceptanceId: String = "acc-1",
        client: FakeOrderClient,
        snackbar: SnackbarController = SnackbarController()
    ) -> WorkContractViewModel {
        WorkContractViewModel(
            acceptanceId: acceptanceId,
            client: client,
            snackbar: snackbar,
            languageTag: { [unowned self] in languageTag }
        )
    }

    func testStartsLoadingAndLandsOnTheContractReadInTheUiLanguage() async {
        let client = FakeOrderClient()
        client.workContractResults = [.success(OrderFixtures.workContract())]
        let vm = makeVM(client: client)
        XCTAssertTrue(vm.state.isLoading)

        await vm.load()

        XCTAssertEqual(vm.state.loadedValue, OrderFixtures.workContract())
        XCTAssertEqual(client.workContractRequests.count, 1)
        XCTAssertEqual(client.workContractRequests.first?.acceptanceId, "acc-1")
        XCTAssertEqual(client.workContractRequests.first?.language, "cs")
    }

    func testARefusedReadIsTheErrorStateWithNothingOfTheOrderAndTheRefusalOnTheSnackbar() async {
        let refusal = ApiError(code: "order.not_found", message: "Booking not found.", httpStatus: 400)
        let client = FakeOrderClient()
        client.workContractResults = [.failure(refusal)]
        let snackbar = SnackbarController()
        let vm = makeVM(client: client, snackbar: snackbar)

        await vm.load()

        guard case let .error(error) = vm.state else { return XCTFail("expected error, got \(vm.state)") }
        XCTAssertEqual(error, refusal)
        XCTAssertNil(vm.state.loadedValue)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    func testATransportFailureIsTheErrorStateWithNoSnackbarOfItsOwn() async {
        let client = FakeOrderClient()
        client.workContractResults = [.failure(ApiError(code: "network.unreachable"))]
        let snackbar = SnackbarController()
        let vm = makeVM(client: client, snackbar: snackbar)

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error, got \(vm.state)") }
        XCTAssertNil(snackbar.current, "the error state already says the contract could not be loaded")
    }

    func testRetryRereadsTheSameAcceptanceInTheLanguageCurrentAtThatMoment() async {
        let client = FakeOrderClient()
        client.workContractResults = [
            .failure(ApiError(code: "network.unreachable")),
            .success(OrderFixtures.workContract(language: "en"))
        ]
        let vm = makeVM(client: client)
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error, got \(vm.state)") }

        languageTag = "en"
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.language, "en")
        XCTAssertEqual(client.workContractRequests.map(\.language), ["cs", "en"])
        XCTAssertEqual(Set(client.workContractRequests.map(\.acceptanceId)), ["acc-1"])
    }

    func testAMissingAcceptanceIdIsTheErrorStateAndAsksTheServerNothing() async {
        let client = FakeOrderClient()
        client.workContractResults = [.success(OrderFixtures.workContract())]
        let vm = makeVM(acceptanceId: " ", client: client)

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error, got \(vm.state)") }
        XCTAssertTrue(client.workContractRequests.isEmpty)
    }
}
