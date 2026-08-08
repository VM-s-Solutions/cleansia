package cz.cleansia.customer.core.notifications

/**
 * The customer half of the backend's audience keysets
 * (`NotificationFeedEventKeys.Customer`) — the events whose push arrives WITH a
 * `UserNotification` row behind it, which is the only thing the unread badge counts.
 *
 * Rendering a push and holding a feed row are separate questions and this app answers
 * both: it has templates for keys the customer feed never serves (partner-targeted
 * ones reach a dual-role user's device tokens, and a key can ship its client copy
 * before it joins the keyset). Bumping the badge off "has a template" counts rows the
 * feed will not return, which is the same phantom-badge the backend keyset's own
 * doc-comment warns about, approached from the client side.
 */
object CustomerFeedEventKeys {
    val all: Set<String> = setOf(
        "order.confirmed",
        "order.cleaner_assigned",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "dispute.reply",
        "recurring.scheduled",
        "membership.expiring_soon",
        "membership.cancellation_effective",
        "loyalty.tier_upgrade",
    )

    fun contains(eventKey: String): Boolean = eventKey in all
}
