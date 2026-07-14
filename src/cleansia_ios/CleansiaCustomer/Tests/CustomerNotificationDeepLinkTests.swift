import XCTest
@testable import CleansiaCustomer

final class CustomerNotificationDeepLinkTests: XCTestCase {
    func testOrderEventWithIdResolvesToOrderDestination() {
        let destination = CustomerNotificationDeepLink.resolve(
            eventKey: "order.confirmed",
            orderId: "ord-1",
            disputeId: nil
        )
        XCTAssertEqual(destination, .order(orderId: "ord-1"))
    }

    func testAllOrderScopedEventsResolveToOrder() {
        let keys = [
            "order.confirmed",
            "order.on_the_way",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            "order.refunded",
            "recurring.scheduled"
        ]
        for key in keys {
            XCTAssertEqual(
                CustomerNotificationDeepLink.resolve(eventKey: key, orderId: "ord-1", disputeId: nil),
                .order(orderId: "ord-1"),
                key
            )
        }
    }

    func testOrderEventWithoutIdResolvesToNil() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(
            eventKey: "order.confirmed",
            orderId: nil,
            disputeId: nil
        ))
    }

    func testDisputeReplyResolvesToDisputeThread() {
        XCTAssertEqual(
            CustomerNotificationDeepLink.resolve(eventKey: "dispute.reply", orderId: "ord-1", disputeId: "d-1"),
            .dispute(disputeId: "d-1")
        )
    }

    func testDisputeReplyWithoutDisputeIdResolvesToNil() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(
            eventKey: "dispute.reply",
            orderId: "ord-1",
            disputeId: nil
        ))
    }

    func testMembershipEventsResolveToSubscribePlus() {
        for key in ["membership.expiring_soon", "membership.cancellation_effective"] {
            XCTAssertEqual(
                CustomerNotificationDeepLink.resolve(eventKey: key, orderId: nil, disputeId: nil),
                .subscribePlus,
                key
            )
        }
    }

    func testLoyaltyTierUpgradeResolvesToRewardsActivity() {
        XCTAssertEqual(
            CustomerNotificationDeepLink.resolve(eventKey: "loyalty.tier_upgrade", orderId: nil, disputeId: nil),
            .rewardsActivity
        )
    }

    func testUnknownEventResolvesToNil() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(
            eventKey: "loyalty.points",
            orderId: "ord-1",
            disputeId: nil
        ))
    }

    func testPromoResolvesToNilAndLandsOnHome() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(
            eventKey: "promo.new_sitewide",
            orderId: nil,
            disputeId: nil
        ))
    }

    func testPartnerOnlyNewAvailableResolvesToNil() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(
            eventKey: "order.new_available",
            orderId: nil,
            disputeId: nil
        ))
    }

    func testResolveFromUserInfoMapsEventKeyAndOrderId() {
        let userInfo: [AnyHashable: Any] = ["event_key": "order.completed", "orderId": "ord-9"]
        XCTAssertEqual(CustomerNotificationDeepLink.resolve(userInfo), .order(orderId: "ord-9"))
    }

    func testResolveFromUserInfoMapsDisputeId() {
        let userInfo: [AnyHashable: Any] = ["event_key": "dispute.reply", "orderId": "ord-9", "disputeId": "d-9"]
        XCTAssertEqual(CustomerNotificationDeepLink.resolve(userInfo), .dispute(disputeId: "d-9"))
    }

    func testResolveFromUserInfoWithEmptyIdsResolvesToNil() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(["event_key": "order.completed", "orderId": ""]))
        XCTAssertNil(CustomerNotificationDeepLink.resolve(["event_key": "dispute.reply", "disputeId": ""]))
    }

    func testResolveFromUserInfoWithoutEventKeyResolvesToNil() {
        XCTAssertNil(CustomerNotificationDeepLink.resolve(["orderId": "ord-1"]))
    }

    // The APNs display payload adds an `aps` alert block alongside the data
    // keys; resolution must read only the data keys and stay unaffected.

    func testResolveFromAlertCarryingUserInfoStillResolvesOrder() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "order.confirmed",
            locArgs: ["A-1042"],
            extra: ["orderId": "ord-1", "orderNumber": "A-1042"]
        )
        XCTAssertEqual(CustomerNotificationDeepLink.resolve(userInfo), .order(orderId: "ord-1"))
    }

    func testResolveFromAlertCarryingUserInfoStillResolvesDispute() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "dispute.reply",
            locArgs: [],
            extra: ["orderId": "ord-1", "disputeId": "d-1"]
        )
        XCTAssertEqual(CustomerNotificationDeepLink.resolve(userInfo), .dispute(disputeId: "d-1"))
    }

    func testResolveFromAlertCarryingUserInfoWithUnknownEventStillResolvesToNil() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "promo.new_sitewide",
            locArgs: [],
            extra: [:]
        )
        XCTAssertNil(CustomerNotificationDeepLink.resolve(userInfo))
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
