import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerSplashViewModelTests: XCTestCase {
    func testValidSessionResolvesToAuthenticated() async {
        let vm = CustomerSplashViewModel(hasValidSession: true, hold: {})
        await vm.resolve()
        XCTAssertEqual(vm.outcome, .authenticated)
    }

    func testNoSessionResolvesToUnauthenticated() async {
        let vm = CustomerSplashViewModel(hasValidSession: false, hold: {})
        await vm.resolve()
        XCTAssertEqual(vm.outcome, .unauthenticated)
    }

    func testOutcomeIsNilBeforeResolve() {
        let vm = CustomerSplashViewModel(hasValidSession: true, hold: {})
        XCTAssertNil(vm.outcome)
    }

    func testResolveWaitsForBrandHoldBeforeEmitting() async {
        var held = false
        let vm = CustomerSplashViewModel(hasValidSession: true, hold: { held = true })
        XCTAssertNil(vm.outcome)
        await vm.resolve()
        XCTAssertTrue(held)
        XCTAssertEqual(vm.outcome, .authenticated)
    }
}
