package cz.cleansia.partner.data.orders

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.*
import cz.cleansia.partner.data.dashboard.DashboardRepositoryImpl
import cz.cleansia.partner.data.dashboard.availableJobsPreviewResponse
import cz.cleansia.partner.data.dashboard.dashboardStatsDto
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import kotlinx.serialization.json.Json
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.Parameterized
import retrofit2.Response
import java.io.IOException
import java.util.concurrent.atomic.AtomicInteger

@RunWith(Parameterized::class)
class OrdersDashboardInvalidationTest(private val mutation: String) {
    private val json = Json { ignoreUnknownKeys = true }
    private val api = mockk<OrderApi>()
    private val dashboardApi = mockk<DashboardApi>()
    private val dashboard = DashboardRepositoryImpl(dashboardApi, json)
    private val statsCalls = AtomicInteger()

    private fun orders() = OrdersRepositoryImpl(api, json, dashboard)

    private fun stubDashboard() {
        coEvery { dashboardApi.dashboardGetStats(any()) } answers {
            statsCalls.incrementAndGet()
            Response.success(dashboardStatsDto())
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(any()) } returns Response.success(availableJobsPreviewResponse())
    }

    private fun stubMutation(block: suspend () -> Unit) {
        coEvery { api.orderTakeOrder(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderNotifyOnTheWay(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderStartOrder(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderCompleteOrder(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderReportCashNotPaid(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderMarkCashCollected(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderMarkMyPartDone(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderDeclinePreferredOffer(any()) } coAnswers { block(); Response.success(mockk(relaxed = true)) }
        coEvery { api.orderDropOrder(any()) } coAnswers {
            block()
            Response.success(DropOrderResponse(orderId = "same-id", crewRemaining = 0))
        }
    }

    private suspend fun mutate(repo: OrdersRepository): ApiResult<Unit> = when (mutation) {
        "takeOrder" -> repo.takeOrder("same-id", "text-id")
        "notifyOnTheWay" -> repo.notifyOnTheWay("same-id")
        "startOrder" -> repo.startOrder("same-id")
        "completeOrder" -> repo.completeOrder("same-id", null, null)
        "reportCashNotPaid" -> repo.reportCashNotPaid("same-id")
        "markCashCollected" -> repo.markCashCollected("same-id")
        "markMyPartDone" -> repo.markMyPartDone("same-id")
        "dropOrder" -> repo.dropOrder("same-id").map { }
        else -> repo.declinePreferredOffer("same-id")
    }

    private suspend fun awaitEntered(entered: CompletableDeferred<Unit>) =
        withContext(Dispatchers.Default) { withTimeout(5_000) { entered.await() } }

    @Test fun successfulMutationRefreshesWarmDashboardWithoutForcing() = runTest {
        stubDashboard()
        stubMutation { }
        val repo = orders()
        dashboard.refresh(null, false)
        val displayed = dashboard.snapshot.value
        assertTrue(mutate(repo) is ApiResult.Success)
        assertEquals(displayed, dashboard.snapshot.value)
        dashboard.refresh(null, false)
        assertEquals(2, statsCalls.get())
    }

    @Test fun failedMutationKeepsWarmDashboard() = runTest {
        stubDashboard()
        stubMutation { throw IOException("refused") }
        val repo = orders()
        dashboard.refresh(null, false)
        val displayed = dashboard.snapshot.value
        assertTrue(mutate(repo) is ApiResult.Error)
        assertEquals(displayed, dashboard.snapshot.value)
        dashboard.refresh(null, false)
        assertEquals(1, statsCalls.get())
    }

    @Test fun lateSuccessInSameSessionStillInvalidatesAfterAnotherMutation() = runTest {
        stubDashboard()
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        val mutationCalls = AtomicInteger()
        stubMutation {
            if (mutationCalls.incrementAndGet() == 1) { entered.complete(Unit); release.await() }
        }
        val repo = orders()
        dashboard.refresh(null, false)
        val old = async { mutate(repo) }
        try {
            awaitEntered(entered)
            assertTrue(mutate(repo) is ApiResult.Success)
            dashboard.refresh(null, false)
            assertEquals(2, statsCalls.get())
            release.complete(Unit)
            assertTrue(old.await() is ApiResult.Success)
            dashboard.refresh(null, false)
            assertEquals(3, statsCalls.get())
        } finally { release.complete(Unit); old.await() }
    }

    @Test fun oldSessionSuccessCannotInvalidateNextSessionsOrderPaneOrDashboard() = runTest {
        stubDashboard()
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        stubMutation { entered.complete(Unit); release.await() }
        coEvery { api.orderGetById("same-id") } returns Response.success(OrderItem(id = "same-id"))
        coEvery { api.orderGetPaged(offset = 0, limit = 50, sort0Field = any(), sort0Direction = any()) } returns
            Response.success(PagedDataOfOrderListItem(data = emptyList()))
        val repo = orders()
        val old = async { mutate(repo) }
        try {
            awaitEntered(entered)
            repo.clear()
            dashboard.clear()
            repo.getById("same-id")
            repo.getPaged(pane = OrdersPane.Available)
            dashboard.refresh(null, false)
            val next = dashboard.snapshot.value
            release.complete(Unit)
            assertTrue(old.await() is ApiResult.Success)
            assertFalse(repo.isOrderStale("same-id"))
            assertFalse(repo.isPaneStale(OrdersPane.Available))
            assertEquals(next, dashboard.snapshot.value)
            dashboard.refresh(null, false)
            assertEquals(1, statsCalls.get())
        } finally { release.complete(Unit); old.await() }
    }

    companion object {
        @JvmStatic @Parameterized.Parameters(name = "{0}") fun mutations() = listOf(
            "takeOrder", "notifyOnTheWay", "startOrder", "completeOrder", "reportCashNotPaid",
            "markCashCollected", "markMyPartDone", "declinePreferredOffer", "dropOrder",
        ).map { arrayOf(it) }
    }
}
