import SwiftUI
import XCTest
@testable import CleansiaCustomer

final class CustomerPushTapRoutingTests: XCTestCase {
    func testOrderDestinationLandsOnOrdersTabWithItsDetail() {
        let plan = CustomerPushTapRouting.plan(for: .order(orderId: "ord-7"))
        XCTAssertEqual(plan.tab, .orders)
        XCTAssertEqual(plan.routes, [.orderDetail("ord-7")], "the resolved order id must reach the detail push")
    }

    func testDisputeDestinationSeedsTheListUnderTheThread() {
        let plan = CustomerPushTapRouting.plan(for: .dispute(disputeId: "d-3"))
        XCTAssertEqual(plan.tab, .profile)
        XCTAssertEqual(
            plan.routes,
            [.disputes, .disputeDetail("d-3")],
            "back from the thread must land on the disputes list, not the tab root"
        )
    }

    func testSubscribePlusDestinationLandsOnProfileTab() {
        let plan = CustomerPushTapRouting.plan(for: .subscribePlus)
        XCTAssertEqual(plan.tab, .profile)
        XCTAssertEqual(plan.routes, [.subscribePlus])
    }

    func testRewardsActivityDestinationLandsOnRewardsTab() {
        let plan = CustomerPushTapRouting.plan(for: .rewardsActivity)
        XCTAssertEqual(plan.tab, .rewards)
        XCTAssertEqual(plan.routes, [.rewardsActivity])
    }

    func testPlansForDifferentOrdersDiffer() {
        XCTAssertNotEqual(
            CustomerPushTapRouting.plan(for: .order(orderId: "a")),
            CustomerPushTapRouting.plan(for: .order(orderId: "b"))
        )
    }
}

@MainActor
final class CustomerShellPushApplyTests: XCTestCase {
    func testApplyPushTapSelectsTabAndReplacesThePath() throws {
        let model = CustomerShellModel()
        model.path.append(ShellRoute.subscribePlus)

        model.applyPushTap(CustomerPushTapRouting.plan(for: .order(orderId: "ord-1")))

        XCTAssertEqual(model.selection, .orders)
        try assertPath(model.path, equals: [ShellRoute.orderDetail("ord-1")])
    }

    func testApplyPushTapDismissesModalSheets() {
        let model = CustomerShellModel()
        model.isBookingPresented = true
        model.isAddressManagerPresented = true

        model.applyPushTap(CustomerPushTapRouting.plan(for: .dispute(disputeId: "d-1")))

        XCTAssertFalse(model.isBookingPresented, "a covered destination is an invisible destination")
        XCTAssertFalse(model.isAddressManagerPresented)
        XCTAssertEqual(model.selection, .profile)
    }

    func testApplyPushTapSeedsTheDisputeBackStack() throws {
        let model = CustomerShellModel()

        model.applyPushTap(CustomerPushTapRouting.plan(for: .dispute(disputeId: "d-2")))

        try assertPath(model.path, equals: [ShellRoute.disputes, ShellRoute.disputeDetail("d-2")])
    }

    func testConsumeReturnsThePendingDestinationOnceThenClearsIt() {
        let navigation = PushNavigationModel()
        navigation.pendingDestination = .order(orderId: "ord-1")

        XCTAssertEqual(navigation.consume(), .order(orderId: "ord-1"))
        XCTAssertNil(navigation.pendingDestination)
        XCTAssertNil(navigation.consume())
    }

    private func assertPath(
        _ path: NavigationPath,
        equals expected: [ShellRoute],
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let encoder = JSONEncoder()
        let actual = try encoder.encode(XCTUnwrap(path.codable, file: file, line: line))
        let wanted = try encoder.encode(XCTUnwrap(NavigationPath(expected).codable, file: file, line: line))
        XCTAssertEqual(actual, wanted, file: file, line: line)
    }
}
