package cz.cleansia.partner.core.notifications

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import androidx.core.content.getSystemService
import cz.cleansia.partner.R

/**
 * The partner notification channels. Android dedupes by channel id, so
 * registering on every start is cheap and safe. Their names are read when a
 * channel is registered, and registering an existing id again renames it
 * (`createNotificationChannel` updates the name and the description), which is
 * how they follow the in-app language: see [registerAll].
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

    /**
     * Registers every channel in [context]'s language, renaming the existing ones. MainActivity calls
     * it with its own context, which carries the in-app language on every API level, at start and
     * after a language change.
     */
    fun registerAll(context: Context) {
        val manager = context.getSystemService<NotificationManager>() ?: return
        manager.createNotificationChannels(channels(context))
    }

    /**
     * Creates only the channels that do not exist yet, so a push can be posted before the app is
     * first opened. The Application calls this rather than [registerAll]: on API 26–32 its context
     * resolves in the DEVICE language, and a process a push cold-starts would rename every channel
     * back into it.
     */
    fun registerMissing(context: Context) {
        val manager = context.getSystemService<NotificationManager>() ?: return
        val missing = channels(context).filter { manager.getNotificationChannel(it.id) == null }
        if (missing.isNotEmpty()) manager.createNotificationChannels(missing)
    }

    private fun channels(context: Context): List<NotificationChannel> =
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
        )

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
