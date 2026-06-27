import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrderDetailMappingTests: XCTestCase {
    private func makeItem() -> OrderItem {
        var item = OrderItem()
        item.id = "order-1"
        item.displayOrderNumber = "ORD-2026-007"
        item.orderStatus = Code(value: 4)
        item.cleaningDateTime = Date(timeIntervalSince1970: 1_700_000_000)
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
        item.selectedServices = [ServiceDetails(name: "Standard clean"), ServiceDetails(name: "")]
        item.selectedPackages = [PackageDetails(name: "Deep clean", price: 800)]
        item.extras = ["inside-oven": true, "interior-windows": true, "skipped": false]
        item.notes = "Cat is friendly."
        item.specialInstructions = "Eco products only."
        item.accessInstructions = "Code 1234."
        item.originalSubtotal = 1400
        item.totalPrice = 1200
        item.tierDiscountAmount = 200
        item.paymentType = Code(name: "Card")
        item.paymentStatus = Code(name: "Paid")
        item.isAssignedToCurrentUser = true
        item.hasAfterPhotos = true
        return item
    }

    func testMapsCoreIdentityFields() {
        let detail = OrderDetail(makeItem())
        XCTAssertEqual(detail.id, "order-1")
        XCTAssertEqual(detail.orderNumber, "ORD-2026-007")
        XCTAssertEqual(detail.status, ._4)
        XCTAssertEqual(detail.pay, 1200)
        XCTAssertEqual(detail.currencySymbol, "Kč")
    }

    func testMapsStatusThroughCodeEnvelope() {
        var item = makeItem()
        item.orderStatus = Code(value: 6)
        XCTAssertEqual(OrderDetail(item).status, ._6)
    }

    func testMapsAddressAndCoordinate() {
        let detail = OrderDetail(makeItem())
        XCTAssertEqual(detail.address?.singleLine, "Vinohradská 12, Praha, 120 00")
        XCTAssertEqual(detail.coordinate, Coordinate(latitude: 50.0755, longitude: 14.4378))
    }

    func testNilCoordinateWhenAddressLacksLatLon() {
        var item = makeItem()
        item.address = OrderAddress(street: "X", city: "Y", zipCode: "Z")
        XCTAssertNil(OrderDetail(item).coordinate)
    }

    func testMapsScopeServicesPackagesAndActiveExtrasOnly() {
        let detail = OrderDetail(makeItem())
        XCTAssertEqual(detail.rooms, 3)
        XCTAssertEqual(detail.bathrooms, 2)
        XCTAssertEqual(detail.services, ["Standard clean"]) // blank dropped
        XCTAssertEqual(detail.packages, [OrderDetailPackage(name: "Deep clean", price: 800)])
        XCTAssertEqual(detail.extras, ["inside-oven", "interior-windows"]) // false dropped, sorted
    }

    func testMapsCustomerAndAccessAndNotes() {
        let detail = OrderDetail(makeItem())
        XCTAssertEqual(detail.customerName, "Jana")
        XCTAssertEqual(detail.customerPhone, "+420 777 111 222")
        XCTAssertEqual(detail.accessInstructions, "Code 1234.")
        XCTAssertEqual(detail.customerNotes, "Cat is friendly.")
        XCTAssertEqual(detail.specialInstructions, "Eco products only.")
    }

    func testMapsPaymentBreakdown() {
        let detail = OrderDetail(makeItem())
        XCTAssertTrue(detail.payment.hasBreakdown)
        XCTAssertEqual(detail.payment.subtotal, 1400)
        XCTAssertEqual(detail.payment.total, 1200)
        XCTAssertEqual(detail.payment.tierDiscount, 200)
        XCTAssertEqual(detail.payment.methodName, "Card")
        XCTAssertEqual(detail.payment.statusName, "Paid")
    }

    func testPaymentHasNoBreakdownWhenSubtotalEqualsTotal() {
        var item = makeItem()
        item.originalSubtotal = 1200
        item.totalPrice = 1200
        XCTAssertFalse(OrderDetail(item).payment.hasBreakdown)
    }

    func testMapsOwnershipAndAfterPhotos() {
        let detail = OrderDetail(makeItem())
        XCTAssertTrue(detail.isAssignedToCurrentUser)
        XCTAssertTrue(detail.hasAfterPhotos)
    }

    func testDefaultsForMissingFields() {
        let detail = OrderDetail(OrderItem())
        XCTAssertEqual(detail.rooms, 0)
        XCTAssertEqual(detail.services, [])
        XCTAssertEqual(detail.extras, [])
        XCTAssertFalse(detail.isAssignedToCurrentUser)
        XCTAssertFalse(detail.hasAfterPhotos)
        XCTAssertFalse(detail.payment.hasBreakdown)
    }

    func testCanShowMapTrueWithCoordsAndNotCancelled() {
        XCTAssertTrue(OrderDetail(makeItem()).canShowMap)
    }

    func testCanShowMapFalseWhenCancelled() {
        var item = makeItem()
        item.orderStatus = Code(value: 6)
        XCTAssertFalse(OrderDetail(item).canShowMap)
    }

    func testCanShowMapFalseWhenNoCoordinate() {
        var item = makeItem()
        item.address = OrderAddress(street: "X")
        XCTAssertFalse(OrderDetail(item).canShowMap)
    }
}
