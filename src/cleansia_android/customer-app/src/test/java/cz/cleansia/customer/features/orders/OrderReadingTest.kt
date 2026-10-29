package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderPhotoDto
import cz.cleansia.customer.core.orders.OrderStatusTrackDto
import cz.cleansia.customer.core.user.CodeDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class OrderReadingTest {
    private fun start(at: String) = OrderStatusTrackDto(
        status = CodeDto(type = "OrderStatus", name = "InProgress", value = 4),
        createdOn = at,
    )

    @Test
    fun `estimate becomes overdue without completing the order`() {
        val progress = cleaningEstimateProgress(listOf(start("2026-10-28T10:00:00Z")), 10, 1793182261L)!!
        assertEquals(1f, progress.fraction)
        assertEquals(2L, progress.overrunMinutes)
        assertEquals(0L, progress.remainingMinutes)
    }

    @Test
    fun `before the estimate the fourth segment is fractional`() {
        val progress = cleaningEstimateProgress(listOf(start("2026-10-28T10:00:00Z")), 10, 1793181900L)!!
        assertEquals(0.5f, progress.fraction)
        assertEquals(0L, progress.overrunMinutes)
        assertEquals(5L, progress.remainingMinutes)
    }

    @Test
    fun `missing invalid future starts and absent estimates fabricate no progress`() {
        assertNull(cleaningEstimateProgress(null, 10, 0))
        assertNull(cleaningEstimateProgress(listOf(start("bad")), 10, 0))
        assertNull(cleaningEstimateProgress(listOf(start("2026-10-28T10:00:00Z")), 10, 0))
        assertNull(cleaningEstimateProgress(listOf(start("2026-10-28T10:00:00Z")), 0, 1793181900L))
    }

    @Test
    fun `the newest valid start wins independently of response ordering`() {
        val history = listOf(start("2026-10-28T10:00:00Z"), start("2026-10-28T09:00:00Z"))
        assertEquals(0.5f, cleaningEstimateProgress(history, 10, 1793181900L)!!.fraction)
    }

    @Test
    fun `phase partition retains entrance and unknown evidence without calling them before`() {
        val photos = listOf(1, 2, 3, 99, null).mapIndexed { index, phase ->
            OrderPhotoDto(id = index.toString(), photoType = phase)
        }
        val groups = groupOrderPhotos(photos)
        assertEquals(listOf(1, 2, 3, 0), groups.map { it.phase })
        assertEquals(listOf("0"), groups[0].photos.map { it.id })
        assertEquals(listOf("1"), groups[1].photos.map { it.id })
        assertEquals(listOf("2"), groups[2].photos.map { it.id })
        assertEquals(listOf("3", "4"), groups[3].photos.map { it.id })
        assertEquals(photos.size, groups.sumOf { it.photos.size })
    }

    @Test
    fun `selected image follows its identity when a fresh gallery response is reordered`() {
        val group = OrderPhotoGroup(2, listOf(OrderPhotoDto(id = "new"), OrderPhotoDto(id = "selected")))
        assertEquals(1, group.indexOfPhoto("selected"))
        assertNull(group.indexOfPhoto("removed"))
        assertNull(group.indexOfPhoto(null))
    }

    @Test
    fun `empty before and after remain explicit collections`() {
        assertEquals(listOf(1, 2), groupOrderPhotos(emptyList()).map { it.phase })
    }
}
