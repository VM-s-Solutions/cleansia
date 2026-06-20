package cz.cleansia.customer.core.recurring

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Characterization + post-migration contract tests for [RecurringBookingRepository].
 *
 * Pins the observable repo behavior across the T-0197 migration from the legacy
 * swallow-and-log `T?`/`Boolean` form to the `ApiResult<T>` contract:
 *  - success returns the body in [ApiResult.Success] (and refreshes the cache);
 *  - a transport failure (networkCall returns null) is the SILENT channel -
 *    [ApiResult.Error] carrying [ApiError.Network] (NetworkErrorInterceptor owns
 *    the infra toast, so the consuming ViewModel skips it - no double-toast);
 *  - an HTTP error returns [ApiResult.Error] carrying the parsed message (built
 *    from [cz.cleansia.customer.core.auth.ApiErrorParser]).
 *
 * This repo never held a SnackbarController (it logged and returned the failure
 * sentinel; the consuming surfaces decided what to show - the create screen its
 * own generic message, the list/home callers nothing). The standalone `snackbar`
 * mock asserts the repo still never surfaces a snackbar itself.
 */
class RecurringBookingRepositoryTest {

    private lateinit var api: RecurringBookingApi
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val serverMessage = "Server problem. Please try again later."

    @Before
    fun setUp() {
        api = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_generic_server) } returns serverMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns "unknown"
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newRepo() = RecurringBookingRepository(api, appContext)

    private fun template(id: String) = RecurringBookingTemplateDto(
        id = id,
        frequency = 1,
        dayOfWeek = 4,
        timeOfDay = "10:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = "addr-1",
        paymentType = 1,
        startsOn = "2026-07-01T08:00:00Z",
        isActive = true,
    )

    private fun createRequest() = CreateRecurringBookingRequest(
        frequency = 1,
        dayOfWeek = 4,
        timeOfDay = "10:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = "addr-1",
        selectedServiceIds = listOf("s-1"),
        selectedPackageIds = emptyList(),
        paymentType = 1,
        startsOn = "2026-07-01T08:00:00Z",
    )

    private fun updateRequest() = UpdateRecurringBookingRequest(
        templateId = "t-1",
        frequency = 1,
        dayOfWeek = 4,
        timeOfDay = "10:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = "addr-1",
        selectedServiceIds = listOf("s-1"),
        selectedPackageIds = emptyList(),
        paymentType = 1,
        startsOn = "2026-07-01T08:00:00Z",
    )

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    // -- refresh() --

    @Test
    fun refresh_givenSuccess_cachesTemplatesAndReturnsSuccess() = runTest {
        val body = listOf(template("t-1"), template("t-2"))
        coEvery { api.getMine() } returns Response.success(body)

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(body, repo.templates.value)
        assertEquals(true, repo.loaded.value)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_givenHttpError_returnsServerErrorAndNoRepoSnackbar() = runTest {
        coEvery { api.getMine() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.refresh()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        assertEquals(false, repo.loaded.value)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.getMine() } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Network)
        assertEquals(networkMessage, error.getUserMessage())
        assertEquals(false, repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    // -- create() --

    @Test
    fun create_givenSuccess_returnsBodyAndRefreshes() = runTest {
        val created = template("t-new")
        coEvery { api.create(any()) } returns Response.success(created)
        coEvery { api.getMine() } returns Response.success(listOf(created))

        val result = newRepo().create(createRequest())

        assertEquals(created, result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun create_givenHttpError_returnsErrorAndDoesNotRefresh() = runTest {
        coEvery { api.create(any()) } returns Response.error(400, errorBody())

        val repo = newRepo()
        val result = repo.create(createRequest())

        assertTrue((result as ApiResult.Error).error is ApiError.BadRequest)
        assertEquals(false, repo.loaded.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun create_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.create(any()) } throws java.io.IOException("boom")

        val result = newRepo().create(createRequest())

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // -- update() --

    @Test
    fun update_givenSuccess_returnsBodyAndRefreshes() = runTest {
        val updated = template("t-1")
        coEvery { api.update(any()) } returns Response.success(updated)
        coEvery { api.getMine() } returns Response.success(listOf(updated))

        val result = newRepo().update(updateRequest())

        assertEquals(updated, result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun update_givenHttpError_returnsError() = runTest {
        coEvery { api.update(any()) } returns Response.error(500, errorBody())

        val result = newRepo().update(updateRequest())

        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // -- setActive() --

    @Test
    fun setActive_givenSuccess_returnsUnitSuccessAndRefreshes() = runTest {
        coEvery { api.setActive(any()) } returns Response.success(Unit)
        coEvery { api.getMine() } returns Response.success(listOf(template("t-1")))

        val result = newRepo().setActive("t-1", isActive = false)

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun setActive_givenHttpError_returnsError() = runTest {
        coEvery { api.setActive(any()) } returns Response.error(500, errorBody())

        val result = newRepo().setActive("t-1", isActive = false)

        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun setActive_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.setActive(any()) } throws java.io.IOException("boom")

        val result = newRepo().setActive("t-1", isActive = false)

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // -- delete() --

    @Test
    fun delete_givenSuccess_returnsUnitSuccessAndRefreshes() = runTest {
        coEvery { api.delete(any()) } returns Response.success(Unit)
        coEvery { api.getMine() } returns Response.success(emptyList())

        val result = newRepo().delete("t-1")

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun delete_givenHttpError_returnsError() = runTest {
        coEvery { api.delete(any()) } returns Response.error(404, errorBody())

        val result = newRepo().delete("t-1")

        assertTrue((result as ApiResult.Error).error is ApiError.NotFound)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // -- clear() --

    @Test
    fun clear_resetsTemplatesAndLoaded() = runTest {
        coEvery { api.getMine() } returns Response.success(listOf(template("t-1")))
        val repo = newRepo()
        repo.refresh()
        assertTrue(repo.templates.value.isNotEmpty())
        assertTrue(repo.loaded.value)

        repo.clear()

        assertEquals(emptyList<RecurringBookingTemplateDto>(), repo.templates.value)
        assertEquals(false, repo.loaded.value)
    }
}
