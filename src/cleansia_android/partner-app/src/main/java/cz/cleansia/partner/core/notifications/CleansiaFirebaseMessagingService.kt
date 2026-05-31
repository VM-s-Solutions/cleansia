package cz.cleansia.partner.core.notifications

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.content.getSystemService
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import cz.cleansia.partner.MainActivity
import cz.cleansia.partner.R
import cz.cleansia.partner.core.notifications.db.NotificationDao
import cz.cleansia.partner.core.notifications.db.NotificationRecord
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Receives FCM data payloads and turns them into local Android notifications
 * plus a row in the in-app notifications feed. Mirrors the customer-app
 * service. We use data-only payloads (no `notification` field) so:
 *  - The client owns the title/body text (no PII shipped to FCM).
 *  - The lock-screen text is whatever WE choose to show.
 *  - The same payload shape works for foreground and background delivery.
 *
 * Hilt-injected via [AndroidEntryPoint]; the service is process-scoped by
 * Android so the singleton [PushTokenRepository] is reachable for onNewToken.
 *
 * TODO(backend): the backend currently dispatches every push to the order's
 * customer UserId only — there are NO partner-targeted dispatches yet (see
 * NotificationEventCatalog.cs; the order.* and dispute.reply events all target
 * order.UserId). The keys wired below are the order-lifecycle events the cleaner WOULD
 * receive once the backend fans out to the assigned employee's UserId. Keys
 * that still need backend confirmation / a new dispatch site:
 *   - order.available  (a new unassigned job the cleaner can take)
 *   - order.assigned   (a job was assigned to this cleaner)
 *   - invoice.generated / payperiod.invoice_generated (pay-period invoice ready)
 * Add their templates + channels here once the backend ships them.
 */
@AndroidEntryPoint
class CleansiaFirebaseMessagingService : FirebaseMessagingService() {

    @Inject lateinit var pushTokenRepository: PushTokenRepository
    @Inject lateinit var notificationDao: NotificationDao

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    override fun onNewToken(token: String) {
        // FCM rotated the token; push the new value into the repository's
        // hot flow. PushTokenSessionObserver picks it up and POSTs to the
        // backend if (and only if) a session is active — so rotations that
        // happen while signed out no longer 401, they're buffered and
        // delivered on next sign-in automatically.
        pushTokenRepository.reportRotatedToken(token)
    }

    override fun onMessageReceived(message: RemoteMessage) {
        val data = message.data
        val eventKey = data["event_key"] ?: return
        val template = templateFor(eventKey) ?: return

        val orderId = data["orderId"]?.takeIf { it.isNotBlank() }
        val title = getString(template.titleRes)
        val body = formatBody(template.bodyRes, eventKey, data)

        // Persist to the feed first so the row exists even if the OS suppresses
        // the system notification (permission denied, channel muted).
        scope.launch {
            notificationDao.insert(
                NotificationRecord(
                    eventKey = eventKey,
                    title = title,
                    body = body,
                    orderId = orderId,
                    timestamp = System.currentTimeMillis(),
                ),
            )
        }

        showNotification(eventKey, template.channelId, title, body, data, orderId)
    }

    private fun showNotification(
        eventKey: String,
        channelId: String,
        title: String,
        body: String,
        data: Map<String, String>,
        orderId: String?,
    ) {
        val tapIntent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
            NotificationDeepLink.encode(this, eventKey, data)
        }
        val pendingFlags = PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        val pendingIntent = PendingIntent.getActivity(
            this,
            // Per-event request code so two notifications of different
            // event_keys don't overwrite each other's pending intent.
            eventKey.hashCode(),
            tapIntent,
            pendingFlags,
        )

        val notification = NotificationCompat.Builder(this, channelId)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()

        val manager = getSystemService<NotificationManager>() ?: return
        // Order id is the distinguishing tag when present so two different
        // jobs don't collapse into one notification line; otherwise the
        // event_key keeps same-kind events from stacking.
        val tag = orderId ?: eventKey
        manager.notify(tag, eventKey.hashCode(), notification)
    }

    /**
     * Map an event_key to its (title, body, channel) template. Returns `null`
     * for unknown keys — silently drop rather than surface a phantom
     * notification. Customer-only events (loyalty / membership / promo /
     * recurring / refund) are intentionally absent.
     */
    private fun templateFor(eventKey: String): Template? = when (eventKey) {
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
        "order.new_available" -> Template(
            R.string.notification_new_jobs_title,
            R.string.notification_new_jobs_body,
            NotificationChannels.CHANNEL_NEW_JOBS,
        )
        else -> null
    }

    /**
     * Order events carry `orderNumber` for the body's positional arg; dispute
     * replies have no arg.
     */
    private fun formatBody(bodyRes: Int, eventKey: String, data: Map<String, String>): String =
        when (eventKey) {
            "order.confirmed",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            -> getString(bodyRes, data["orderNumber"].orEmpty())
            "order.new_available" -> {
                // Backend sends `count` as a decimal-string. Fall back to 1 if
                // missing so the string doesn't render "%d new jobs" literally.
                val count = data["count"]?.toIntOrNull() ?: 1
                getString(bodyRes, count)
            }
            else -> getString(bodyRes)
        }

    private data class Template(val titleRes: Int, val bodyRes: Int, val channelId: String)
}
