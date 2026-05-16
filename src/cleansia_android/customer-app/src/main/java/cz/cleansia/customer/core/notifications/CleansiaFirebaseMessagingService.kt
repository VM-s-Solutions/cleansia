package cz.cleansia.customer.core.notifications

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.content.getSystemService
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import cz.cleansia.customer.MainActivity
import cz.cleansia.customer.R
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Receives FCM data payloads and turns them into local Android
 * notifications. We use data-only payloads (no `notification` field on
 * the FCM message) so:
 *  - The mobile client owns the title/body text (no PII shipped to FCM).
 *  - The lock-screen text is whatever WE choose to show.
 *  - The same payload shape works for foreground and background delivery.
 *
 * Hilt-injected via [AndroidEntryPoint]. The service is process-scoped
 * by Android, so the [PushTokenRepository] singleton is reachable for
 * `onNewToken`.
 */
@AndroidEntryPoint
class CleansiaFirebaseMessagingService : FirebaseMessagingService() {

    @Inject lateinit var pushTokenRepository: PushTokenRepository
    @Inject lateinit var orderEventBus: OrderEventBus

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    override fun onNewToken(token: String) {
        // FCM rotated the token; push the new value to the backend so
        // future dispatches don't get NotRegistered. This call requires
        // the user to be signed in — if they aren't, the network call
        // 401s and PushTokenRepository's `networkCall` swallows it.
        // Either way, the backend will receive the token at next sign-in
        // via PushSessionListener.
        scope.launch { pushTokenRepository.onTokenRotated(token) }
    }

    override fun onMessageReceived(message: RemoteMessage) {
        val data = message.data
        val eventKey = data["event_key"] ?: return

        // Fan out order-scoped events to in-process listeners (e.g.
        // OrderDetailViewModel) so they refetch immediately instead of waiting
        // for the next poll tick. Covers `order.*` lifecycle events plus
        // `recurring.scheduled` (which carries an orderId for the materialized
        // recurring booking the customer needs to confirm). Done before
        // building the local notification so the UI stays in sync even if the
        // user has the detail screen open and the system notification is
        // suppressed.
        val carriesOrderId = eventKey.startsWith("order.") || eventKey == "recurring.scheduled"
        if (carriesOrderId) {
            data["orderId"]?.takeIf { it.isNotBlank() }?.let { orderId ->
                orderEventBus.emit(OrderEvent(orderId = orderId, eventKey = eventKey))
            }
        }

        // promo.new_sitewide is the only event whose title+body are admin-
        // authored at send time (rather than resolved from a fixed
        // strings.xml template). The fan-out Function localizes per user and
        // ships the resulting strings in the FCM data payload. Local resource
        // lookup is bypassed; we only resolve the category for the channel.
        val (title, body, category) = if (eventKey == "promo.new_sitewide") {
            val serverTitle = data["title"]?.takeIf { it.isNotBlank() } ?: return
            val serverBody = data["body"]?.takeIf { it.isNotBlank() } ?: return
            Triple(serverTitle, serverBody, NotificationCategoryDto.Promo)
        } else {
            val (titleRes, bodyRes, category) = templateFor(eventKey) ?: return
            Triple(
                getString(titleRes),
                formatBody(bodyRes, eventKey, data),
                category,
            )
        }
        val channelId = NotificationChannels.channelIdFor(category)

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
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()

        val manager = getSystemService<NotificationManager>() ?: return
        // Per-event tag so a new event_key replaces (not stacks) any
        // existing notification of the same kind. Order id is the
        // distinguishing axis when present so two different orders
        // don't collapse into one notification line.
        val tag = data["orderId"] ?: data["disputeId"] ?: eventKey
        manager.notify(tag, eventKey.hashCode(), notification)
    }

