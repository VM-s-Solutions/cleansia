import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

/// The partner's inbox rows resolve to the partner's destinations; the inbox itself is tested in Core.
@MainActor
final class NotificationsInboxViewModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client, feedEventKeys: PartnerFeedEventKeys.all)
        snackbar = SnackbarController()
    }

    private func makeVM() -> NotificationsInboxViewModel<PartnerNotificationDestination> {
        NotificationsInboxViewModel(
            client: client,
            badge: badge,
            snackbar: snackbar,
            destination: PartnerNotificationDeepLink.resolve
        )
    }

    func testTapNewAvailableRowEmitsTheOrdersTabDestination() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.new_available", args: ["count": "2"])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [PartnerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertEqual(received, [.ordersTab])
    }

    func testTapInvoicePaidRowEmitsTheInvoiceDestination() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "payroll.invoice_paid", args: ["invoiceId": "inv-5"])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [PartnerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertEqual(received, [.invoice(invoiceId: "inv-5")])
    }

    func testTapRowWithoutTargetMarksReadWithoutNavigating() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.payment_confirmed", args: [:])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [PartnerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertTrue(received.isEmpty)
        XCTAssertEqual(client.markReadIds, ["n-1"])
    }
}
