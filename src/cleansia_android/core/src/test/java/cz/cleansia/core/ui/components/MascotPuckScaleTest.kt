package cz.cleansia.core.ui.components

import org.junit.Assert.assertEquals
import org.junit.Test

class MascotPuckScaleTest {

    @Test
    fun `the customer's two still figures keep the scales they shipped with`() {
        assertEquals(0.89752f, MascotPuckScale.Leaning)
        assertEquals(0.85773f, MascotPuckScale.Ready)
    }

    @Test
    fun `the waving figure's ledge is 0_797 of its art, carried through the puck's scale about the centre`() {
        assertEquals(0.5f + (0.797f - 0.5f) * MascotPuckScale.Waving, MascotPuckScale.WavingLedge, 0.00001f)
        assertEquals(0.76918f, MascotPuckScale.WavingLedge, 0.00001f)
    }
}
