import CleansiaCore
import SwiftUI

/// The customer's inbox: its words, the leaning mascot when it is empty, its push templates and its destinations.
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
            rows: NotificationFeedTemplates.rows(from:),
            destination: CustomerNotificationDeepLink.resolve,
            onDestination: onDestination
        )
    }
}
