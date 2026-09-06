import XCTest
@testable import CleansiaCustomer

/// How an order's items become something a customer can point at — for a dispute ("this part was not
/// done properly") and for a review ("this part was excellent, that one was not").
///
/// Both features share one builder, and the property that makes it worth sharing is the identity: the
/// SAME service can be on one order twice, bought alone and inside a bundle, and the two must not
/// collapse into one row. An admin refunding one of them must not refund the other.
///
/// The quieter property is just as load-bearing: an item with no id is SKIPPED rather than shown. A
/// row the customer can tick but the server would reject as not-on-this-order is worse than no row —
/// the error it produces names a box they cannot un-tick.
final class OrderItemLineTests: XCTestCase {
    private func service(id: String?, name: String) -> CustomerOrderService {
        CustomerOrderService(
            id: id,
            name: name,
            description: nil,
            estimatedMinutes: 0,
            translations: nil
        )
    }

    private func package(
        id: String?,
        name: String,
        items: [CustomerOrderPackageService]
    ) -> CustomerOrderPackage {
        OrderFakes.package(id: id, name: name, includedServiceItems: items)
    }

    private func order(
        services: [CustomerOrderService] = [],
        packages: [CustomerOrderPackage] = []
    ) -> CustomerOrderDetail {
        OrderFakes.detail(services: services, packages: packages)
    }

    // MARK: - the identity

    func testStandaloneAndInPackageAreTwoDistinctRows() {
        let lines = OrderItemLine.lines(
            of: order(
                services: [service(id: "svc-oven", name: "Oven clean")],
                packages: [
                    package(
                        id: "pkg-deep",
                        name: "Deep Clean",
                        items: [CustomerOrderPackageService(id: "svc-oven", name: "Oven clean")]
                    )
                ]
            )
        )

        XCTAssertEqual(lines.count, 2)
        XCTAssertEqual(Set(lines.map(\.id)).count, 2)
    }

    func testTheSameServiceInTwoPackagesStaysTwoRows() {
        let lines = OrderItemLine.lines(
            of: order(packages: [
                package(id: "pkg-a", name: "A", items: [CustomerOrderPackageService(id: "svc", name: "Oven")]),
                package(id: "pkg-b", name: "B", items: [CustomerOrderPackageService(id: "svc", name: "Oven")])
            ])
        )

        XCTAssertEqual(Set(lines.map(\.id)).count, 2)
    }

    func testAStandaloneServiceCarriesNoPackage() {
        let line = OrderItemLine.lines(
            of: order(services: [service(id: "svc-1", name: "Windows")])
        ).first

        XCTAssertNil(line?.packageId)
        XCTAssertNil(line?.packageLabel)
        XCTAssertEqual(line?.label, "Windows")
    }

    func testAnInPackageServiceNamesTheBundle() {
        let line = OrderItemLine.lines(
            of: order(packages: [
                package(
                    id: "pkg-deep",
                    name: "Deep Clean",
                    items: [CustomerOrderPackageService(id: "svc-oven", name: "Oven")]
                )
            ])
        ).first

        XCTAssertEqual(line?.packageId, "pkg-deep")
        XCTAssertEqual(line?.packageLabel, "Deep Clean")
        XCTAssertEqual(line?.label, "Oven")
    }

    // MARK: - what must not be offered

    func testAnItemWithNoIdIsSkipped() {
        let lines = OrderItemLine.lines(
            of: order(
                services: [service(id: nil, name: "Nameless"), service(id: "svc-ok", name: "Fine")],
                packages: [
                    package(
                        id: "pkg-1",
                        name: "Bundle",
                        items: [
                            CustomerOrderPackageService(id: nil, name: "Nameless too"),
                            CustomerOrderPackageService(id: "svc-in", name: "Also fine")
                        ]
                    )
                ]
            )
        )

        XCTAssertEqual(lines.map(\.serviceId), ["svc-ok", "svc-in"])
    }

    func testAPackageWithNoIdContributesNothing() {
        let lines = OrderItemLine.lines(
            of: order(packages: [
                package(id: nil, name: "Bundle", items: [CustomerOrderPackageService(id: "svc", name: "Oven")])
            ])
        )

        XCTAssertTrue(lines.isEmpty)
    }

    func testNoOrderAnswersEmptySoTheSectionHides() {
        XCTAssertTrue(OrderItemLine.lines(of: nil).isEmpty)
    }

    /// `includedServices` is a list of NAMES and cannot be sent back to the server. Building rows from
    /// it would produce ticks the server always rejects.
    func testTheNameOnlyIncludedServicesListIsNeverUsed() {
        var pkg = OrderFakes.package(id: "pkg-1", name: "Bundle")
        pkg = CustomerOrderPackage(
            id: pkg.id,
            name: pkg.name,
            description: pkg.description,
            price: pkg.price,
            estimatedMinutes: pkg.estimatedMinutes,
            currencyCode: pkg.currencyCode,
            includedServices: ["Oven clean", "Windows"],
            includedServiceItems: [],
            translations: pkg.translations
        )

        XCTAssertTrue(OrderItemLine.lines(of: order(packages: [pkg])).isEmpty)
    }
}
