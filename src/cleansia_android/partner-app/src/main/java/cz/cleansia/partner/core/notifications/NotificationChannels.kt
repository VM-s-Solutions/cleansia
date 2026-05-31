package cz.cleansia.partner.core.notifications

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import androidx.core.content.getSystemService
import cz.cleansia.partner.R

/**
 * Registers the partner notification channels at app start. Android dedupes by
 * channel id, so calling [registerAll] on every cold start is cheap and safe.
 *
 * One channel per category gives the cleaner system-level granular control
 * (long-press a notification → "Stop showing this category") without us
 * shipping a separate mute UI.
 *
 * Partners see a narrower set than customers — just job updates + support
 * replies — so we register two channels rather than the customer app's eleven.
 */
object NotificationChannels {

    /** Job lifecycle (confirmed / in progress / completed / cancelled). */
    const val CHANNEL_ORDER_UPDATES = "cleansia.partner.notification.order_updates"

    /** Support → cleaner dispute replies. */
    const val CHANNEL_DISPUTE_REPLY = "cleansia.partner.notification.dispute_reply"

    /** "N new jobs available near you" digest (every 30 min, only when new). */
    const val CHANNEL_NEW_JOBS = "cleansia.partner.notification.new_jobs"

    fun registerAll(context: Context) {
        val manager = context.getSystemService<NotificationManager>() ?: return
        manager.createNotificationChannels(
            listOf(
                channel(
                    context,
                    CHANNEL_ORDER_UPDATES,
                    R.string.notification_channel_order_updates_name,
                    R.string.notification_channel_order_updates_desc,
                    NotificationManager.IMPORTANCE_HIGH,
                ),
                channel(
                    context,
                    CHANNEL_DISPUTE_REPLY,
                    R.string.notification_channel_dispute_reply_name,
                    R.string.notification_channel_dispute_reply_desc,
                    NotificationManager.IMPORTANCE_HIGH,
                ),
                channel(
                    context,
                    CHANNEL_NEW_JOBS,
                    R.string.notification_channel_new_jobs_name,
                    R.string.notification_channel_new_jobs_desc,
                    // Default importance: digest, not an urgent ping. Don't
                    // wake the screen / heads-up for a periodic summary.
                    NotificationManager.IMPORTANCE_DEFAULT,
                ),
            ),
        )
    }

    private fun channel(
        context: Context,
        id: String,
        nameRes: Int,
        descRes: Int,
        importance: Int,
    ): NotificationChannel = NotificationChannel(
        id,
        context.getString(nameRes),
        importance,
    ).apply {
        description = context.getString(descRes)
    }
}
