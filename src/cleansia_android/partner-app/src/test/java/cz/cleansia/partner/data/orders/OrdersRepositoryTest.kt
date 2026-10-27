package cz.cleansia.partner.data.orders

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.DropOrderCommand
import cz.cleansia.partner.api.model.DropOrderResponse
import cz.cleansia.partner.api.model.MarkMyPartDoneCommand
import cz.cleansia.partner.api.model.MarkMyPartDoneResponse
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import cz.cleansia.partner.api.model.ReportCashNotPaidCommand
import cz.cleansia.partner.api.model.ReportCashNotPaidResponse
import cz.cleansia.partner.api.model.ReportOrderLockoutCommand
import cz.cleansia.partner.api.model.ReportOrderLockoutResponse
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Pins the [SessionScopedCache] contract of [OrdersRepositoryImpl]: on every wipe trigger
 * every freshness watermark (per-pane list + per-order detail) resets, so the
 * next account's first fetch is never gated out as "fresh" by the prior user's
 * cache — per Staleness.kt's documented reset-from-clear() instruction.
 * -> /mobile-app/patterns#session-wipe
 */
class OrdersRepositoryTest {

    private lateinit var orderApi: OrderApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        orderApi = mockk()
    }

    private fun newRepo() = OrdersRepositoryImpl(orderApi, json, mockk(relaxed = true))

    @Test
    fun clear_resetsPaneWatermarks() = runTest {
        // Named, so a filter the spec adds and the repository never sends lands on its default
        // on both sides instead of shifting a positional matcher off the argument it was written for.
        coEvery {
            orderApi.orderGetPaged(
                filterOrderStatuses = any(),
                filterIsUnassigned = any(),
                filterEmployeeId = any(),
                filterCleaningDateFrom = any(),
                filterCleaningDateTo = any(),
                sort0Field = any(),
                sort0Direction = any(),
                offset = any(),
                limit = any(),
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

    @Test
    fun reportLockout_postsTheOrderAndTheCallsAndExpiresTheOrder() = runTest {
        coEvery { orderApi.orderGetById("order-1") } returns Response.success(mockk<OrderItem>(relaxed = true))
        coEvery {
            orderApi.orderReportLockout(ReportOrderLockoutCommand(orderId = "order-1", callAttempts = "Called twice"))
        } returns Response.success(ReportOrderLockoutResponse(orderId = "order-1", reportedAt = "2026-09-29T10:20:00Z"))
        val repo = newRepo()
        repo.getById("order-1")

        val result = repo.reportLockout("order-1", "Called twice")

        assertTrue("the report must succeed; got $result", result is ApiResult.Success)
        assertTrue("the reported order must be refetched, not served warm", repo.isOrderStale("order-1"))
    }

    @Test
    fun reportCashNotPaid_postsTheOrderAndExpiresTheOrder() = runTest {
        coEvery { orderApi.orderGetById("order-1") } returns Response.success(mockk<OrderItem>(relaxed = true))
        coEvery {
            orderApi.orderReportCashNotPaid(ReportCashNotPaidCommand(orderId = "order-1"))
        } returns Response.success(ReportCashNotPaidResponse(orderId = "order-1", amountOwed = 1200.0))
        val repo = newRepo()
        repo.getById("order-1")

        val result = repo.reportCashNotPaid("order-1")

        assertTrue("the report must succeed; got $result", result is ApiResult.Success)
        assertTrue("the completed order must be refetched, not served warm", repo.isOrderStale("order-1"))
    }

    @Test
    fun markMyPartDone_postsTheOrderAndExpiresTheOrder() = runTest {
        coEvery { orderApi.orderGetById("order-1") } returns Response.success(mockk<OrderItem>(relaxed = true))
        coEvery {
            orderApi.orderMarkMyPartDone(MarkMyPartDoneCommand(orderId = "order-1"))
        } returns Response.success(
            MarkMyPartDoneResponse(orderId = "order-1", completionConfirmedAt = "2026-10-27T10:15:00Z", crewDone = false),
        )
        val repo = newRepo()
        repo.getById("order-1")

        val result = repo.markMyPartDone("order-1")

        assertTrue("the mark must succeed; got $result", result is ApiResult.Success)
        assertTrue("the marked job must be refetched, not served warm", repo.isOrderStale("order-1"))
    }

    private fun stubFreshReads() {
        coEvery { orderApi.orderGetById("order-1") } returns Response.success(mockk<OrderItem>(relaxed = true))
        coEvery {
            orderApi.orderGetPaged(
                filterOrderStatuses = any(),
                filterIsUnassigned = any(),
                filterEmployeeId = any(),
                filterCleaningDateFrom = any(),
                filterCleaningDateTo = any(),
                sort0Field = any(),
                sort0Direction = any(),
                offset = any(),
                limit = any(),
            )
        } returns Response.success(mockk<PagedDataOfOrderListItem>(relaxed = true))
    }

    private suspend fun readEverythingFresh(repo: OrdersRepository) {
        repo.getById("order-1")
        OrdersPane.entries.forEach { repo.getPaged(pane = it) }
    }

    /** The job leaves the cleaner's Active tab and their seat is back on Available; History is untouched. */
    @Test
    fun dropOrder_returnsTheCrewLeftAndExpiresTheOrderActiveAndAvailable() = runTest {
        stubFreshReads()
        coEvery { orderApi.orderDropOrder(DropOrderCommand(orderId = "order-1")) } returns
            Response.success(DropOrderResponse(orderId = "order-1", crewRemaining = 0))
        val repo = newRepo()
        readEverythingFresh(repo)

        val result = repo.dropOrder("order-1")

        assertEquals(ApiResult.Success(0), result)
        assertTrue("the order must be re-read, not served warm", repo.isOrderStale("order-1"))
        assertTrue("Active must refetch: the job has left it", repo.isPaneStale(OrdersPane.Active))
        assertTrue("Available must refetch: the seat is open again", repo.isPaneStale(OrdersPane.Available))
        assertFalse("History did not change", repo.isPaneStale(OrdersPane.History))
    }

    @Test
    fun dropOrder_refused_expiresNothing() = runTest {
        stubFreshReads()
        coEvery { orderApi.orderDropOrder(any()) } returns Response.error(
            400,
            """{"errors":{"Command":["order.not_confirmed"]}}""".toResponseBody("application/json".toMediaType()),
        )
        val repo = newRepo()
        readEverythingFresh(repo)

        val result = repo.dropOrder("order-1")

        assertTrue("a refusal is an error; got $result", result is ApiResult.Error)
        assertFalse(repo.isOrderStale("order-1"))
        OrdersPane.entries.forEach { assertFalse("$it must stay fresh", repo.isPaneStale(it)) }
    }

    /** Rule 1 of ADR-0048: a missing count is a broken wire field, never a crew of zero. */
    @Test
    fun dropOrder_withNoCrewCount_isRefusedRatherThanReadAsNoCrew() = runTest {
        coEvery { orderApi.orderDropOrder(any()) } returns
            Response.success(DropOrderResponse(orderId = "order-1", crewRemaining = null))

        val result = newRepo().dropOrder("order-1")

        assertTrue("a missing crewRemaining must be refused; got $result", (result as? ApiResult.Error)?.error is ApiError.Server)
    }

    @Test
    fun dropOrder_answeredAfterASignOut_expiresNothingOfTheNextSession() = runTest {
        stubFreshReads()
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        coEvery { orderApi.orderDropOrder(any()) } coAnswers {
            entered.complete(Unit)
            release.await()
            Response.success(DropOrderResponse(orderId = "order-1", crewRemaining = 0))
        }
        val repo = newRepo()
        val old = async { repo.dropOrder("order-1") }
        try {
            withContext(Dispatchers.Default) { withTimeout(5_000) { entered.await() } }
            (repo as SessionScopedCache).clear()
            readEverythingFresh(repo)
            release.complete(Unit)

            assertTrue(old.await() is ApiResult.Success)
            assertFalse(repo.isOrderStale("order-1"))
            OrdersPane.entries.forEach { assertFalse("$it of the next session must stay fresh", repo.isPaneStale(it)) }
        } finally {
            release.complete(Unit)
            old.await()
        }
    }
}
