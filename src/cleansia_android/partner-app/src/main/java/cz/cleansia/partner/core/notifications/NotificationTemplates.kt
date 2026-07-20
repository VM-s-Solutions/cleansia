package cz.cleansia.partner.core.notifications

import android.content.Context
import cz.cleansia.partner.R

/**
 * The per-event notification templates — the single source for BOTH surfaces
 * that render an event_key + args into user-visible text: the FCM push display
 * ([CleansiaFirebaseMessagingService]) and the notifications feed. Keys unknown
 * to this catalog are silently dropped on both surfaces (drop-parity). The
 * server feed only writes `order.new_available` rows for partners today; the
 * order-lifecycle and dispute keys stay so a partner-targeted dispatch (T-0431)
 * slots straight in without touching two maps.
 */
object NotificationTemplates {

    data class Template(
        val titleRes: Int,
        val bodyRes: Int,
        val channelId: String,
    )

    fun templateFor(eventKey: String): Template? = when (eventKey) {
        "order.confirmed" -> Template(
            R.string.notification_order_confirmed_title,
            R.string.notification_order_confirmed_body,
            NotificationChannels.CHANNEL_ORDER_UPDATES,
        )
        "order.in_progress" -> Template(
            R.string.notification_order_in_progress_title,
            R.string.notification_order_in_progress_body,
            NotificationChannels.CHANNEL_ORDER_UPDATES,
        )
        "order.completed" -> Template(
            R.string.notification_order_completed_title,
            R.string.notification_order_completed_body,
            NotificationChannels.CHANNEL_ORDER_UPDATES,
        )
        "order.cancelled" -> Template(
            R.string.notification_order_cancelled_title,
            R.string.notification_order_cancelled_body,
            NotificationChannels.CHANNEL_ORDER_UPDATES,
        )
        "dispute.reply" -> Template(
            R.string.notification_dispute_reply_title,
            R.string.notification_dispute_reply_body,
            NotificationChannels.CHANNEL_DISPUTE_REPLY,
        )
        "order.assignment_cancelled" -> Template(
            R.string.notification_order_assignment_cancelled_title,
            R.string.notification_order_assignment_cancelled_body,
            NotificationChannels.CHANNEL_ORDER_UPDATES,
        )
        "order.new_available" -> Template(
            R.string.notification_new_jobs_title,
            R.string.notification_new_jobs_body,
            NotificationChannels.CHANNEL_NEW_JOBS,
        )
        "payroll.invoice_paid" -> Template(
            R.string.notification_payroll_invoice_paid_title,
            R.string.notification_payroll_invoice_paid_body,
            NotificationChannels.CHANNEL_ORDER_UPDATES,
        )
        else -> null
    }

    /**
     * Order events carry `orderNumber` for the body's positional arg; the
     * new-jobs digest carries a decimal-string `count`; dispute replies have
     * no arg.
     */
    fun formatBody(context: Context, eventKey: String, bodyRes: Int, args: Map<String, String>): String =
        when (eventKey) {
            "order.confirmed",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            "order.assignment_cancelled",
            -> context.getString(bodyRes, args["orderNumber"].orEmpty())
            "order.new_available" -> {
                val count = args["count"]?.toIntOrNull() ?: 1
                context.getString(bodyRes, count)
            }
            else -> context.getString(bodyRes)
        }
}
