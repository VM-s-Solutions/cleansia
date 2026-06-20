package cz.cleansia.customer.core.disputes

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
 * Post-migration contract tests for [DisputeRepository].
 *
 * Pins the observable repo behavior across the T-0197 migration from the legacy
 * nullable/sentinel-with-snackbar-in-repo form to the `ApiResult<T>` contract:
 *  - success returns the body in [ApiResult.Success] (and, for the list, caches
 *    the page);
 *  - a transport failure (networkCall returns null) is the SILENT channel —
 *    [ApiResult.Error] carrying [ApiError.Network] (NetworkErrorInterceptor owns
 *    the infra toast, so the consuming ViewModel skips it — no double-toast);
 *  - an HTTP error returns [ApiResult.Error] carrying the SAME single message
 *    the legacy repo pushed to the snackbar (built from
 *    [cz.cleansia.customer.core.auth.ApiErrorParser]) — now surfaced by the VM.
 *
 * The repo no longer holds a SnackbarController; the snackbar moved to the VMs
 * (CreateDisputeViewModel / DisputeDetailViewModel / DisputesListViewModel). The
 * standalone `snackbar` mock asserts the repo never surfaces a snackbar itself.
 */
class DisputeRepositoryTest {

    private lateinit var api: DisputeApi
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val serverMessage = "server"

    @Before
    fun setUp() {
        api = mockk()
        // Standalone mock the repo never receives — asserts the repo never
        // surfaces a snackbar (the snackbar moved to the consuming ViewModel).
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

    private fun newRepo() = DisputeRepository(api, appContext)

    private fun errBody() = "{}".toResponseBody("application/json".toMediaType())

    // ── refresh() ──

    @Test
    fun refresh_givenSuccess_cachesPageAndReturnsSuccess() = runTest {
        val page = DisputeListResponseDto(total = 1, data = listOf(DisputeListItemDto(id = "d-1")))
        coEvery { api.getPaged(offset = 0, limit = 20) } returns Response.success(page)

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(listOf(DisputeListItemDto(id = "d-1")), repo.disputes.value)
        assertEquals(1, repo.totalRecords.value)
        assertTrue(repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { api.getPaged(offset = 0, limit = 20) } returns Response.error(500, errBody())

        val result = newRepo().refresh()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.getPaged(offset = 0, limit = 20) } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Network)
        assertEquals(networkMessage, error.getUserMessage())
        assertEquals(false, repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    // ── loadNextPage() ──

    @Test
    fun loadNextPage_givenSuccess_appendsPage() = runTest {
        val page0 = DisputeListResponseDto(total = 3, data = listOf(DisputeListItemDto(id = "d-1")))
        coEvery { api.getPaged(offset = 0, limit = 20) } returns Response.success(page0)
        val repo = newRepo()
        repo.refresh()

        val page1 = DisputeListResponseDto(total = 3, data = listOf(DisputeListItemDto(id = "d-2")))
        coEvery { api.getPaged(offset = 1, limit = 20) } returns Response.success(page1)
        val result = repo.loadNextPage()

        assertTrue(result is ApiResult.Success)
        assertEquals(
            listOf(DisputeListItemDto(id = "d-1"), DisputeListItemDto(id = "d-2")),
            repo.disputes.value,
        )
    }

    @Test
    fun loadNextPage_givenHttpError_returnsErrorButRepoStaysSilent() = runTest {
        val page0 = DisputeListResponseDto(total = 5, data = listOf(DisputeListItemDto(id = "d-1")))
        coEvery { api.getPaged(offset = 0, limit = 20) } returns Response.success(page0)
        val repo = newRepo()
        repo.refresh()

        coEvery { api.getPaged(offset = 1, limit = 20) } returns Response.error(500, errBody())
        val result = repo.loadNextPage()

        // The repo returns the typed Error; the VM deliberately maps it to a
        // no-op (silent background path), so the repo never surfaces a snackbar.
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── getById() ──

    @Test
    fun getById_givenSuccess_returnsBody() = runTest {
        val details = DisputeDetailsDto(id = "d-1")
        coEvery { api.getById("d-1") } returns Response.success(details)

        val result = newRepo().getById("d-1")

        assertEquals(details, result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun getById_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { api.getById("d-1") } returns Response.error(500, errBody())

        val result = newRepo().getById("d-1")

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
    }

    @Test
    fun getById_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.getById("d-1") } throws java.io.IOException("boom")

        val result = newRepo().getById("d-1")

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // ── create() ──

    @Test
    fun create_givenSuccess_returnsNewId() = runTest {
        coEvery { api.create(any()) } returns Response.success("d-9")

        val result = newRepo().create("o-1", 3, "desc")

        assertEquals("d-9", result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun create_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { api.create(any()) } returns Response.error(500, errBody())

        val result = newRepo().create("o-1", 3, "desc")

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
    }

    @Test
    fun create_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.create(any()) } throws java.io.IOException("boom")

        val result = newRepo().create("o-1", 3, "desc")

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // ── addMessage() (fire-and-forget → ApiResult<Unit>) ──

    @Test
    fun addMessage_givenSuccess_returnsSuccessUnit() = runTest {
        coEvery { api.addMessage(any()) } returns Response.success(Unit)

        val result = newRepo().addMessage("d-1", "hello")

        assertEquals(Unit, result.getOrNull())
        assertTrue(result is ApiResult.Success)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun addMessage_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { api.addMessage(any()) } returns Response.error(500, errBody())

        val result = newRepo().addMessage("d-1", "hello")

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
    }

    @Test
    fun addMessage_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.addMessage(any()) } throws java.io.IOException("boom")

        val result = newRepo().addMessage("d-1", "hello")

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // ── uploadEvidence() ──

    @Test
    fun uploadEvidence_givenSuccess_returnsBody() = runTest {
        val response = UploadDisputeEvidenceResponse(evidenceId = "e-1")
        coEvery { api.uploadEvidence(any(), any()) } returns Response.success(response)

        val result = newRepo().uploadEvidence("d-1", byteArrayOf(1, 2, 3), "f.png", "image/png")

        assertEquals(response, result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun uploadEvidence_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { api.uploadEvidence(any(), any()) } returns Response.error(500, errBody())

        val result = newRepo().uploadEvidence("d-1", byteArrayOf(1, 2, 3), "f.png", "image/png")

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
    }

    @Test
    fun uploadEvidence_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.uploadEvidence(any(), any()) } throws java.io.IOException("boom")

        val result = newRepo().uploadEvidence("d-1", byteArrayOf(1, 2, 3), "f.png", "image/png")

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // ── clear() ──

    @Test
    fun clear_resetsDisputesTotalAndLoaded() = runTest {
        val page = DisputeListResponseDto(total = 1, data = listOf(DisputeListItemDto(id = "d-1")))
        coEvery { api.getPaged(offset = 0, limit = 20) } returns Response.success(page)
        val repo = newRepo()
        repo.refresh()
        assertTrue(repo.loaded.value)

        repo.clear()

        assertEquals(emptyList<DisputeListItemDto>(), repo.disputes.value)
        assertEquals(0, repo.totalRecords.value)
        assertEquals(false, repo.loaded.value)
    }
}
