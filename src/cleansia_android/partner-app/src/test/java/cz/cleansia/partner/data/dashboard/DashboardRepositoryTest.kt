package cz.cleansia.partner.data.dashboard

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.model.DashboardStatsDto
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

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

    @Test
    fun clear_wipesTheCachedSnapshot() = runTest {
        coEvery { dashboardApi.dashboardGetStats(any()) } returns Response.success(mockk<DashboardStatsDto>())
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(any()) } returns Response.success(mockk())
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
            Response.success(mockk<DashboardStatsDto>())
        }
        coEvery { dashboardApi.dashboardGetAvailableJobsPreview(any()) } returns Response.success(mockk())
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
}
