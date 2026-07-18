package cz.cleansia.partner.core.notifications

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.content.getSystemService
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.MainActivity
import cz.cleansia.partner.R
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

/**
 * Receives FCM data payloads and turns them into local Android notifications.
 * We use data-only payloads (no `notification` field) so:
 *  - The client owns the title/body text (no PII shipped to FCM).
 *  - The lock-screen text is whatever WE choose to show.
 *  - The same payload shape works for foreground and background delivery.
 *
 * The in-app feed is server-backed ([NotificationFeedRepository]): the producer
 * wrote a UserNotification row in the same transaction that dispatched this
 * push, so we bump the bell badge locally instead of persisting a row here.
 *
 * Hilt-injected via [AndroidEntryPoint]; the service is process-scoped by
 * Android so the singleton [PushTokenRepository] is reachable for onNewToken.
 */
@AndroidEntryPoint
class CleansiaFirebaseMessagingService : FirebaseMessagingService() {

    @Inject lateinit var pushTokenRepository: PushTokenRepository
    @Inject lateinit var notificationFeedRepository: NotificationFeedRepository

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
        val template = NotificationTemplates.templateFor(eventKey) ?: return

        // Feed-scoped event: the server row already exists, so bump the bell
        // badge locally instead of refetching. Unknown keys returned above.
        notificationFeedRepository.onPushReceived()

        val orderId = data["orderId"]?.takeIf { it.isNotBlank() }
        val title = getString(template.titleRes)
        val body = NotificationTemplates.formatBody(this, eventKey, template.bodyRes, data)

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
}
