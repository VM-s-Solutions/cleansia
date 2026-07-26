package cz.cleansia.customer.core.notifications

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import androidx.core.content.getSystemService
import cz.cleansia.customer.R

/**
 * One registered [NotificationChannel]: which category it serves, the two
 * strings the OS shows for it, and how loudly it arrives.
 *
 * Pulled out of [NotificationChannels.registerAll] so the table can be
 * asserted on in a plain JVM test — the actual channel objects need a
 * [Context] and an Android runtime, the table does not.
 */
internal data class NotificationChannelSpec(
    val category: NotificationCategoryDto,
    val nameRes: Int,
    val descRes: Int,
    val importance: Int,
)

/**
 * The complete channel table, one row per [NotificationCategoryDto].
 *
 * Every row's name/description are the same strings the matching in-app
 * toggle renders on the notification-settings screen, so the OS category a
 * user mutes reads exactly like the switch they just tapped. The three
 * Phase A channels keep their older dedicated `notification_channel_*`
 * strings, which say the same thing in longer form.
 *
 * NOTE: names and descriptions are read once, at [NotificationChannels.registerAll]
 * time — i.e. in `CleansiaApp.onCreate`. Switching the in-app language
 * recreates activities but not the process, so the OS-level names follow the
 * new locale on the next cold start, not immediately. Do not "fix" that by
 * re-registering on locale change: re-registration cannot raise an
 * importance the user has lowered, and touching importance here is how you
 * end up with a fleet that behaves two different ways.
 */
internal val notificationChannelSpecs: List<NotificationChannelSpec> = listOf(
    NotificationChannelSpec(NotificationCategoryDto.OrderUpdates,
        R.string.notification_channel_order_updates_name,
        R.string.notification_channel_order_updates_desc,
        NotificationManager.IMPORTANCE_HIGH),
    NotificationChannelSpec(NotificationCategoryDto.OrderCompleted,
        R.string.notification_channel_order_completed_name,
        R.string.notification_channel_order_completed_desc,
        NotificationManager.IMPORTANCE_HIGH),
    NotificationChannelSpec(NotificationCategoryDto.DisputeReply,
        R.string.notification_channel_dispute_reply_name,
        R.string.notification_channel_dispute_reply_desc,
        NotificationManager.IMPORTANCE_HIGH),
    // Phase B/C/D channels — registered now so users can pre-mute, even
    // though the events that drive them aren't wired yet.
    NotificationChannelSpec(NotificationCategoryDto.CleanerOnTheWay,
        R.string.notifications_cleaner_messages,
        R.string.notifications_cleaner_messages_desc,
        NotificationManager.IMPORTANCE_HIGH),
    NotificationChannelSpec(NotificationCategoryDto.OrderCancelled,
        R.string.notifications_order_cancelled,
        R.string.notifications_order_cancelled_desc,
        NotificationManager.IMPORTANCE_HIGH),
    NotificationChannelSpec(NotificationCategoryDto.RefundIssued,
        R.string.notifications_refund_issued,
        R.string.notifications_refund_issued_desc,
        NotificationManager.IMPORTANCE_DEFAULT),
    NotificationChannelSpec(NotificationCategoryDto.MembershipExpiring,
        R.string.notifications_membership_expiring,
        R.string.notifications_membership_expiring_desc,
        NotificationManager.IMPORTANCE_DEFAULT),
    NotificationChannelSpec(NotificationCategoryDto.MembershipCancelled,
        R.string.notifications_membership_cancelled,
        R.string.notifications_membership_cancelled_desc,
        NotificationManager.IMPORTANCE_DEFAULT),
    NotificationChannelSpec(NotificationCategoryDto.TierUpgrade,
        R.string.notifications_tier_upgrade,
        R.string.notifications_tier_upgrade_desc,
        NotificationManager.IMPORTANCE_DEFAULT),
    NotificationChannelSpec(NotificationCategoryDto.Promo,
        R.string.notifications_promos,
        R.string.notifications_promos_desc,
        NotificationManager.IMPORTANCE_LOW),
    NotificationChannelSpec(NotificationCategoryDto.RecurringScheduled,
        R.string.notifications_reminders,
        R.string.notifications_reminders_desc,
        NotificationManager.IMPORTANCE_DEFAULT),
)

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
        manager.createNotificationChannels(notificationChannelSpecs.map { channel(context, it) })
    }

    private fun channel(context: Context, spec: NotificationChannelSpec): NotificationChannel {
        return NotificationChannel(
            channelIdFor(spec.category),
            context.getString(spec.nameRes),
            spec.importance,
        ).apply {
            description = context.getString(spec.descRes)
        }
    }
}
