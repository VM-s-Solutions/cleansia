package cz.cleansia.partner.core.notifications

/**
 * The partner half of the backend's audience keysets
 * (`NotificationFeedEventKeys.Partner`) — the events whose push arrives WITH a
 * `UserNotification` row behind it, which is the only thing the unread badge counts.
 *
 * Rendering a push and holding a feed row are separate questions and this app answers
 * both: it has templates for keys the partner feed never serves (customer-targeted
 * ones reach a dual-role user's device tokens, and a key can ship its client copy
 * before it joins the keyset). Bumping the badge off "has a template" counts rows the
 * feed will not return, which is the same phantom-badge the backend keyset's own
 * doc-comment warns about, approached from the client side.
 */
object PartnerFeedEventKeys {
    val all: Set<String> = setOf(
        "order.new_available",
        "order.preferred_offer",
        "order.assignment_cancelled",
        "payroll.invoice_paid",
    )

    fun contains(eventKey: String): Boolean = eventKey in all
}
