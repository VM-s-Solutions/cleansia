import CleansiaCore
import SwiftUI

/// The partner's inbox: its words, the resting mascot when it is empty, older rows dated as the Orders tab
/// dates a job, its push templates and its destinations.
extension NotificationsInboxSheet where Destination == PartnerNotificationDestination {
    init(
        client: NotificationFeedClient,
        badge: NotificationBadgeModel,
        snackbar: SnackbarController,
        onDestination: @escaping (PartnerNotificationDestination) -> Void
    ) {
        self.init(
            client: client,
            badge: badge,
            snackbar: snackbar,
            copy: L10n.NotificationsInbox.self,
            emptyMascot: .resting,
            olderThanAWeek: { OrdersFormat.relativeDateTime($0, locale: $1) },
            rows: NotificationFeedTemplates.rows(from:),
            destination: PartnerNotificationDeepLink.resolve,
            onDestination: onDestination
        )
    }
}
