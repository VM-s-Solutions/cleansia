package cz.cleansia.partner.core.notifications.db

import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * One row per received push, written by the FCM service so the in-app
 * notifications feed can list history that outlives the transient system
 * notification (which the user may swipe away). The feed reads these back
 * newest-first and flips [isRead] when opened.
 *
 * [eventKey] + [orderId] are kept raw (not resolved to a route) so the feed's
 * tap handler can re-resolve through [NotificationDeepLink] exactly like a
 * system-notification tap does — one code path for both.
 */
@Entity(tableName = "notification_records")
data class NotificationRecord(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val eventKey: String,
    val title: String,
    val body: String,
    val orderId: String?,
    val timestamp: Long,
    val isRead: Boolean = false,
)
