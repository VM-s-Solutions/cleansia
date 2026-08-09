import XCTest
@testable import CleansiaPartner

final class PartnerNotificationDeepLinkTests: XCTestCase {
    func testOrderEventWithIdResolvesToOrderDestination() {
        let destination = PartnerNotificationDeepLink.resolve(eventKey: "order.confirmed", orderId: "ord-1")
        XCTAssertEqual(destination, .order(orderId: "ord-1"))
    }

    func testAllOrderScopedEventsResolveToOrder() {
        let keys = [
            "order.confirmed",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            "order.on_the_way",
            "order.assignment_cancelled",
            "order.preferred_offer",
            "order.assigned",
            "order.assignment_revoked",
            "dispute.reply"
        ]
        for key in keys {
            XCTAssertEqual(
                PartnerNotificationDeepLink.resolve(eventKey: key, orderId: "ord-1"),
                .order(orderId: "ord-1"),
                key
            )
        }
    }

    /// The cleaner is off the job, and the order detail is still the destination: the copy says the job
    /// moved, and the detail is where they read which day just came off their schedule (Android parity).
    func testARevokedAssignmentStillOpensTheOrderItWasTakenFrom() {
        XCTAssertEqual(
            PartnerNotificationDeepLink.resolve(eventKey: "order.assignment_revoked", orderId: "ord-4"),
            .order(orderId: "ord-4")
        )
    }

    /// Customer-audience keys, which this app carries display copy for but is never sent.
    func testCustomerOnlyEventsResolveToNil() {
        for key in ["order.starting_soon", "order.preferred_offer_closed", "order.cleaner_assigned"] {
            XCTAssertNil(PartnerNotificationDeepLink.resolve(eventKey: key, orderId: "ord-1"), key)
        }
    }

    func testOrderEventWithoutIdResolvesToNil() {
        XCTAssertNil(PartnerNotificationDeepLink.resolve(eventKey: "order.confirmed", orderId: nil))
    }

    func testAssignmentEventsWithoutIdResolveToNil() {
        for key in ["order.assigned", "order.assignment_revoked"] {
            XCTAssertNil(PartnerNotificationDeepLink.resolve(eventKey: key, orderId: nil), key)
        }
    }

    func testNewAvailableResolvesToOrdersTab() {
        XCTAssertEqual(
            PartnerNotificationDeepLink.resolve(eventKey: "order.new_available", orderId: nil),
            .ordersTab
        )
    }

    func testInvoicePaidWithIdResolvesToInvoiceDestination() {
        XCTAssertEqual(
            PartnerNotificationDeepLink.resolve(eventKey: "payroll.invoice_paid", orderId: nil, invoiceId: "inv-7"),
            .invoice(invoiceId: "inv-7")
        )
    }

    func testInvoicePaidWithoutIdResolvesToEarningsTab() {
        // Parity with Android's `InvoiceDetail(id) ?: Earnings` fallback.
        XCTAssertEqual(
            PartnerNotificationDeepLink.resolve(eventKey: "payroll.invoice_paid", orderId: nil, invoiceId: nil),
            .earningsTab
        )
    }

    func testUnknownEventResolvesToNil() {
        XCTAssertNil(PartnerNotificationDeepLink.resolve(eventKey: "loyalty.points", orderId: "ord-1"))
    }

    func testResolveFromUserInfoMapsEventKeyAndOrderId() {
        let userInfo: [AnyHashable: Any] = ["event_key": "order.completed", "orderId": "ord-9"]
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .order(orderId: "ord-9"))
    }

    func testResolveFromUserInfoWithEmptyOrderIdResolvesToNil() {
        let userInfo: [AnyHashable: Any] = ["event_key": "order.completed", "orderId": ""]
        XCTAssertNil(PartnerNotificationDeepLink.resolve(userInfo))
    }

    func testResolveFromUserInfoMapsInvoiceId() {
        let userInfo: [AnyHashable: Any] = ["event_key": "payroll.invoice_paid", "invoiceId": "inv-9"]
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .invoice(invoiceId: "inv-9"))
    }

    func testResolveFromUserInfoWithEmptyInvoiceIdResolvesToEarningsTab() {
        // An empty invoiceId is normalized to nil, then falls back to the
        // Earnings tab (Android parity), not a dropped tap.
        let userInfo: [AnyHashable: Any] = ["event_key": "payroll.invoice_paid", "invoiceId": ""]
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .earningsTab)
    }

    func testResolveFromUserInfoWithoutEventKeyResolvesToNil() {
        XCTAssertNil(PartnerNotificationDeepLink.resolve(["orderId": "ord-1"]))
    }

    // The APNs display payload adds an `aps` alert block alongside the data
    // keys; resolution must read only the data keys and stay unaffected.

    func testResolveFromAlertCarryingUserInfoStillResolvesOrder() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "order.confirmed",
            locArgs: ["A-1042"],
            extra: ["orderId": "ord-1"]
        )
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .order(orderId: "ord-1"))
    }

    func testResolveFromAlertCarryingUserInfoStillResolvesOrdersTab() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "order.new_available",
            locArgs: ["3"],
            extra: ["count": "3"]
        )
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .ordersTab)
    }

    func testResolveFromAlertCarryingUserInfoWithUnknownEventStillResolvesToNil() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "promo.new_sitewide",
            locArgs: [],
            extra: ["orderId": "ord-1"]
        )
        XCTAssertNil(PartnerNotificationDeepLink.resolve(userInfo))
    }

    func testResolveFromAlertCarryingUserInfoResolvesInvoice() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "payroll.invoice_paid",
            locArgs: [],
            extra: ["invoiceId": "inv-3"]
        )
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .invoice(invoiceId: "inv-3"))
    }

    private func alertCarryingUserInfo(
        eventKey: String,
        locArgs: [String],
        extra: [AnyHashable: Any]
    ) -> [AnyHashable: Any] {
        var userInfo: [AnyHashable: Any] = [
            "aps": [
                "alert": [
                    "title-loc-key": "push.\(eventKey).title",
                    "loc-key": "push.\(eventKey).body",
                    "loc-args": locArgs
                ],
                "sound": "default",
                "thread-id": (extra["orderId"] as? String) ?? eventKey
            ],
            "event_key": eventKey
        ]
        for (key, value) in extra {
            userInfo[key] = value
        }
        return userInfo
    }
}
