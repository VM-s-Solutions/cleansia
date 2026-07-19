package cz.cleansia.partner.core.notifications

import android.content.Intent
import cz.cleansia.partner.navigation.NavRoute

/**
 * Bidirectional bridge between FCM data payloads and the partner app's typed
 * Compose-Nav routes (`NavRoute.OrderDetail`, etc.). Mirrors the customer-app
 * object; the only difference is the route types it resolves to.
 *
 * Why serialize through Intent extras instead of passing a route object
 * directly: PendingIntent payloads round-trip through the Android system, which
 * allows raw types only. We encode event_key + args as string extras and
 * re-resolve to a typed [NavRoute] inside [MainActivity].
 *
 * Falls back to `null` when the payload doesn't map to a known event — the
 * caller should leave the user on the current/start destination rather than
 * crash.
 */
object NotificationDeepLink {

    const val EXTRA_EVENT_KEY = "cleansia.partner.notification.event_key"
    const val EXTRA_ARG_ORDER_ID = "cleansia.partner.notification.order_id"

    /**
     * Stuff an Intent with the strings the typed-route resolver expects later.
     * Called by the messaging service when building the notification's tap
     * intent.
     */
    fun encode(intent: Intent, eventKey: String, args: Map<String, String>) {
        intent.putExtra(EXTRA_EVENT_KEY, eventKey)
        args["orderId"]?.let { intent.putExtra(EXTRA_ARG_ORDER_ID, it) }
    }

    /**
     * Resolve the intent extras back into one of the app's typed routes.
     * Returns `null` when the intent doesn't carry a known event_key.
     */
    fun resolve(intent: Intent?): NavRoute? {
        val eventKey = intent?.getStringExtra(EXTRA_EVENT_KEY) ?: return null
        return resolve(eventKey, intent.getStringExtra(EXTRA_ARG_ORDER_ID))
    }

    /**
     * Resolve from a raw event_key + orderId. Shared by [resolve] (system-tap
     * path) and the in-app notifications feed (row-tap path) so both routes
     * land on the same destination.
     */
    fun resolve(eventKey: String, orderId: String?): NavRoute? = when (eventKey) {
        // All currently-wired partner events are order-scoped — land on the
        // order detail so the cleaner sees the job that changed. dispute.reply
        // also carries the disputed order's id (see CleansiaFirebaseMessagingService).
        "order.confirmed",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.on_the_way",
        "dispute.reply",
        "order.assignment_cancelled",
        -> orderId?.takeIf { it.isNotBlank() }?.let { NavRoute.OrderDetail(orderId = it) }
        // Digest — no single order to open. Land on the bottom-nav
        // scaffold (Main). NavRoute.Orders is a nested tab inside Main,
        // not a root destination, so navigating to it directly throws
        // "Destination cannot be found in navigation graph". Main opens
        // on Dashboard; the new-jobs callout on the dashboard plus the
        // bottom-nav Orders tab let the cleaner land on Available in one
        // tap. orderId arg is ignored.
        "order.new_available" -> NavRoute.Main
        else -> null
    }
}
