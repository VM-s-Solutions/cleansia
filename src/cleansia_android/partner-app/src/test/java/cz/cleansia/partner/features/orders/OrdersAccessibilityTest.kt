package cz.cleansia.partner.features.orders

import org.junit.Assert.assertEquals
import org.junit.Test

/** One spoken sentence per card in the visual order, and action buttons that name their job (spec §6.6). */
class OrdersAccessibilityTest {

    @Test
    fun `clauses join with commas inside and full stops between, skipping what is missing`() {
        assertEquals(
            "Tomorrow, 1 November, 08:00–10:00, you earn 780 Kč. Praha 3, 2 h. Standard clean, 2 rooms. Customer pays you 1 290 Kč in cash.",
            spokenSentence(
                listOf("Tomorrow, 1 November", "08:00–10:00", "you earn 780 Kč"),
                listOf("Praha 3", null, "2 h"),
                listOf("Standard clean, 2 rooms"),
                listOf(" "),
                listOf("Customer pays you 1 290 Kč in cash."),
            ),
        )
    }

    @Test
    fun `visual middle dots are spoken as commas`() {
        assertEquals("Standard clean, 2 rooms, 1 bath, + oven.", spokenSentence(listOf("Standard clean · 2 rooms · 1 bath · + oven")))
        // The board's separator keeps a no-break space before the dot so a wrapped line never starts with it.
        assertEquals("Standard clean, 2 rooms, + oven.", spokenSentence(listOf("Standard clean · 2 rooms · + oven")))
    }

    @Test
    fun `an action names the day and window of its job`() {
        assertEquals("Take, Tomorrow, 1 November, 08:00–10:00", actionLabel("Take", "Tomorrow, 1 November", "08:00–10:00"))
        assertEquals("Confirm, 08:00–10:00", actionLabel("Confirm", null, "08:00–10:00"))
    }

    @Test
    fun `the hero says its status, day and date before the window, the place, the scope with the team, you earn, the cash and the locked slide's when`() {
        assertEquals(
            "Setting off, in 4 days. Monday, 16 November, 08:00–10:00. Hornoměcholupská 12, Praha 10, Marie Svobodová. " +
                "Standard clean, 1 room, team of 2. you earn 714 Kč. Cash: collect 1 240 Kč for the company. " +
                "You can set off from 07:00, Monday, 16 Nov.",
            heroSentence(
                status = listOf("Setting off", "in 4 days"),
                day = "Monday, 16 November",
                window = "08:00–10:00",
                place = listOf("Hornoměcholupská 12", "Praha 10", "Marie Svobodová"),
                scope = listOf("Standard clean", "1 room", "team of 2"),
                pay = "you earn 714 Kč",
                cash = "Cash: collect 1 240 Kč for the company",
                setOff = listOf("You can set off from 07:00", "Monday" + OrdersBoardRules.SEP + "16 Nov"),
            ),
        )
    }
}
