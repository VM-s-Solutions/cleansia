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
        try assertPath(model.path, equals: [ShellRoute.editProfile(showBookingHint: false)])
    }

    func testOpenEditProfileFromTheBookingGateCarriesTheHintFlag() throws {
        let model = CustomerShellModel()

        model.openEditProfile(showBookingHint: true)

        XCTAssertEqual(model.selection, .profile)
        try assertPath(model.path, equals: [ShellRoute.editProfile(showBookingHint: true)])
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
            .createDispute(orderId: "order-3"),
            .createDispute(orderId: "order-3", reason: DisputeReasonOption.serviceNotProvided),
            .disputeDetail("d-1"),
            .addresses,
            .payments,
            .editProfile(showBookingHint: false),
            .editProfile(showBookingHint: true),
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
        XCTAssertNotEqual(
            ShellRoute.editProfile(showBookingHint: false),
            ShellRoute.editProfile(showBookingHint: true)
        )
        XCTAssertNotEqual(ShellRoute.subscribePlus, ShellRoute.editProfile(showBookingHint: false))
        XCTAssertNotEqual(
            ShellRoute.createDispute(orderId: "a"),
            ShellRoute.createDispute(orderId: "a", reason: DisputeReasonOption.serviceNotProvided)
        )
    }

    private func assertPath(
        _ path: NavigationPath,
        equals expected: [ShellRoute],
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let actual = try canonical(path, file: file, line: line)
        let wanted = try canonical(NavigationPath(expected), file: file, line: line)
        XCTAssertEqual(actual, wanted, file: file, line: line)
    }

    /// The path's entries with each route's JSON re-serialised with sorted keys: a route with two
    /// associated values (createDispute) encodes its keys in no fixed order, so raw bytes can differ.
    private func canonical(_ path: NavigationPath, file: StaticString, line: UInt) throws -> [String] {
        let data = try JSONEncoder().encode(XCTUnwrap(path.codable, file: file, line: line))
        let entries = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String], file: file, line: line)
        return try entries.map { entry in
            guard let object = try? JSONSerialization.jsonObject(with: Data(entry.utf8), options: .fragmentsAllowed)
            else { return entry }
            let sorted = try JSONSerialization.data(withJSONObject: object, options: [.sortedKeys, .fragmentsAllowed])
            return try XCTUnwrap(String(bytes: sorted, encoding: .utf8), file: file, line: line)
        }
    }
}
