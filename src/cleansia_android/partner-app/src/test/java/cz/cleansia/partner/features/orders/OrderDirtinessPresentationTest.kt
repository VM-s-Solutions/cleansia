package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.DirtinessLevel
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The level is an integer on the wire and a `@Contextual` enum in the generated models, so these
 * decode it through the production `Json` config rather than constructing the enum by hand — the
 * card and the detail read what the server actually sends.
 */
class OrderDirtinessPresentationTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private val labels = mapOf(
        DirtinessLevel._1 to R.string.dirtiness_level_increased,
        DirtinessLevel._2 to R.string.dirtiness_level_heavy,
    )

    /** Dirt only when increased or heavy; a normal home says nothing about it. */
    @Test
    fun `the detail names the increased and heavy levels only`() {
        labels.forEach { (level, res) ->
            assertEquals("$level", res, dirtinessLevelLabelRes(level))
        }
        assertNull(dirtinessLevelLabelRes(DirtinessLevel._0))
    }

    @Test
    fun `the board card flags only a dirtier than normal home`() {
        assertNull(dirtinessChipLabelRes(DirtinessLevel._0))
        assertEquals(R.string.dirtiness_level_increased, dirtinessChipLabelRes(DirtinessLevel._1))
        assertEquals(R.string.dirtiness_level_heavy, dirtinessChipLabelRes(DirtinessLevel._2))
    }

    @Test
    fun `a server that sends no level shows none`() {
        assertNull(dirtinessLevelLabelRes(null))
        assertNull(dirtinessChipLabelRes(null))
        val card = json.decodeFromString(OrderListItem.serializer(), """{ "id": "ord-1" }""")
        assertNull(dirtinessChipLabelRes(card.dirtinessLevel))
    }

    @Test
    fun `a heavy job on the board decodes to the heavy chip`() {
        val card = json.decodeFromString(
            OrderListItem.serializer(),
            """{ "id": "ord-1", "dirtinessLevel": 2, "dirtinessSurchargeAmount": 720.0 }""",
        )

        assertEquals(R.string.dirtiness_level_heavy, dirtinessChipLabelRes(card.dirtinessLevel))
    }

    @Test
    fun `an increased job detail decodes to the increased label`() {
        val detail = json.decodeFromString(
            OrderItem.serializer(),
            """{ "id": "ord-1", "dirtinessLevel": 1, "dirtinessSurchargeAmount": 360.0 }""",
        )

        assertEquals(R.string.dirtiness_level_increased, dirtinessLevelLabelRes(detail.dirtinessLevel))
    }

    @Test
    fun `a normal job detail names no level`() {
        val detail = json.decodeFromString(OrderItem.serializer(), """{ "id": "ord-1", "dirtinessLevel": 0 }""")

        assertNull(dirtinessLevelLabelRes(detail.dirtinessLevel))
    }
}
