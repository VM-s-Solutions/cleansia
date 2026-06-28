import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerSplashViewModelTests: XCTestCase {
    func testValidSessionResolvesToAuthenticated() async {
        let vm = CustomerSplashViewModel(hasValidSession: true)
        await vm.resolve()
        XCTAssertEqual(vm.outcome, .authenticated)
    }

    func testNoSessionResolvesToUnauthenticated() async {
        let vm = CustomerSplashViewModel(hasValidSession: false)
        await vm.resolve()
        XCTAssertEqual(vm.outcome, .unauthenticated)
    }

    func testOutcomeIsNilBeforeResolve() {
        let vm = CustomerSplashViewModel(hasValidSession: true)
        XCTAssertNil(vm.outcome)
    }
}
