import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrderStatusMappingTests: XCTestCase {
    func testCodeMapsEveryValueToTheMatchingOrderStatus() {
        let cases: [(Int, OrderStatus)] = [
            (0, ._0), (1, ._1), (2, ._2), (3, ._3), (4, ._4), (5, ._5), (6, ._6)
        ]
        for (value, expected) in cases {
            XCTAssertEqual(Code(value: value).toOrderStatus(), expected)
        }
    }

    func testCodeWithNilValueMapsToNil() {
        XCTAssertNil(Code(value: nil).toOrderStatus())
    }

    func testCodeWithOutOfRangeValueMapsToNil() {
        XCTAssertNil(Code(value: 7).toOrderStatus())
        XCTAssertNil(Code(value: -1).toOrderStatus())
    }

    func testOrderListItemStatusReadsThroughTheCodeEnvelope() {
        let item = OrderListItem.sample(id: "o1", status: ._4)
        XCTAssertEqual(item.status, ._4)
        XCTAssertTrue(item.isInProgress)
    }

    func testOrderItemStatusReadsThroughTheCodeEnvelope() {
        var item = OrderItem()
        item.orderStatus = Code(value: 5)
        XCTAssertEqual(item.status, ._5)
    }
}
