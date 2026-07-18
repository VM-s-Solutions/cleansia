package cz.cleansia.customer.core.notifications

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.content.getSystemService
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import cz.cleansia.core.notifications.PushTokenRepository
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
    @Inject lateinit var notificationFeedRepository: NotificationFeedRepository

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    override fun onNewToken(token: String) {
        // FCM rotated the token; push the new value to the backend so
        // future dispatches don't get NotRegistered. This call requires
        // the user to be signed in — if they aren't, the network call
        // 401s and PushTokenRepository's `networkCall` swallows it.
        // Either way, the backend will receive the token at next sign-in
        // via PushSessionListener.
        // Push into the repository's hot flow. PushTokenSessionObserver
        // POSTs to the backend if (and only if) a session is active —
        // rotations while signed out are buffered and delivered at next
        // sign-in instead of silently 401ing.
        pushTokenRepository.reportRotatedToken(token)
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
            val template = NotificationTemplates.templateFor(eventKey) ?: return
            // Feed-scoped event: the producer wrote a UserNotification row in the
            // same transaction, so bump the bell badge locally instead of refetching.
            // Promo is excluded (no feed row v1) and unknown keys returned above.
            notificationFeedRepository.onPushReceived()
            Triple(
                getString(template.titleRes),
                NotificationTemplates.formatBody(this, eventKey, template.bodyRes, data),
                template.category,
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

}
