package cz.cleansia.partner.data.dashboard

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.async
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response
import java.util.concurrent.atomic.AtomicInteger

/**
 * Pins the [SessionScopedCache] contract of [DashboardRepositoryImpl]: on
 * sign-out the cached snapshot (earnings stats, upcoming orders, jobs preview)
 * must be wiped AND the 60s staleness watermark reset, otherwise the next
 * account on a shared device inherits the prior user's dashboard and a
 * non-forced refresh no-ops inside the stale window.
 */
class DashboardRepositoryTest {

    private lateinit var dashboardApi: DashboardApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        dashboardApi = mockk()
    }

    private fun newRepo() = DashboardRepositoryImpl(dashboardApi, json)

    private suspend fun <T> awaitRealCompletion(block: suspend () -> T): T =
        withContext(Dispatchers.Default) { withTimeout(5_000) { block() } }

    @Test
    fun clear_wipesTheCachedSnapshot() = runTest {
        coEvery { dashboardApi.dashboardGetStats(any()) } returns Response.success(dashboardStatsDto())
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(any()) } returns
            Response.success(availableJobsPreviewResponse())
        val repo = newRepo()
        repo.refresh(employeeId = null, force = false)
        assertTrue(repo.snapshot.value.loaded)

        (repo as SessionScopedCache).clear()

        assertNull(repo.snapshot.value.stats)
        assertFalse(repo.snapshot.value.loaded)
        assertEquals(DashboardSnapshot(), repo.snapshot.value)
    }

    @Test
    fun clear_resetsStalenessSoNextNonForcedRefreshHitsTheNetwork() = runTest {
        var statsCalls = 0
        coEvery { dashboardApi.dashboardGetStats(any()) } answers {
            statsCalls++
            Response.success(dashboardStatsDto())
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(any()) } returns
            Response.success(availableJobsPreviewResponse())
        val repo = newRepo()

        repo.refresh(employeeId = null, force = false)
        // Second non-forced refresh inside the stale window would no-op...
        repo.refresh(employeeId = null, force = false)
        assertEquals(1, statsCalls)

        (repo as SessionScopedCache).clear()

        // ...but after clear() the watermark is gone, so it fetches again.
        repo.refresh(employeeId = null, force = false)
        assertEquals(2, statsCalls)
    }

    @Test
    fun refresh_publishesStatsThenPreviewWhileUpcomingIsHeld() = runTest {
        val statsEntered = CompletableDeferred<Unit>()
        val statsRelease = CompletableDeferred<Unit>()
        val upcomingEntered = CompletableDeferred<Unit>()
        val upcomingRelease = CompletableDeferred<Unit>()
        val upcomingCompleted = CompletableDeferred<Unit>()
        val previewEntered = CompletableDeferred<Unit>()
        val previewRelease = CompletableDeferred<Unit>()
        val previewCompleted = CompletableDeferred<Unit>()
        val upcoming = listOf(OrderListItem(id = "upcoming-1"))
        coEvery { dashboardApi.dashboardGetStats("emp-1") } coAnswers {
            statsEntered.complete(Unit)
            statsRelease.await()
            Response.success(dashboardStatsDto())
        }
        coEvery {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        } coAnswers {
            upcomingEntered.complete(Unit)
            try {
                upcomingRelease.await()
                Response.success(PagedDataOfOrderListItem(data = upcoming))
            } finally {
                upcomingCompleted.complete(Unit)
            }
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } coAnswers {
            previewEntered.complete(Unit)
            try {
                previewRelease.await()
                Response.success(availableJobsPreviewResponse())
            } finally {
                previewCompleted.complete(Unit)
            }
        }
        val repo = newRepo()
        val refresh = async { repo.refresh("emp-1", force = false) }
        try {
            awaitRealCompletion { statsEntered.await() }
            assertFalse(upcomingEntered.isCompleted)
            assertFalse(previewEntered.isCompleted)
            statsRelease.complete(Unit)
            awaitRealCompletion { upcomingEntered.await(); previewEntered.await() }
            assertEquals(dashboardStats(), repo.snapshot.value.stats)
            assertTrue(repo.snapshot.value.refreshing)
            assertFalse(repo.snapshot.value.loaded)
            previewRelease.complete(Unit)
            awaitRealCompletion {
                previewCompleted.await()
                repo.snapshot.first { it.availableJobsPreview != null }
            }
            assertEquals(availableJobsPreviewResponse().toDomain(), repo.snapshot.value.availableJobsPreview)
            assertTrue(repo.snapshot.value.upcoming.isEmpty())
            assertFalse(refresh.isCompleted)
            upcomingRelease.complete(Unit)
            assertNull(refresh.await())
            awaitRealCompletion { upcomingCompleted.await() }
            assertEquals(upcoming, repo.snapshot.value.upcoming)
            assertEquals(availableJobsPreviewResponse().toDomain(), repo.snapshot.value.availableJobsPreview)
            assertTrue(repo.snapshot.value.loaded)
            assertFalse(repo.snapshot.value.refreshing)
        } finally {
            statsRelease.complete(Unit)
            upcomingRelease.complete(Unit)
            previewRelease.complete(Unit)
            refresh.cancelAndJoin()
        }
    }

    @Test
    fun refresh_retainsUpcomingWhenItFinishesBeforePreview() = runTest {
        val upcomingEntered = CompletableDeferred<Unit>()
        val upcomingRelease = CompletableDeferred<Unit>()
        val upcomingCompleted = CompletableDeferred<Unit>()
        val previewEntered = CompletableDeferred<Unit>()
        val previewRelease = CompletableDeferred<Unit>()
        val previewCompleted = CompletableDeferred<Unit>()
        val upcoming = listOf(OrderListItem(id = "upcoming-1"))
        coEvery { dashboardApi.dashboardGetStats("emp-1") } returns Response.success(dashboardStatsDto())
        coEvery {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        } coAnswers {
            upcomingEntered.complete(Unit)
            try {
                upcomingRelease.await()
                Response.success(PagedDataOfOrderListItem(data = upcoming))
            } finally {
                upcomingCompleted.complete(Unit)
            }
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } coAnswers {
            previewEntered.complete(Unit)
            try {
                previewRelease.await()
                Response.success(availableJobsPreviewResponse())
            } finally {
                previewCompleted.complete(Unit)
            }
        }
        val repo = newRepo()
        val refresh = async { repo.refresh("emp-1", force = false) }
        try {
            awaitRealCompletion { upcomingEntered.await(); previewEntered.await() }
            upcomingRelease.complete(Unit)
            awaitRealCompletion { upcomingCompleted.await(); repo.snapshot.first { it.upcoming == upcoming } }
            assertNull(repo.snapshot.value.availableJobsPreview)
            assertFalse(refresh.isCompleted)
            previewRelease.complete(Unit)
            assertNull(refresh.await())
            awaitRealCompletion { previewCompleted.await() }
            assertEquals(upcoming, repo.snapshot.value.upcoming)
            assertEquals(availableJobsPreviewResponse().toDomain(), repo.snapshot.value.availableJobsPreview)
        } finally {
            upcomingRelease.complete(Unit)
            previewRelease.complete(Unit)
            refresh.cancelAndJoin()
        }
    }

    @Test
    fun refresh_optionalFailuresKeepLastGoodFieldsAndStatsErrorRemainsCritical() = runTest {
        val upcoming = listOf(OrderListItem(id = "upcoming-1"))
        coEvery { dashboardApi.dashboardGetStats("emp-1") } returns Response.success(dashboardStatsDto())
        coEvery {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        } returns Response.success(PagedDataOfOrderListItem(data = upcoming))
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } returns Response.success(availableJobsPreviewResponse())
        val repo = newRepo()
        assertNull(repo.refresh("emp-1", force = false))
        val good = repo.snapshot.value

        coEvery {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        } throws java.io.IOException("optional upcoming")
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } throws java.io.IOException("optional preview")
        assertNull(repo.refresh("emp-1", force = true))
        assertEquals(good, repo.snapshot.value)

        coEvery { dashboardApi.dashboardGetStats("emp-1") } throws java.io.IOException("critical stats")
        assertTrue(repo.refresh("emp-1", force = true) is cz.cleansia.core.network.ApiError.Network)
        assertEquals(good, repo.snapshot.value)
    }

    @Test
    fun refresh_nullEmployeeClearsUpcomingAndForceBypassesFreshCache() = runTest {
        val statsCalls = AtomicInteger()
        val upcoming = listOf(OrderListItem(id = "upcoming-1"))
        coEvery { dashboardApi.dashboardGetStats(any()) } answers {
            statsCalls.incrementAndGet()
            Response.success(dashboardStatsDto())
        }
        coEvery {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        } returns Response.success(PagedDataOfOrderListItem(data = upcoming))
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } returns Response.success(availableJobsPreviewResponse())
        val repo = newRepo()
        assertNull(repo.refresh("emp-1", force = false))
        assertNull(repo.refresh("emp-1", force = false))
        assertEquals(1, statsCalls.get())
        assertEquals(upcoming, repo.snapshot.value.upcoming)
        assertNull(repo.refresh(null, force = true))
        assertTrue(repo.snapshot.value.upcoming.isEmpty())
        assertEquals(2, statsCalls.get())
        coVerify(exactly = 1) {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        }
    }

    @Test
    fun refresh_cancellationClearsRefreshingWithoutPublishingHeldOptionalResults() = runTest {
        val upcomingEntered = CompletableDeferred<Unit>()
        val upcomingRelease = CompletableDeferred<Unit>()
        val upcomingCompleted = CompletableDeferred<Unit>()
        val previewEntered = CompletableDeferred<Unit>()
        val previewRelease = CompletableDeferred<Unit>()
        val previewCompleted = CompletableDeferred<Unit>()
        coEvery { dashboardApi.dashboardGetStats("emp-1") } returns Response.success(dashboardStatsDto())
        coEvery {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = "emp-1", filterIsActive = true, sort = any(), offset = 0, limit = 10,
            )
        } coAnswers {
            upcomingEntered.complete(Unit)
            try {
                upcomingRelease.await()
                Response.success(PagedDataOfOrderListItem(data = emptyList()))
            } finally {
                upcomingCompleted.complete(Unit)
            }
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } coAnswers {
            previewEntered.complete(Unit)
            try {
                previewRelease.await()
                Response.success(availableJobsPreviewResponse())
            } finally {
                previewCompleted.complete(Unit)
            }
        }
        val repo = newRepo()
        val refresh = async { repo.refresh("emp-1", force = true) }
        try {
            awaitRealCompletion { upcomingEntered.await(); previewEntered.await() }
            refresh.cancelAndJoin()
            awaitRealCompletion { upcomingCompleted.await(); previewCompleted.await() }
            assertTrue(refresh.isCancelled)
            assertFalse(repo.snapshot.value.refreshing)
            assertFalse(repo.snapshot.value.loaded)
            assertEquals(dashboardStats(), repo.snapshot.value.stats)
            assertTrue(repo.snapshot.value.upcoming.isEmpty())
            assertNull(repo.snapshot.value.availableJobsPreview)
        } finally {
            upcomingRelease.complete(Unit)
            previewRelease.complete(Unit)
            refresh.cancelAndJoin()
        }
    }

    @Test
    fun clear_blocksLatePublicationAndOldQueuedRefreshThenNewSessionLoads() = runTest {
        val oldEntered = CompletableDeferred<Unit>()
        val oldRelease = CompletableDeferred<Unit>()
        val oldCompleted = CompletableDeferred<Unit>()
        val statsCalls = AtomicInteger()
        coEvery { dashboardApi.dashboardGetStats(any()) } coAnswers {
            if (statsCalls.incrementAndGet() == 1) {
                oldEntered.complete(Unit)
                try {
                    withContext(NonCancellable) { oldRelease.await() }
                    Response.success(dashboardStatsDto().copy(todayEarnings = 1.0))
                } finally {
                    oldCompleted.complete(Unit)
                }
            } else {
                Response.success(dashboardStatsDto())
            }
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(5) } returns Response.success(availableJobsPreviewResponse())
        val repo = newRepo()
        val old = async { repo.refresh(null, force = true) }
        try {
            awaitRealCompletion { oldEntered.await() }
            val queued = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh(null, force = true) }
            try {
                repo.clear()
                assertEquals(DashboardSnapshot(), repo.snapshot.value)
                oldRelease.complete(Unit)
                assertNull(old.await())
                assertNull(queued.await())
                awaitRealCompletion { oldCompleted.await() }
                assertEquals(DashboardSnapshot(), repo.snapshot.value)
                assertEquals(1, statsCalls.get())
                assertNull(repo.refresh(null, force = false))
                assertEquals(dashboardStats(), repo.snapshot.value.stats)
                assertTrue(repo.snapshot.value.loaded)
                assertFalse(repo.snapshot.value.refreshing)
                assertEquals(2, statsCalls.get())
            } finally {
                oldRelease.complete(Unit)
                queued.cancelAndJoin()
            }
        } finally {
            oldRelease.complete(Unit)
            old.cancelAndJoin()
        }
    }
}
