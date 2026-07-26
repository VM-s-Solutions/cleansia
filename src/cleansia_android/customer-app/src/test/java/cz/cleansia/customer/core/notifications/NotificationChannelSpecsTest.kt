package cz.cleansia.customer.core.notifications

import android.app.NotificationManager
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Guards the channel table. Android CI runs no lint, so nothing else would
 * notice if a new category were pasted in carrying a neighbour's strings —
 * which is exactly how eight of these channels ended up all called
 * "Order updates" in the system settings list, making per-category muting
 * useless.
 */
class NotificationChannelSpecsTest {

    @Test
    fun `every category is registered exactly once`() {
        assertEquals(
            NotificationCategoryDto.entries.toSet(),
            notificationChannelSpecs.map { it.category }.toSet(),
        )
        assertEquals(
            NotificationCategoryDto.entries.size,
            notificationChannelSpecs.size,
        )
    }

    @Test
    fun `no two channels share a name string`() {
        val duplicated = notificationChannelSpecs
            .groupBy { it.nameRes }
            .filterValues { it.size > 1 }
            .values
            .map { specs -> specs.map { it.category.name } }

        assertEquals(emptyList<List<String>>(), duplicated)
    }

    @Test
    fun `no two channels share a description string`() {
        val duplicated = notificationChannelSpecs
            .groupBy { it.descRes }
            .filterValues { it.size > 1 }
            .values
            .map { specs -> specs.map { it.category.name } }

        assertEquals(emptyList<List<String>>(), duplicated)
    }

    /**
     * Importance is immutable once a channel exists on a device — the OS
     * ignores it on re-registration (the user may only lower it). Renaming
     * channels therefore reaches existing installs, but re-importancing them
     * does not, so any change here would apply to fresh installs only and
     * split the fleet in two. Pinned so that is a deliberate act.
     */
    @Test
    fun `importance per category is unchanged`() {
        val expected = mapOf(
            NotificationCategoryDto.OrderUpdates to NotificationManager.IMPORTANCE_HIGH,
            NotificationCategoryDto.OrderCompleted to NotificationManager.IMPORTANCE_HIGH,
            NotificationCategoryDto.DisputeReply to NotificationManager.IMPORTANCE_HIGH,
            NotificationCategoryDto.CleanerOnTheWay to NotificationManager.IMPORTANCE_HIGH,
            NotificationCategoryDto.OrderCancelled to NotificationManager.IMPORTANCE_HIGH,
            NotificationCategoryDto.RefundIssued to NotificationManager.IMPORTANCE_DEFAULT,
            NotificationCategoryDto.MembershipExpiring to NotificationManager.IMPORTANCE_DEFAULT,
            NotificationCategoryDto.MembershipCancelled to NotificationManager.IMPORTANCE_DEFAULT,
            NotificationCategoryDto.TierUpgrade to NotificationManager.IMPORTANCE_DEFAULT,
            NotificationCategoryDto.Promo to NotificationManager.IMPORTANCE_LOW,
            NotificationCategoryDto.RecurringScheduled to NotificationManager.IMPORTANCE_DEFAULT,
        )

        assertEquals(expected, notificationChannelSpecs.associate { it.category to it.importance })
    }

    /** Baked into every install's system settings and into the FCM service's lookup. */
    @Test
    fun `channel ids are stable and unique`() {
        assertEquals(
            "cleansia.notification.Promo",
            NotificationChannels.channelIdFor(NotificationCategoryDto.Promo),
        )
        assertEquals(
            notificationChannelSpecs.size,
            notificationChannelSpecs.map { NotificationChannels.channelIdFor(it.category) }.toSet().size,
        )
    }
}
