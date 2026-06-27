import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class OnboardingChainViewModelTests: XCTestCase {
    private var client: FakePartnerProfileClient!

    override func setUp() {
        super.setUp()
        client = FakePartnerProfileClient()
    }

    private func makeVM() -> OnboardingChainViewModel {
        OnboardingChainViewModel(client: client)
    }

    func testInitialStateIsLoadingWithAllSectionsFalse() {
        let vm = makeVM()
        XCTAssertTrue(vm.state.isLoading)
        XCTAssertEqual(vm.state.completedSteps, 0)
    }

    func testLoadPopulatesHeaderBeforeFirstSave() async {
        client.statusResult = .success(RegistrationCompletionStatus(
            hasCompletedProfile: false,
            missingFields: ["profile.fields.iban"]
        ))
        let vm = makeVM()

        await vm.load()

        XCTAssertFalse(vm.state.isLoading)
        // personal/address/identification done, only bank still missing —
        // the header reflects real completion before any save runs.
        XCTAssertEqual(vm.state.completedSteps, 3)
        XCTAssertTrue(vm.state.isComplete(.personal))
        XCTAssertFalse(vm.state.isComplete(.bank))
    }

    func testPerSectionCompletionMarksSectionsWithoutMissingFieldsDone() {
        let status = RegistrationCompletionStatus(
            hasCompletedProfile: false,
            missingFields: ["profile.fields.iban"]
        )
        let map = OnboardingChainViewModel.perSectionCompletion(status)
        // personal(0)/address(1)/identification(2) done; bank(3) missing iban.
        XCTAssertEqual(map[0], true)
        XCTAssertEqual(map[1], true)
        XCTAssertEqual(map[2], true)
        XCTAssertEqual(map[3], false)
    }

    func testPerSectionCompletionAllDoneWhenProfileComplete() {
        let status = RegistrationCompletionStatus(hasCompletedProfile: true)
        let map = OnboardingChainViewModel.perSectionCompletion(status)
        XCTAssertTrue(map.values.allSatisfy { $0 })
    }

    func testNextDestinationPicksFirstMissingSection() {
        let status = RegistrationCompletionStatus(
            hasCompletedProfile: false,
            missingFields: ["profile.fields.passportId"]
        )
        XCTAssertEqual(OnboardingChainViewModel.nextDestination(status), .identification(onboarding: true))
    }

    func testNextDestinationNilWhenComplete() {
        let status = RegistrationCompletionStatus(hasCompletedProfile: true)
        XCTAssertNil(OnboardingChainViewModel.nextDestination(status))
    }

    func testAdvanceOrFinishEmitsNextWhenMissing() async {
        client.statusResult = .success(RegistrationCompletionStatus(
            hasCompletedProfile: false,
            missingFields: ["profile.fields.iban"]
        ))
        let vm = makeVM()

        var received: OnboardingChainStep?
        let token = vm.advanced.sink { received = $0 }
        defer { token.cancel() }

        await vm.advanceOrFinish()
        XCTAssertEqual(received, .next(.bank(onboarding: true)))
    }

    func testAdvanceOrFinishEmitsFinishedWhenComplete() async {
        client.statusResult = .success(RegistrationCompletionStatus(hasCompletedProfile: true))
        let vm = makeVM()

        var received: OnboardingChainStep?
        let token = vm.advanced.sink { received = $0 }
        defer { token.cancel() }

        await vm.advanceOrFinish()
        XCTAssertEqual(received, .finished)
    }
}
