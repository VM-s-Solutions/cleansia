package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class OrderStatusPresentationTest {

    @Test
    fun `every backend ordinal maps onto a typed status`() {
        (0..6).forEach { ordinal ->
            assertEquals("ordinal $ordinal", ordinal, Code(value = ordinal).toOrderStatus()?.value)
        }
        assertNull(Code(value = 99).toOrderStatus())
        assertNull((null as Code?).toOrderStatus())
    }
}
