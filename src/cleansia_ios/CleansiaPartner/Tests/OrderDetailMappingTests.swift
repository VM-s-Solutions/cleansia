import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrderDetailMappingTests: XCTestCase {
    private func makeItem() -> OrderItem {
        var item = OrderItem.wireComplete()
        item.id = "order-1"
        item.displayOrderNumber = "ORD-2026-007"
        item.orderStatus = Code(value: 4)
        item.cleaningDateTime = Date(timeIntervalSince1970: 1_700_000_000)
        item.completedAt = Date(timeIntervalSince1970: 1_700_007_200)
        item.estimatedCleanerPay = 1200
        item.currency = CurrencyDetailDto(code: "CZK", symbol: "Kč")
        item.address = OrderAddress(
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            latitude: 50.0755,
            longitude: 14.4378
        )
        item.customerName = "Jana"
        item.customerPhone = "+420 777 111 222"
        item.rooms = 3
        item.bathrooms = 2
        item.selectedServices = [
            ServiceDetails(name: "Standard clean", translations: ["cs": Translation(name: "Standardní úklid")]),
            ServiceDetails(name: "")
        ]
        item.selectedPackages = [
            PackageDetails(name: "Deep clean", price: 800, translations: ["cs": Translation(name: "Hloubkový úklid")])
        ]
        item.extras = ["inside-oven": true, "interior-windows": true, "skipped": false]
        item.notes = "Cat is friendly."
        item.specialInstructions = "Eco products only."
        item.accessInstructions = "Code 1234."
        item.originalSubtotal = 1400
        item.totalPrice = 1200
        item.tierDiscountAmount = 200
        item.paymentType = Code(name: "Card", value: 2)
        item.paymentStatus = Code(name: "Paid", value: 2)
        item.isAssignedToCurrentUser = true
        item.hasAfterPhotos = true
        return item
    }

    func testMapsCoreIdentityFields() throws {
        let detail = try OrderDetail(makeItem())
        XCTAssertEqual(detail.id, "order-1")
        XCTAssertEqual(detail.orderNumber, "ORD-2026-007")
        XCTAssertEqual(detail.status, ._4)
        XCTAssertEqual(detail.pay, 1200)
        XCTAssertEqual(detail.currencySymbol, "Kč")
    }

    func testCompletedAtMapsThrough() throws {
        XCTAssertEqual(try OrderDetail(makeItem()).completedAt, Date(timeIntervalSince1970: 1_700_007_200))
        XCTAssertNil(try OrderDetail(.wireComplete()).completedAt)
    }

    func testMapsStatusThroughCodeEnvelope() throws {
        var item = makeItem()
        item.orderStatus = Code(value: 6)
        XCTAssertEqual(try OrderDetail(item).status, ._6)
    }

    func testMapsAddressAndCoordinate() throws {
        let detail = try OrderDetail(makeItem())
        XCTAssertEqual(detail.location.line, "Vinohradská 12, Praha, 120 00")
        XCTAssertEqual(
            detail.location.navigationTarget?.coordinate,
            Coordinate(latitude: 50.0755, longitude: 14.4378)
        )
    }

    func testNilCoordinateWhenAddressLacksLatLon() throws {
        var item = makeItem()
        item.address = OrderAddress(street: "X", city: "Y", zipCode: "Z")
        XCTAssertNil(try OrderDetail(item).location.navigationTarget?.coordinate)
    }

    func testMapsScopeServicesPackagesAndActiveExtrasOnly() throws {
        let detail = try OrderDetail(makeItem())
        XCTAssertEqual(detail.rooms, 3)
        XCTAssertEqual(detail.bathrooms, 2)
        XCTAssertEqual(detail.services, [
            OrderDetailService(
                id: nil,
                name: "Standard clean",
                translations: ["cs": Translation(name: "Standardní úklid")]
            )
        ]) // blank dropped
        XCTAssertEqual(detail.packages, [
            OrderDetailPackage(
                id: nil,
                name: "Deep clean",
                price: 800,
                translations: ["cs": Translation(name: "Hloubkový úklid")]
            )
        ])
        XCTAssertEqual(detail.extras, ["inside-oven", "interior-windows"]) // false dropped, sorted
    }

    func testMapsCustomerAndAccessAndNotes() throws {
        let detail = try OrderDetail(makeItem())
        XCTAssertEqual(detail.customerName, "Jana")
        XCTAssertEqual(detail.customerPhone, "+420 777 111 222")
        XCTAssertEqual(detail.accessInstructions, "Code 1234.")
        XCTAssertEqual(detail.customerNotes, "Cat is friendly.")
        XCTAssertEqual(detail.specialInstructions, "Eco products only.")
    }

    func testMapsPaymentBreakdown() throws {
        let detail = try OrderDetail(makeItem())
        XCTAssertTrue(detail.payment.hasBreakdown)
        XCTAssertEqual(detail.payment.subtotal, 1400)
        XCTAssertEqual(detail.payment.total, 1200)
        XCTAssertEqual(detail.payment.tierDiscount, 200)
        XCTAssertEqual(detail.payment.typeCode, PaymentTypeCode.card.rawValue)
        XCTAssertEqual(detail.payment.statusCode, PaymentStatusCode.paid.rawValue)
        XCTAssertTrue(detail.payment.isSettled)
        XCTAssertFalse(detail.payment.isCash)
    }

    func testPaymentHasNoBreakdownWhenSubtotalEqualsTotal() throws {
        var item = makeItem()
        item.originalSubtotal = 1200
        item.totalPrice = 1200
        XCTAssertFalse(try OrderDetail(item).payment.hasBreakdown)
    }

    func testMapsOwnershipAndAfterPhotos() throws {
        let detail = try OrderDetail(makeItem())
        XCTAssertTrue(detail.isAssignedToCurrentUser)
        XCTAssertTrue(detail.hasAfterPhotos)
    }

    /// Collections default; the scalars the mapper refuses are supplied by the fixture. What must
    /// never happen — a missing scalar becoming a rendered `0`/`false` — is driven in
    /// `PartnerWireContractTests`.
    func testAbsentCollectionsRenderAsEmptyOnes() throws {
        let detail = try OrderDetail(.wireComplete())
        XCTAssertEqual(detail.services, [])
        XCTAssertEqual(detail.packages, [])
        XCTAssertEqual(detail.extras, [])
        XCTAssertEqual(detail.orderNotes, [])
        XCTAssertEqual(detail.orderIssues, [])
        XCTAssertEqual(detail.statusHistory, [])
        XCTAssertFalse(detail.payment.hasBreakdown)
    }

    func testMapCoordinateResolvesWithCoordsAndNotCancelled() throws {
        XCTAssertEqual(try OrderDetail(makeItem()).mapCoordinate, Coordinate(latitude: 50.0755, longitude: 14.4378))
    }

    func testMapCoordinateIsNilWhenCancelled() throws {
        var item = makeItem()
        item.orderStatus = Code(value: 6)
        XCTAssertNil(try OrderDetail(item).mapCoordinate)
    }

    func testMapCoordinateIsNilWhenNoCoordinate() throws {
        var item = makeItem()
        item.address = OrderAddress(street: "X")
        XCTAssertNil(try OrderDetail(item).mapCoordinate)
    }

    func testCashDueLabelFormatsTheOrderTotalWithItsCurrency() throws {
        XCTAssertEqual(try OrderDetail(makeItem()).cashDueLabel, OrdersFormat.money(1200, symbol: "Kč"))
    }

    func testCashDueLabelIsNilWithoutATotal() throws {
        var item = makeItem()
        item.totalPrice = nil
        XCTAssertNil(try OrderDetail(item).cashDueLabel)

        item.totalPrice = 0
        XCTAssertNil(try OrderDetail(item).cashDueLabel)
    }
}
