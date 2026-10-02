package cz.cleansia.customer.features.home

import cz.cleansia.customer.R
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Which Home carousel slides show, in which order, and that no two of them draw the same mascot —
 * every permutation of the inputs, because a duplicate or an overlong set only appears for some
 * users. The iOS twin (UpsellSlideTests) pins the same order, cap and mapping.
 */
class UpsellSlidesTest {

    private val bools = listOf(false, true)

    /** Every combination of the five inputs. */
    private val permutations: List<List<UpsellKind>> =
        bools.flatMap { notifications ->
            bools.flatMap { credit ->
                bools.flatMap { express ->
                    bools.flatMap { setup ->
                        bools.map { isPlus ->
                            upsellKinds(
                                notificationsOff = notifications,
                                hasCredit = credit,
                                expressAvailable = express,
                                showSetupRecurring = setup,
                                isPlus = isPlus,
                            )
                        }
                    }
                }
            }
        }

    @Test
    fun `a non-member with nothing else pending sees Plus, referral and the quick-size closer`() {
        assertEquals(
            listOf(UpsellKind.Plus, UpsellKind.Referral, UpsellKind.QuickSize),
            upsellKinds(notificationsOff = false, hasCredit = false, expressAvailable = false, showSetupRecurring = false, isPlus = false),
        )
    }

    @Test
    fun `the order is notifications, credit, express, setup-recurring, Plus, referral`() {
        assertEquals(
            listOf(UpsellKind.Notifications, UpsellKind.Credit, UpsellKind.Plus, UpsellKind.Referral, UpsellKind.QuickSize),
            upsellKinds(notificationsOff = true, hasCredit = true, expressAvailable = false, showSetupRecurring = false, isPlus = false),
        )
        assertEquals(
            listOf(UpsellKind.Credit, UpsellKind.Express, UpsellKind.SetupRecurring, UpsellKind.Referral, UpsellKind.QuickSize),
            upsellKinds(notificationsOff = false, hasCredit = true, expressAvailable = true, showSetupRecurring = true, isPlus = true),
        )
    }

    /** Four leading slides at most, so with everything eligible the referral slide is the one that gives way. */
    @Test
    fun `the first four eligible slides show, then quick-size always closes`() {
        assertEquals(
            listOf(UpsellKind.Notifications, UpsellKind.Credit, UpsellKind.Express, UpsellKind.SetupRecurring, UpsellKind.QuickSize),
            upsellKinds(notificationsOff = true, hasCredit = true, expressAvailable = true, showSetupRecurring = true, isPlus = true),
        )
        permutations.forEach { kinds ->
            assertTrue("$kinds is longer than five", kinds.size <= UPSELL_LEADING_CAP + 1)
            assertEquals("$kinds does not close on quick-size", UpsellKind.QuickSize, kinds.last())
            assertEquals("$kinds shows quick-size twice", 1, kinds.count { it == UpsellKind.QuickSize })
        }
    }

    @Test
    fun `a member never sees the Plus upsell`() {
        assertTrue(
            upsellKinds(notificationsOff = false, hasCredit = false, expressAvailable = false, showSetupRecurring = false, isPlus = true)
                .none { it == UpsellKind.Plus },
        )
    }

    @Test
    fun `every visible slide draws its own mascot, in every permutation`() {
        permutations.forEach { kinds ->
            val mascots = kinds.map { it.mascotRes() }
            assertEquals("$kinds repeats a mascot", mascots.size, mascots.toSet().size)
        }
        val all = UpsellKind.entries.map { it.mascotRes() }
        assertEquals("two kinds share a mascot", all.size, all.toSet().size)
    }

    @Test
    fun `each slide draws the mascot iOS draws for it`() {
        assertEquals(R.drawable.mascot_waving, UpsellKind.Notifications.mascotRes())
        assertEquals(R.drawable.mascot_invoice, UpsellKind.Credit.mascotRes())
        assertEquals(R.drawable.mascot_floor_scrubber, UpsellKind.Express.mascotRes())
        assertEquals(R.drawable.mascot_idea, UpsellKind.SetupRecurring.mascotRes())
        assertEquals(R.drawable.mascot_plus, UpsellKind.Plus.mascotRes())
        assertEquals(R.drawable.mascot_thumbs_up, UpsellKind.Referral.mascotRes())
        assertEquals(R.drawable.mascot_vacuuming, UpsellKind.QuickSize.mascotRes())
    }
}
