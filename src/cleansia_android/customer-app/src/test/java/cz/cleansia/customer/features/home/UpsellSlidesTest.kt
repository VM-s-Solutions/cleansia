package cz.cleansia.customer.features.home

import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.features.booking.cancellationPolicyFor
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Which Home carousel slides show, in which order, and that no two of them draw the same mascot —
 * every permutation of the inputs, because a duplicate, a short set or an overlong one only appears
 * for some users. The iOS twin (UpsellSlideTests) pins the same order, cap and mapping.
 */
class UpsellSlidesTest {

    private val bools = listOf(false, true)

    /** The slide's hours for a member whose plan carries [window], read the way Home reads them. */
    private fun memberHours(window: Int?): Int = cancellationPolicyFor(
        GetMyMembershipResponse(hasMembership = true, freeCancellationWindowHours = window),
    ).plusFreeHours ?: 0

    /** Every combination of the inputs; the member's plan window is none, 4 h, or the standard 24 h. */
    private val permutations: List<List<UpsellKind>> =
        bools.flatMap { notifications ->
            bools.flatMap { credit ->
                bools.flatMap { express ->
                    bools.flatMap { setup ->
                        bools.flatMap { isPlus ->
                            listOf(null, 4, 24).map { window ->
                                upsellKinds(
                                    notificationsOff = notifications,
                                    hasCredit = credit,
                                    expressAvailable = express,
                                    showSetupRecurring = setup,
                                    isPlus = isPlus,
                                    memberCancellationHours = memberHours(window),
                                )
                            }
                        }
                    }
                }
            }
        }

    @Test
    fun `every customer sees five slides, closed by quick-size`() {
        permutations.forEach { kinds ->
            assertEquals("$kinds is not five slides", UPSELL_LEADING_CAP + 1, kinds.size)
            assertEquals("$kinds does not close on quick-size", UpsellKind.QuickSize, kinds.last())
            assertEquals("$kinds shows quick-size twice", 1, kinds.count { it == UpsellKind.QuickSize })
            assertEquals("$kinds repeats a slide", kinds.size, kinds.toSet().size)
        }
    }

    @Test
    fun `a non-member with nothing else pending sees Plus, referral, then the facts`() {
        assertEquals(
            listOf(UpsellKind.Plus, UpsellKind.Referral, UpsellKind.ExpressToday, UpsellKind.Rewards, UpsellKind.QuickSize),
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
    fun `with everything eligible the state slides fill the set`() {
        assertEquals(
            listOf(UpsellKind.Notifications, UpsellKind.Credit, UpsellKind.Express, UpsellKind.SetupRecurring, UpsellKind.QuickSize),
            upsellKinds(
                notificationsOff = true,
                hasCredit = true,
                expressAvailable = true,
                showSetupRecurring = true,
                isPlus = true,
                memberCancellationHours = 4,
            ),
        )
    }

    @Test
    fun `the facts come after referral, in the order cancellation, express-today, rewards, arrival times`() {
        assertEquals(
            listOf(UpsellKind.Referral, UpsellKind.PlusCancellation, UpsellKind.ExpressToday, UpsellKind.Rewards, UpsellKind.QuickSize),
            upsellKinds(
                notificationsOff = false,
                hasCredit = false,
                expressAvailable = false,
                showSetupRecurring = false,
                isPlus = true,
                memberCancellationHours = 4,
            ),
        )
        // A member whose renewal failed has no benefit to name, so the data-free facts fill in.
        assertEquals(
            listOf(UpsellKind.Referral, UpsellKind.ExpressToday, UpsellKind.Rewards, UpsellKind.ArrivalTimes, UpsellKind.QuickSize),
            upsellKinds(notificationsOff = false, hasCredit = false, expressAvailable = false, showSetupRecurring = false, isPlus = true),
        )
    }

    /** A plan may carry anything up to 24 h, and 24 h is the window everyone gets, so it is no benefit. */
    @Test
    fun `a member whose window is the standard one sees no cancellation slide`() {
        assertEquals(0, memberHours(24))
        assertEquals(
            listOf(UpsellKind.Referral, UpsellKind.ExpressToday, UpsellKind.Rewards, UpsellKind.ArrivalTimes, UpsellKind.QuickSize),
            upsellKinds(
                notificationsOff = false,
                hasCredit = false,
                expressAvailable = false,
                showSetupRecurring = false,
                isPlus = true,
                memberCancellationHours = memberHours(24),
            ),
        )
        val home = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer/features/home/HomeTab.kt") }
            .first { it.isFile }
            .readText()
        assertTrue(
            "Home no longer reads the member's window by the confirm step's rule",
            home.contains("val memberCancellationHours = cancellationPolicyFor(membership).plusFreeHours ?: 0"),
        )
    }

    @Test
    fun `express-today never repeats the member's express slide`() {
        permutations.forEach { kinds ->
            assertTrue("$kinds shows both express slides", !(UpsellKind.Express in kinds && UpsellKind.ExpressToday in kinds))
        }
    }

    @Test
    fun `a member never sees the Plus upsell, and a non-member never sees a member's benefit`() {
        assertTrue(
            upsellKinds(notificationsOff = false, hasCredit = false, expressAvailable = false, showSetupRecurring = false, isPlus = true)
                .none { it == UpsellKind.Plus },
        )
        assertTrue(
            upsellKinds(
                notificationsOff = false,
                hasCredit = false,
                expressAvailable = false,
                showSetupRecurring = false,
                isPlus = false,
                memberCancellationHours = 4,
            ).none { it == UpsellKind.PlusCancellation },
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
        assertEquals(R.drawable.mascot_leaning, UpsellKind.PlusCancellation.mascotRes())
        assertEquals(R.drawable.mascot_ready, UpsellKind.ExpressToday.mascotRes())
        assertEquals(R.drawable.mascot_spray_and_cloth, UpsellKind.Rewards.mascotRes())
        assertEquals(R.drawable.mascot_resting, UpsellKind.ArrivalTimes.mascotRes())
        assertEquals(R.drawable.mascot_vacuuming, UpsellKind.QuickSize.mascotRes())
    }

    /** The slide states the When step's own first and last arrival, never a pair of its own. */
    @Test
    fun `the arrival-times slide names the booking's first and last arrival`() {
        assertEquals("08:00" to "19:45", upsellArrivalBounds())
    }
}
