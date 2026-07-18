package cz.cleansia.customer.core.notifications

import android.content.Intent
import cz.cleansia.customer.navigation.Routes

/**
 * Bidirectional bridge between FCM data payloads and the app's typed
 * Compose-Nav routes (`Routes.OrderDetail`, etc.).
 *
 * Why we serialize through Intent extras instead of passing a route
 * object directly: PendingIntent payloads need to round-trip through the
 * Android system, which means raw types only. We encode the event_key +
 * args as string extras and re-resolve to a typed `Routes.X` instance
 * inside [MainActivity.onNewIntent].
 *
 * Falls back to `null` when the payload doesn't map to a known event —
 * caller should land on Home in that case rather than crashing.
 */
object NotificationDeepLink {

    const val EXTRA_EVENT_KEY = "cleansia.notification.event_key"
    const val EXTRA_ARG_ORDER_ID = "cleansia.notification.order_id"
    const val EXTRA_ARG_DISPUTE_ID = "cleansia.notification.dispute_id"

    /**
     * Stuff an Intent with the strings the typed-route resolver expects
     * later. Called by the messaging service when building the
     * notification's tap intent.
     */
    fun encode(intent: Intent, eventKey: String, args: Map<String, String>) {
        intent.putExtra(EXTRA_EVENT_KEY, eventKey)
        args["orderId"]?.let { intent.putExtra(EXTRA_ARG_ORDER_ID, it) }
        args["disputeId"]?.let { intent.putExtra(EXTRA_ARG_DISPUTE_ID, it) }
    }

    /**
     * Resolve the intent extras back into one of the app's typed routes.
     * Returns `null` when the intent doesn't carry a known event_key —
     * MainActivity should leave the user on the splash/home destination.
     */
    fun resolve(intent: Intent?): Any? {
        val eventKey = intent?.getStringExtra(EXTRA_EVENT_KEY) ?: return null
        val args = buildMap {
            intent.getStringExtra(EXTRA_ARG_ORDER_ID)?.let { put("orderId", it) }
            intent.getStringExtra(EXTRA_ARG_DISPUTE_ID)?.let { put("disputeId", it) }
        }
        return resolve(eventKey, args)
    }

    /**
     * The event_key + args → typed-route mapping itself — shared by the push
     * tap path above and the notifications inbox feed, so both surfaces land
     * on identical destinations. Returns `null` for events with no single
     * right screen; feed callers just mark the row read in that case.
     */
    fun resolve(eventKey: String, args: Map<String, String>): Any? = when (eventKey) {
        "order.confirmed",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "order.on_the_way",
        // recurring.scheduled also carries an orderId — the materialized
        // Pending order the customer needs to confirm + pay for. Route to
        // the Order Detail so the Confirm CTA is right there. Wave 3.3 adds
        // the actual confirm flow; today the user lands on the standard
        // detail screen.
        "recurring.scheduled" ->
            args["orderId"]?.takeIf { it.isNotBlank() }?.let { Routes.OrderDetail(it) }
        "dispute.reply" ->
            args["disputeId"]?.takeIf { it.isNotBlank() }?.let { Routes.DisputeDetail(it) }
        "membership.expiring_soon",
        "membership.cancellation_effective" -> Routes.SubscribePlus
        "loyalty.tier_upgrade" -> Routes.RewardsActivity
        // promo.new_sitewide intentionally lands on Home — there's no
        // single screen that's right for "see the new offer".
        else -> null
    }
}
