package cz.cleansia.customer.core.catalog

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.async
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Behavior contract for CatalogRepository.refresh().
 *
 * refresh() warms the services/packages/extras catalog cache. Success populates
 * the three flows, flips `loaded` true and returns Success. A failed
 * services/packages call returns Error carrying the same single parsed message
 * the repo used to push to the snackbar; the consuming ViewModel now surfaces
 * that message (the repo no longer touches the snackbar). Network/connectivity
 * failures stay silent (NetworkErrorInterceptor owns the infra toast) — the
 * Error carries ApiError.Network which the VM keeps a no-op. Extras are
 * best-effort and never fail the refresh.
 *
 * Characterization -> migrate -> green: the same success/failure outcomes and the
 * same single message are preserved across the String?/snackbar-in-repo ->
 * ApiResult<Unit>/snackbar-in-VM migration; only the carrier and the surfacing
 * layer changed.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class CatalogRepositoryTest {

    private lateinit var api: CatalogApi
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val serverMessage = "Server problem. Please try again later."
    private val unknownMessage = "Something went wrong. Please try again."

    @Before
    fun setUp() {
        api = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_generic_server) } returns serverMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns unknownMessage
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newRepo() = CatalogRepository(api, appContext)

    private fun service(id: String) = ServiceListItem(
        id = id,
        name = "Service $id",
        basePrice = 10.0,
        perRoomPrice = 1.0,
        category = CategoryDto(id = "c-1", slug = "general", name = "General"),
    )

    private fun pkg(id: String) = PackageListItem(id = id, name = "Package $id", price = 20.0)

    private fun extra(id: String) = ExtraListItem(id = id, slug = "oven", name = "Inside Oven", price = 5.0)

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    // ── success ──

    @Test
    fun refresh_givenAllSuccessful_populatesFlowsAndReturnsSuccess() = runTest {
        coEvery { api.getServices() } returns Response.success(listOf(service("s-1")))
        coEvery { api.getPackages() } returns Response.success(listOf(pkg("p-1")))
        coEvery { api.getExtras() } returns Response.success(listOf(extra("e-1")))

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(listOf(service("s-1")), repo.services.value)
        assertEquals(listOf(pkg("p-1")), repo.packages.value)
        assertEquals(listOf(extra("e-1")), repo.extras.value)
        assertTrue(repo.loaded.value)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenExtrasFail_stillSucceedsWithEmptyExtras() = runTest {
        coEvery { api.getServices() } returns Response.success(listOf(service("s-1")))
        coEvery { api.getPackages() } returns Response.success(listOf(pkg("p-1")))
        coEvery { api.getExtras() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue(result is ApiResult.Success)
        assertEquals(listOf(service("s-1")), repo.services.value)
        assertEquals(emptyList<ExtraListItem>(), repo.extras.value)
        assertTrue(repo.loaded.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenAlreadyLoading_shortCircuitsToSuccess() = runTest {
        // Drive the repo into loading=true via a gated services call, then a
        // second refresh must bail out (no-op) to Success without a second hit.
        val gate = CompletableDeferred<Response<List<ServiceListItem>>>()
        coEvery { api.getServices() } coAnswers { gate.await() }
        coEvery { api.getPackages() } returns Response.success(listOf(pkg("p-1")))
        coEvery { api.getExtras() } returns Response.success(emptyList())

        val repo = newRepo()
        val first = async { repo.refresh() }
        runCurrent()

        val second = repo.refresh()
        assertTrue("second concurrent refresh should no-op to Success", second is ApiResult.Success)

        gate.complete(Response.success(listOf(service("s-1"))))
        first.await()
    }

    // ── services / packages HTTP failure → Error with the parsed message ──

    @Test
    fun refresh_givenServicesHttp500_returnsServerErrorMessageAndNoRepoSnackbar() = runTest {
        coEvery { api.getServices() } returns Response.error(500, errorBody())
        coEvery { api.getPackages() } returns Response.success(listOf(pkg("p-1")))
        coEvery { api.getExtras() } returns Response.success(emptyList())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        // Empty body + 500 → the (errorBody, code) parser's server fallback string.
        assertEquals(serverMessage, (result as ApiResult.Error).error.message)
        assertTrue(result.error is ApiError.Server)
        assertEquals(false, repo.loaded.value)
        // The snackbar moved to the VM — the repo no longer surfaces it.
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_givenPackagesHttp400_returnsParsedMessageAndNoRepoSnackbar() = runTest {
        coEvery { api.getServices() } returns Response.success(listOf(service("s-1")))
        coEvery { api.getPackages() } returns Response.error(400, errorBody())
        coEvery { api.getExtras() } returns Response.success(emptyList())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        // Empty body + 400 → falls through to the generic-unknown fallback string.
        assertEquals(unknownMessage, (result as ApiResult.Error).error.message)
        assertTrue(result.error is ApiError.BadRequest)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── network/connectivity failure → silent Error (no snackbar anywhere) ──

    @Test
    fun refresh_whenServicesThrows_returnsNetworkErrorSilently() = runTest {
        coEvery { api.getServices() } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue(
            "network failure must carry ApiError.Network so the VM keeps it silent",
            (result as ApiResult.Error).error is ApiError.Network,
        )
        assertEquals(networkMessage, result.error.message)
        assertEquals(false, repo.loaded.value)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }
}
