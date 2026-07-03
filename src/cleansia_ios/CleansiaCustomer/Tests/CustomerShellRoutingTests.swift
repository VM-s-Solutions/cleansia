import SwiftUI
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerShellRoutingTests: XCTestCase {
    func testOpenOrderSelectsOrdersTabAndShowsOnlyTheDetail() throws {
        let model = CustomerShellModel()
        model.path.append(ShellRoute.subscribePlus)

        model.openOrder("order-1")

        XCTAssertEqual(model.selection, .orders)
        try assertPath(model.path, equals: [ShellRoute.orderDetail("order-1")])
    }

    func testOpenOrdersSelectsOrdersTabAndClearsThePath() {
        let model = CustomerShellModel()
        model.path.append(ShellRoute.disputes)

        model.openOrders()

        XCTAssertEqual(model.selection, .orders)
        XCTAssertTrue(model.path.isEmpty)
    }

    func testOpenEditProfileSelectsProfileTabAndShowsOnlyTheEditor() throws {
        let model = CustomerShellModel()

        model.openEditProfile()

        XCTAssertEqual(model.selection, .profile)
        try assertPath(model.path, equals: [ShellRoute.editProfile])
    }

    func testSelectChangesTheTabWithoutTouchingThePath() {
        let model = CustomerShellModel()
        model.path.append(ShellRoute.orderDetail("order-1"))

        model.select(.rewards)

        XCTAssertEqual(model.selection, .rewards)
        XCTAssertEqual(model.path.count, 1)
    }

    func testPopRemovesTheLastRouteAndIsSafeOnAnEmptyPath() {
        let model = CustomerShellModel()

        model.pop()
        XCTAssertTrue(model.path.isEmpty)

        model.path.append(ShellRoute.disputes)
        model.path.append(ShellRoute.disputeDetail("d-1"))
        model.pop()
        XCTAssertEqual(model.path.count, 1)
    }

    func testEveryShellRouteRoundTripsThroughTheErasedPath() throws {
        let routes: [ShellRoute] = [
            .orderDetail("order-1"),
            .subscribePlus,
            .membershipSuccess,
            .recurringList,
            .createRecurring(orderId: nil),
            .createRecurring(orderId: "order-2"),
            .rewardsActivity,
            .disputes,
            .createDispute(orderId: nil),
            .createDispute(orderId: "order-3"),
            .disputeDetail("d-1"),
            .addresses,
            .editProfile,
            .devices,
            .notifications,
            .security,
            .language,
            .appearance,
            .help,
            .deleteAccount
        ]
        let path = NavigationPath(routes)

        let encoded = try JSONEncoder().encode(XCTUnwrap(path.codable))
        let representation = try JSONDecoder().decode(NavigationPath.CodableRepresentation.self, from: encoded)
        let decoded = NavigationPath(representation)

        XCTAssertEqual(decoded.count, routes.count)
        try assertPath(decoded, equals: routes)
    }

    func testShellRouteEqualityDistinguishesAssociatedValues() {
        XCTAssertEqual(ShellRoute.orderDetail("a"), ShellRoute.orderDetail("a"))
        XCTAssertNotEqual(ShellRoute.orderDetail("a"), ShellRoute.orderDetail("b"))
        XCTAssertEqual(ShellRoute.orderDetail("a").hashValue, ShellRoute.orderDetail("a").hashValue)
        XCTAssertNotEqual(ShellRoute.createRecurring(orderId: nil), ShellRoute.createRecurring(orderId: "x"))
        XCTAssertNotEqual(ShellRoute.subscribePlus, ShellRoute.editProfile)
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
