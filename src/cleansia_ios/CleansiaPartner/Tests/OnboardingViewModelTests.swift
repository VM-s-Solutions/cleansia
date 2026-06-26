import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class OnboardingViewModelTests: XCTestCase {
    private final class FakeSettings: AppSettingsStore {
        var hasSeenOnboarding = false
        private(set) var markCount = 0
        func markOnboardingSeen() {
            markCount += 1
            hasSeenOnboarding = true
        }

        var languageTag = "en"
    }

    private var settings: FakeSettings!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        settings = FakeSettings()
        cancellables = []
    }

    override func tearDown() {
        cancellables = nil
        settings = nil
        super.tearDown()
    }

    func testFinishMarksOnboardingSeen() {
        let vm = OnboardingViewModel(settings: settings)
        vm.finish()

        XCTAssertTrue(settings.hasSeenOnboarding)
        XCTAssertEqual(settings.markCount, 1)
    }

    func testFinishEmitsFinishedEvent() {
        let vm = OnboardingViewModel(settings: settings)
        var received = false
        vm.finished.sink { received = true }.store(in: &cancellables)

        vm.finish()

        XCTAssertTrue(received)
    }
}
