package cz.cleansia.partner.data.orders

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.DeclinePreferredOfferCommand
import cz.cleansia.partner.api.model.DeclinePreferredOfferResponse
import cz.cleansia.partner.api.model.PendingOfferItem
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * The cleaner-facing half of ADR-0045: what is reserved for this cleaner right now, and the one write
 * that refuses it. Both endpoints already existed on the partner mobile host and no client called
 * either, so everything here is a first caller.
 */
class OrdersRepositoryPendingOffersTest {

    private lateinit var orderApi: OrderApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        orderApi = mockk()
    }

    private fun newRepo() = OrdersRepositoryImpl(orderApi, json)

    private fun offer(id: String) = PendingOfferItem(
        id = id,
        displayOrderNumber = "CL-$id",
        cleaningDateTime = "2026-08-12T09:00:00Z",
        estimatedTime = 120,
        respondByUtc = "2026-08-10T18:40:00Z",
        customerAddressApproximate = "Praha 4 · 14000",
        rooms = 2,
        bathrooms = 1,
        totalPrice = 1200.0,
        currencyCode = "CZK",
    )

    @Test
    fun `a successful fetch publishes the server's rows and marks the surface fresh`() = runTest {
        val repo = newRepo()
        assertTrue("a repo that has never fetched must be stale", repo.arePendingOffersStale())
        coEvery { orderApi.orderMyPendingOffers() } returns Response.success(listOf(offer("a"), offer("b")))

        val result = repo.refreshPendingOffers()

        assertTrue(result is ApiResult.Success)
        assertEquals(listOf("a", "b"), repo.pendingOffers.value.map { it.id })
        assertFalse(repo.arePendingOffersStale())
    }

    @Test
    fun `a failed fetch neither publishes nor claims freshness`() = runTest {
        val repo = newRepo()
        coEvery { orderApi.orderMyPendingOffers() } returns Response.error(
            500,
            "{}".toResponseBody("application/json".toMediaType()),
        )

        val result = repo.refreshPendingOffers()

        assertTrue(result is ApiResult.Error)
        assertEquals(emptyList<PendingOffer>(), repo.pendingOffers.value)
        assertTrue("a transient failure must not pretend the cache is warm", repo.arePendingOffersStale())
    }

    /**
     * Asserts the GENERATED command, not an app-side one: every field on an OpenAPI-generated command
     * is optional with a `= null` default, so an omitted mapping compiles and the wire silently carries
     * no order id — a decline that refuses nothing and is reported as a success.
     */
    @Test
    fun `declining sends the order id on the generated command`() = runTest {
        val repo = newRepo()
        val command = slot<DeclinePreferredOfferCommand>()
        coEvery { orderApi.orderDeclinePreferredOffer(capture(command)) } returns
            Response.success(DeclinePreferredOfferResponse(orderId = "order-1"))

        repo.declinePreferredOffer("order-1")

        coVerify(exactly = 1) { orderApi.orderDeclinePreferredOffer(any()) }
        assertEquals("order-1", command.captured.orderId)
    }

    @Test
    fun `a successful decline drops that offer and leaves the others`() = runTest {
        val repo = newRepo()
        coEvery { orderApi.orderMyPendingOffers() } returns
            Response.success(listOf(offer("a"), offer("b"), offer("c")))
        repo.refreshPendingOffers()
        coEvery { orderApi.orderDeclinePreferredOffer(any()) } returns
            Response.success(DeclinePreferredOfferResponse(orderId = "b"))

        val result = repo.declinePreferredOffer("b")

        assertTrue(result is ApiResult.Success)
        assertEquals(listOf("a", "c"), repo.pendingOffers.value.map { it.id })
    }

    @Test
    fun `a refused decline leaves the offer where it was`() = runTest {
        val repo = newRepo()
        coEvery { orderApi.orderMyPendingOffers() } returns Response.success(listOf(offer("a"), offer("b")))
        repo.refreshPendingOffers()
        coEvery { orderApi.orderDeclinePreferredOffer(any()) } returns Response.error(
            400,
            """{"errors":{"OrderId":["order.not_found"]}}""".toResponseBody("application/json".toMediaType()),
        )

        val result = repo.declinePreferredOffer("b")

        assertTrue(result is ApiResult.Error)
        assertEquals(listOf("a", "b"), repo.pendingOffers.value.map { it.id })
    }

    @Test
    fun `sign-out drops the offers and re-stales the surface`() = runTest {
        val repo = newRepo()
        coEvery { orderApi.orderMyPendingOffers() } returns Response.success(listOf(offer("a")))
        repo.refreshPendingOffers()
        assertFalse(repo.arePendingOffersStale())

        (repo as SessionScopedCache).clear()

        assertEquals(emptyList<PendingOffer>(), repo.pendingOffers.value)
        assertTrue(repo.arePendingOffersStale())
    }
}
