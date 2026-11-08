import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// The customer's inbox rows resolve to the customer's destinations; the inbox itself is tested in Core.
@MainActor
final class NotificationsInboxViewModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client, feedEventKeys: CustomerFeedEventKeys.all)
        snackbar = SnackbarController()
    }

    private func makeVM() -> NotificationsInboxViewModel<CustomerNotificationDestination> {
        NotificationsInboxViewModel(
            client: client,
            badge: badge,
            snackbar: snackbar,
            destination: CustomerNotificationDeepLink.resolve
        )
    }

    func testTapOrderRowEmitsTheOrderDestination() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.completed", args: ["orderId": "o-42"])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [CustomerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertEqual(received, [.order(orderId: "o-42")])
    }

    func testTapCleanerAssignedRowEmitsTheOrderDestination() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.cleaner_assigned", args: ["orderId": "o-7"])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [CustomerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertEqual(received, [.order(orderId: "o-7")])
    }

    func testTapRowWithoutTargetMarksReadWithoutNavigating() async {
        client.pageResults = [NotificationFixtures.page([
            NotificationFixtures.item(id: "n-1", eventKey: "order.completed", args: [:])
        ])]
        let vm = makeVM()
        await vm.onOpen()

        var received: [CustomerNotificationDestination] = []
        let token = vm.tapped.sink { received.append($0) }
        defer { token.cancel() }

        await vm.tap(id: "n-1")

        XCTAssertTrue(received.isEmpty)
        XCTAssertEqual(client.markReadIds, ["n-1"])
    }
}
