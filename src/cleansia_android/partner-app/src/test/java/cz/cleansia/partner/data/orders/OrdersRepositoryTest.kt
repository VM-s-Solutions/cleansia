package cz.cleansia.partner.data.orders

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Pins the [SessionScopedCache] contract of [OrdersRepositoryImpl]: on sign-out
 * every freshness watermark (per-pane list + per-order detail) resets, so the
 * next account's first fetch is never gated out as "fresh" by the prior user's
 * cache — per Staleness.kt's documented reset-from-clear() instruction.
 */
class OrdersRepositoryTest {

    private lateinit var orderApi: OrderApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        orderApi = mockk()
    }

    private fun newRepo() = OrdersRepositoryImpl(orderApi, json)

    @Test
    fun clear_resetsPaneWatermarks() = runTest {
        coEvery {
            orderApi.orderGetPaged(
                any(), any(), any(), any(), any(), any(), any(), any(), any(), any(),
                any(), any(), any(), any(), any(), any(), any(), any(), any(), any(),
            )
        } returns Response.success(mockk<PagedDataOfOrderListItem>(relaxed = true))
        val repo = newRepo()
        repo.getPaged(pane = OrdersPane.Active)
        assertFalse("pane should be fresh after a successful fetch", repo.isPaneStale(OrdersPane.Active))

        (repo as SessionScopedCache).clear()

        assertTrue("pane must be stale again after clear()", repo.isPaneStale(OrdersPane.Active))
    }

    @Test
    fun clear_resetsPerOrderWatermarks() = runTest {
        coEvery { orderApi.orderGetById("order-1") } returns Response.success(mockk<OrderItem>(relaxed = true))
        val repo = newRepo()
        repo.getById("order-1")
        assertFalse("order should be fresh after a successful fetch", repo.isOrderStale("order-1"))

        (repo as SessionScopedCache).clear()

        assertTrue("order must be stale again after clear()", repo.isOrderStale("order-1"))
    }
}
