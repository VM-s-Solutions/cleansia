import CleansiaCore
import SwiftUI

/// The customer's inbox: its words, the leaning mascot when it is empty, older rows dated as an order's date
/// and time, its push templates and its destinations.
extension NotificationsInboxSheet where Destination == CustomerNotificationDestination {
    init(
        client: NotificationFeedClient,
        badge: NotificationBadgeModel,
        snackbar: SnackbarController,
        onDestination: @escaping (CustomerNotificationDestination) -> Void
    ) {
        self.init(
            client: client,
            badge: badge,
            snackbar: snackbar,
            copy: L10n.NotificationsInbox.self,
            emptyMascot: .leaning,
            olderThanAWeek: { OrdersFormat.dateTime($0, locale: $1) },
            rows: NotificationFeedTemplates.rows(from:),
            destination: CustomerNotificationDeepLink.resolve,
            onDestination: onDestination
        )
    }
}
