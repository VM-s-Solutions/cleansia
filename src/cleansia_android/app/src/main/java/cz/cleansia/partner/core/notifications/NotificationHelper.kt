package cz.cleansia.partner.core.notifications

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import cz.cleansia.partner.MainActivity
import cz.cleansia.partner.R
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class NotificationHelper @Inject constructor(
    @ApplicationContext private val context: Context
) {
    companion object {
        const val CHANNEL_ORDER_TIMER = "order_timer"
        const val CHANNEL_ORDER_ALERTS = "order_alerts"
        const val NOTIFICATION_ID_TIMER = 1001
        const val NOTIFICATION_ID_CAUTION = 1002
        const val NOTIFICATION_ID_URGENT = 1003
        const val NOTIFICATION_ID_OVERTIME = 1004
    }

    fun createNotificationChannels() {
        val notificationManager = context.getSystemService(NotificationManager::class.java)

        // Delete old timer channel if it was created with wrong importance (IMPORTANCE_LOW)
        // Android doesn't allow upgrading channel importance, so we must recreate it
        val existingTimer = notificationManager.getNotificationChannel(CHANNEL_ORDER_TIMER)
        if (existingTimer != null && existingTimer.importance < NotificationManager.IMPORTANCE_DEFAULT) {
            notificationManager.deleteNotificationChannel(CHANNEL_ORDER_TIMER)
        }

        val timerChannel = NotificationChannel(
            CHANNEL_ORDER_TIMER,
            context.getString(R.string.notification_channel_timer),
            NotificationManager.IMPORTANCE_DEFAULT
        ).apply {
            description = context.getString(R.string.notification_channel_timer_desc)
            setShowBadge(false)
            setSound(null, null) // silent but visible in status bar
        }

        val alertsChannel = NotificationChannel(
            CHANNEL_ORDER_ALERTS,
            context.getString(R.string.notification_channel_alerts),
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = context.getString(R.string.notification_channel_alerts_desc)
            enableVibration(true)
        }

        notificationManager.createNotificationChannel(timerChannel)
        notificationManager.createNotificationChannel(alertsChannel)
    }

    fun buildTimerNotification(
        orderNumber: String,
        elapsedMinutes: Int,
        estimatedMinutes: Int,
        isOvertime: Boolean
    ): NotificationCompat.Builder {
        val intent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP
            data = android.net.Uri.parse("cleansia://partner/orders")
        }
        val pendingIntent = PendingIntent.getActivity(
            context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val remaining = estimatedMinutes - elapsedMinutes
        val contentText = if (isOvertime) {
            context.getString(R.string.notification_overtime, -remaining)
        } else {
            context.getString(R.string.notification_time_remaining, remaining)
        }

        return NotificationCompat.Builder(context, CHANNEL_ORDER_TIMER)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(context.getString(R.string.notification_order_timer, orderNumber))
            .setContentText(contentText)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setContentIntent(pendingIntent)
            .setSilent(true)
            .setCategory(NotificationCompat.CATEGORY_PROGRESS)
            .setProgress(estimatedMinutes, elapsedMinutes.coerceAtMost(estimatedMinutes), false)
    }

    fun showAlertNotification(
        notificationId: Int,
        title: String,
        message: String
    ) {
        val intent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP
            data = android.net.Uri.parse("cleansia://partner/orders")
        }
        val pendingIntent = PendingIntent.getActivity(
            context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, CHANNEL_ORDER_ALERTS)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(title)
            .setContentText(message)
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()

        val notificationManager = context.getSystemService(NotificationManager::class.java)
        notificationManager.notify(notificationId, notification)
    }

    fun cancelNotification(notificationId: Int) {
        val notificationManager = context.getSystemService(NotificationManager::class.java)
        notificationManager.cancel(notificationId)
    }

    fun cancelAllTimerNotifications() {
        cancelNotification(NOTIFICATION_ID_TIMER)
        cancelNotification(NOTIFICATION_ID_CAUTION)
        cancelNotification(NOTIFICATION_ID_URGENT)
        cancelNotification(NOTIFICATION_ID_OVERTIME)
    }
}
