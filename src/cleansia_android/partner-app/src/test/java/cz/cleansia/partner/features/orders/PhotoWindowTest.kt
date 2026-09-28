package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PhotoType
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

/**
 * The server refuses a photo outside `OrderPhoto.MayBeAddedAt`, so the rails open on exactly the
 * same statuses.
 */
class PhotoWindowTest {

    @Test
    fun `before photos are open from Confirmed through InProgress`() {
        assertEquals(
            listOf(OrderStatus._2, OrderStatus._3, OrderStatus._4),
            OrderStatus.entries.filter { photoWindowOpen(PhotoType._1, it) },
        )
    }

    @Test
    fun `after photos are open only while InProgress`() {
        assertEquals(
            listOf(OrderStatus._4),
            OrderStatus.entries.filter { photoWindowOpen(PhotoType._2, it) },
        )
    }

    @Test
    fun `a status this build does not know opens neither rail`() {
        assertFalse(photoWindowOpen(PhotoType._1, null))
        assertFalse(photoWindowOpen(PhotoType._2, null))
    }
}
