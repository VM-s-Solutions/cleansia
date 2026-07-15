package cz.cleansia.partner.data.profile

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
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
 * Pins the [SessionScopedCache] contract of [ProfileRepositoryImpl]: the
 * registration-status watermark resets on sign-out so the next account's
 * registration gate re-fetches instead of trusting the prior user's cache.
 */
class ProfileRepositoryTest {

    private lateinit var employeeApi: EmployeeApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        employeeApi = mockk()
    }

    private fun newRepo() = ProfileRepositoryImpl(employeeApi, json)

    @Test
    fun clear_resetsRegistrationStatusWatermark() = runTest {
        coEvery { employeeApi.employeeCheckCurrentEmployee(any()) } returns
            Response.success(mockk<RegistrationCompletionStatus>(relaxed = true))
        val repo = newRepo()
        repo.getRegistrationStatus()
        assertFalse(
            "watermark should be fresh after a successful fetch",
            repo.getRegistrationStatusStaleness().isStale(),
        )

        (repo as SessionScopedCache).clear()

        assertTrue(
            "watermark must be stale again after clear()",
            repo.getRegistrationStatusStaleness().isStale(),
        )
    }
}
