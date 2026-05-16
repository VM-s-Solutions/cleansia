package cz.cleansia.customer.core.notifications

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import androidx.core.content.getSystemService
import cz.cleansia.customer.R

/**
 * Registers one [NotificationChannel] per [NotificationCategoryDto] at app
 * start. Android dedupes by channel id, so calling [registerAll] on every
 * cold start is cheap and safe.
 *
 * Why one channel per category: gives users system-level granular control
 * — long-press a notification → "Stop showing this category" — without
 * us needing to ship a separate mute UI for that level. Our in-app
 * toggles (UserNotificationPreferences) shut things off server-side; the
 * channel toggle is a belt-and-suspenders defense at the OS layer.
 *
 * Phase A only fires three categories (OrderUpdates, OrderCompleted,
 * DisputeReply), but we register all 11 up-front so the user can mute
 * future ones before we ship them.
 */
object NotificationChannels {

    /** Channel id used in the notification payload + builder. Stable for the lifetime of the install. */
    fun channelIdFor(category: NotificationCategoryDto): String =
        "cleansia.notification.${category.name}"

    fun registerAll(context: Context) {
        val manager = context.getSystemService<NotificationManager>() ?: return

        val channels = listOf(
            channel(context, NotificationCategoryDto.OrderUpdates,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_HIGH),
            channel(context, NotificationCategoryDto.OrderCompleted,
                R.string.notification_channel_order_completed_name,
                R.string.notification_channel_order_completed_desc,
                NotificationManager.IMPORTANCE_HIGH),
            channel(context, NotificationCategoryDto.DisputeReply,
                R.string.notification_channel_dispute_reply_name,
                R.string.notification_channel_dispute_reply_desc,
                NotificationManager.IMPORTANCE_HIGH),
            // Phase B/C/D channels — registered now so users can pre-mute,
            // even though the events that drive them aren't wired yet.
            // Names reuse the Phase A strings for now; replaced when each
            // phase lands its full string set.
            channel(context, NotificationCategoryDto.CleanerOnTheWay,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_HIGH),
            channel(context, NotificationCategoryDto.OrderCancelled,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_HIGH),
            channel(context, NotificationCategoryDto.RefundIssued,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_DEFAULT),
            channel(context, NotificationCategoryDto.MembershipExpiring,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_DEFAULT),
            channel(context, NotificationCategoryDto.MembershipCancelled,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_DEFAULT),
            channel(context, NotificationCategoryDto.TierUpgrade,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_DEFAULT),
            channel(context, NotificationCategoryDto.Promo,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_LOW),
            channel(context, NotificationCategoryDto.RecurringScheduled,
                R.string.notification_channel_order_updates_name,
                R.string.notification_channel_order_updates_desc,
                NotificationManager.IMPORTANCE_DEFAULT),
        )

        manager.createNotificationChannels(channels)
    }

    private fun channel(
        context: Context,
        category: NotificationCategoryDto,
        nameRes: Int,
        descRes: Int,
        importance: Int,
    ): NotificationChannel {
        return NotificationChannel(
            channelIdFor(category),
            context.getString(nameRes),
            importance,
        ).apply {
            description = context.getString(descRes)
        }
    }
}
