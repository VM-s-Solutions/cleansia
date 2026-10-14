package cz.cleansia.partner.data.dashboard

import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import kotlinx.serialization.json.Json
import org.junit.Assert.*
import org.junit.Test
import retrofit2.Response
import java.util.concurrent.atomic.AtomicInteger

class DashboardInvalidationTest {
    @Test fun invalidationRetainsDisplayedPayloadAndRejectsHeldAndQueuedOldPublication() = runTest {
        val api = mockk<DashboardApi>()
        val repo = DashboardRepositoryImpl(api, Json { ignoreUnknownKeys = true })
        val release = CompletableDeferred<Unit>()
        val statsEntered = CompletableDeferred<Unit>()
        val previewEntered = CompletableDeferred<Unit>()
        val upcomingEntered = CompletableDeferred<Unit>()
        val statsCalls = AtomicInteger()
        val previewCalls = AtomicInteger()
        val upcomingCalls = AtomicInteger()
        coEvery { api.dashboardGetStats(any()) } coAnswers {
            val call = statsCalls.incrementAndGet()
            if (call == 2) { statsEntered.complete(Unit); release.await() }
            Response.success(dashboardStatsDto().copy(todayEarnings = call.toDouble()))
        }
        coEvery { api.dashboardGetAvailableJobsPreview(any()) } coAnswers {
            val call = previewCalls.incrementAndGet()
            if (call == 2) { previewEntered.complete(Unit); release.await() }
            Response.success(availableJobsPreviewResponse().copy(totalAvailableCount = call))
        }
        coEvery { api.dashboardGetUpcomingOrders(filterEmployeeId = "employee", filterIsActive = true, filterCleaningDateFrom = any(), sort0Field = any(), sort0Direction = any(), offset = 0, limit = 10) } coAnswers {
            val call = upcomingCalls.incrementAndGet()
            if (call == 2) { upcomingEntered.complete(Unit); release.await() }
            Response.success(PagedDataOfOrderListItem(data = listOf(OrderListItem(id = "upcoming-$call"))))
        }
        repo.refresh("employee", false)
        val displayed = repo.snapshot.value
        val held = async { repo.refresh("employee", true) }
        withContext(Dispatchers.Default) { withTimeout(5_000) { statsEntered.await(); previewEntered.await(); upcomingEntered.await() } }
        val queued = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh("employee", true) }
        try {
            repo.invalidate()
            assertEquals(displayed, repo.snapshot.value)
            release.complete(Unit)
            held.await()
            queued.await()
            assertEquals(displayed, repo.snapshot.value)
            assertEquals(2, statsCalls.get())
            assertEquals(2, previewCalls.get())
            assertEquals(2, upcomingCalls.get())
            repo.refresh("employee", false)
            assertEquals(3, statsCalls.get())
            assertEquals(3.0, repo.snapshot.value.stats!!.todayEarnings, 0.0)
            assertEquals(3, repo.snapshot.value.availableJobsPreview!!.totalAvailableCount)
            assertEquals("upcoming-3", repo.snapshot.value.upcoming!!.single().id)
            assertTrue(repo.snapshot.value.loaded)
            assertFalse(repo.snapshot.value.refreshing)
        } finally { release.complete(Unit); held.await(); queued.await() }
    }
}