    /**
     * Map an event_key from the FCM payload to (title, body, category).
     * Returns `null` when the key is unknown — silently drop rather than
     * surface a phantom notification.
     */
    private fun templateFor(eventKey: String): Triple<Int, Int, NotificationCategoryDto>? = when (eventKey) {
        "order.confirmed" -> Triple(
            R.string.notification_order_confirmed_title,
            R.string.notification_order_confirmed_body,
            NotificationCategoryDto.OrderUpdates,
        )
        "order.on_the_way" -> Triple(
            R.string.notification_order_on_the_way_title,
            R.string.notification_order_on_the_way_body,
            NotificationCategoryDto.OrderUpdates,
        )
        "order.in_progress" -> Triple(
            R.string.notification_order_in_progress_title,
            R.string.notification_order_in_progress_body,
            NotificationCategoryDto.OrderUpdates,
        )
        "order.completed" -> Triple(
            R.string.notification_order_completed_title,
            R.string.notification_order_completed_body,
            NotificationCategoryDto.OrderCompleted,
        )
        "order.cancelled" -> Triple(
            R.string.notification_order_cancelled_title,
            R.string.notification_order_cancelled_body,
            NotificationCategoryDto.OrderCancelled,
        )
        "order.refunded" -> Triple(
            R.string.notification_order_refunded_title,
            R.string.notification_order_refunded_body,
            NotificationCategoryDto.RefundIssued,
        )
        "dispute.reply" -> Triple(
            R.string.notification_dispute_reply_title,
            R.string.notification_dispute_reply_body,
            NotificationCategoryDto.DisputeReply,
        )
        "recurring.scheduled" -> Triple(
            R.string.notification_recurring_scheduled_title,
            R.string.notification_recurring_scheduled_body,
            NotificationCategoryDto.RecurringScheduled,
        )
        "loyalty.tier_upgrade" -> Triple(
            R.string.notification_loyalty_tier_upgrade_title,
            R.string.notification_loyalty_tier_upgrade_body,
            NotificationCategoryDto.TierUpgrade,
        )
        "membership.expiring_soon" -> Triple(
            R.string.notification_membership_expiring_title,
            R.string.notification_membership_expiring_body,
            NotificationCategoryDto.MembershipExpiring,
        )
        "membership.cancellation_effective" -> Triple(
            R.string.notification_membership_cancelled_title,
            R.string.notification_membership_cancelled_body,
            NotificationCategoryDto.MembershipCancelled,
        )
        else -> null
    }

    /**
     * Format the body string with the args we expect for the given event.
     * String.format with the right positional args per template.
     */
    private fun formatBody(bodyRes: Int, eventKey: String, data: Map<String, String>): String {
        return when (eventKey) {
            "order.confirmed",
            "order.on_the_way",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            "order.refunded",
            "recurring.scheduled" -> {
                val orderNumber = data["orderNumber"].orEmpty()
                getString(bodyRes, orderNumber)
            }
            "loyalty.tier_upgrade" -> {
                // Body carries a localized tier name. The wire `tier` arg is the
                // enum's .ToString() value ("SilverMopper", "GoldPolisher", etc.)
                // — map to a localized label so the user sees "Silver Mopper"
                // not the enum identifier.
                val tierLabel = data["tier"]?.let { resolveTierLabel(it) } ?: ""
                getString(bodyRes, tierLabel)
            }
            "dispute.reply",
            "membership.expiring_soon",
            "membership.cancellation_effective" -> getString(bodyRes)
            else -> getString(bodyRes)
        }
    }

    /**
     * Map the backend LoyaltyTier enum identifier (sent as `tier` arg on the
     * loyalty.tier_upgrade push) to a localized human label. Unknown values
     * fall back to the raw enum string so the user still sees something.
     */
    private fun resolveTierLabel(enumName: String): String = when (enumName) {
        "BronzeCleaner" -> getString(R.string.loyalty_tier_bronze_cleaner)
        "SilverMopper" -> getString(R.string.loyalty_tier_silver_mopper)
        "GoldPolisher" -> getString(R.string.loyalty_tier_gold_polisher)
        "PlatinumSparkler" -> getString(R.string.loyalty_tier_platinum_sparkler)
        else -> enumName
    }
}
