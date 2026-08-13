package cz.cleansia.partner.core.notifications

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import androidx.core.content.getSystemService
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.MainActivity
import cz.cleansia.partner.R
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

/**
 * Turns FCM data payloads into local notifications.
 *
 * **Data-only payloads, never a `notification` field**: the client owns the text, so no PII ships to
 * FCM, the lock-screen wording is ours, and one payload shape works foreground and background.
 * -> /architecture/push-notifications
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

        // Only a feed-scoped event has a UserNotification row behind it, and the badge
        // counts rows — so bump it off the keyset, never off "this key has a template".
        if (PartnerFeedEventKeys.contains(eventKey)) {
            notificationFeedRepository.onPushReceived()
        }

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
            // Tints the small icon and the app-name line in the shade; without
            // it the system falls back to a neutral grey.
            .setColor(ContextCompat.getColor(this, R.color.cleansia_primary))
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
