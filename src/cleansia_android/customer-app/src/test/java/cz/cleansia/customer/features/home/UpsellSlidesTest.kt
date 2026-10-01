package cz.cleansia.customer.features.home

import cz.cleansia.customer.R
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Which Home carousel slides show, in which order, and that no two of them draw the same mascot —
 * every permutation of the inputs, because the duplicate the owner saw only appears for some users.
 * The iOS twin (UpsellSlideTests) pins the same order and the same mapping.
 */
class UpsellSlidesTest {

    private val permutations = listOf(false, true).flatMap { isPlus ->
        listOf(false, true).map { setup -> isPlus to setup }
    }

    @Test
    fun `a non-member sees Plus first, then referral and book`() {
        assertEquals(
            listOf(UpsellKind.Plus, UpsellKind.Referral, UpsellKind.Book),
            upsellKinds(isPlus = false, showSetupRecurring = false),
        )
    }

    @Test
    fun `a member with no schedule is offered one, then referral and book`() {
        assertEquals(
            listOf(UpsellKind.SetupRecurring, UpsellKind.Referral, UpsellKind.Book),
            upsellKinds(isPlus = true, showSetupRecurring = true),
        )
    }

    @Test
    fun `a member with a schedule sees referral and book`() {
        assertEquals(
            listOf(UpsellKind.Referral, UpsellKind.Book),
            upsellKinds(isPlus = true, showSetupRecurring = false),
        )
    }

    @Test
    fun `every visible slide draws its own mascot`() {
        permutations.forEach { (isPlus, setup) ->
            val mascots = upsellKinds(isPlus, setup).map { it.mascotRes() }
            assertEquals("isPlus=$isPlus setup=$setup repeats a mascot", mascots.size, mascots.toSet().size)
        }
    }

    @Test
    fun `the Plus slide draws the Plus mascot and referral the thumbs-up`() {
        assertEquals(R.drawable.mascot_plus, UpsellKind.Plus.mascotRes())
        assertEquals(R.drawable.mascot_idea, UpsellKind.SetupRecurring.mascotRes())
        assertEquals(R.drawable.mascot_thumbs_up, UpsellKind.Referral.mascotRes())
        assertEquals(R.drawable.mascot_cleaning, UpsellKind.Book.mascotRes())
    }
}
