package cz.cleansia.customer.core.loyalty

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
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Characterization + post-migration contract tests for [LoyaltyRepository].
 *
 * Pins the observable repo behavior across the T-0197 migration from the legacy
 * `String?`/`DTO?`-with-snackbar-in-repo form to the `ApiResult<T>` contract:
 *  - success returns the body in [ApiResult.Success] (and caches account/tiers);
 *  - a transport failure (networkCall returns null) is the SILENT channel —
 *    [ApiResult.Error] carrying [ApiError.Network] (NetworkErrorInterceptor owns
 *    the infra toast, so the consuming ViewModel skips it — no double-toast);
 *  - an HTTP error returns [ApiResult.Error] carrying the SAME single message
 *    the repo used to push to the snackbar (built from
 *    [cz.cleansia.customer.core.auth.ApiErrorParser]) — now surfaced by the VM.
 *
 * The repo no longer holds a SnackbarController; the snackbar moved to the VM
 * (RewardsActivityViewModel) / the RewardsTab composable. The standalone
 * `snackbar` mock asserts the repo never surfaces a snackbar itself.
 */
class LoyaltyRepositoryTest {

    private lateinit var api: LoyaltyApi
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

    private fun newRepo() = LoyaltyRepository(api, appContext)

    /**
     * `ApiError.Network` is never the channel for a 2xx whose body breaks the contract (ADR-0048 §D5
     * ground 3): the transport is the one thing that did not fail, and Network is the silent arm the
     * interceptor already owns — so a refused page came back as a ledger that simply stopped growing.
     */
    @Test
    fun loadActivity_givenA2xxWithNoBody_blamesTheServerAndNotTheConnection() = runTest {
        coEvery { api.getActivity(any(), any()) } returns Response.success(null)

        val result = newRepo().loadActivity(offset = 0)

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue(
            "a broken body is a server fault, got ${(result as ApiResult.Error).error}",
            result.error is ApiError.Server,
        )
    }

    /**
     * The account carries the points balance the whole Rewards tab is about. Assigning a null body
     * straight into the snapshot reported Success while the tab rendered the no-account state.
     */
    @Test
    fun refresh_givenA2xxWithNoAccountBody_refusesRatherThanReportingNoAccount() = runTest {
        coEvery { api.getMy() } returns Response.success(null)

        val result = newRepo().refresh()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
    }

    // ── refresh() ──

    @Test
    fun refresh_givenSuccess_cachesAccountAndTiersAndReturnsSuccess() = runTest {
        val account = LoyaltyAccountDto(currentTier = 2, lifetimePoints = 120)
        val tiers = LoyaltyTiersResponseDto(tiers = listOf(TierInfoDto(tier = 1), TierInfoDto(tier = 2)))
        coEvery { api.getMy() } returns Response.success(account)
        coEvery { api.getTiers() } returns Response.success(tiers)

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(account, repo.account.value)
        assertEquals(tiers.tiers, repo.tiers.value)
        assertEquals(true, repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_givenAccountHttpError_carriesParsedServerMessage() = runTest {
        // Empty body + 500 → server fallback string, carried in ApiError.Server —
        // the SAME single message the legacy repo pushed to the snackbar.
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.getMy() } returns Response.error(500, errBody)

        val repo = newRepo()
        val result = repo.refresh()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        assertEquals(false, repo.loaded.value)
        // The snackbar moved to the VM — the repo no longer surfaces it.
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenAccountThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.getMy() } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        // Transport failure → silent ApiError.Network channel (the consuming VM
        // skips Network so it does not double-toast NetworkErrorInterceptor).
        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Network)
        assertEquals(networkMessage, error.getUserMessage())
        assertEquals(false, repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_whenAccountSucceedsButTiersFail_stillSucceedsWithEmptyTiers() = runTest {
        // Tier ladder errors are swallowed inside refresh — a tiers failure must
        // not fail the whole refresh (the ladder UI falls back to a default).
        val account = LoyaltyAccountDto(currentTier = 1)
        coEvery { api.getMy() } returns Response.success(account)
        coEvery { api.getTiers() } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue(result is ApiResult.Success)
        assertEquals(account, repo.account.value)
        assertEquals(emptyList<TierInfoDto>(), repo.tiers.value)
        assertEquals(true, repo.loaded.value)
    }

    // ── loadActivity() ──

    @Test
    fun loadActivity_givenSuccess_returnsBody() = runTest {
        val body = LoyaltyActivityResponseDto(
            total = 1,
            data = listOf(LoyaltyActivityItemDto(type = 1, points = 10)),
        )
        coEvery { api.getActivity(offset = 0, limit = 20) } returns Response.success(body)

        val result = newRepo().loadActivity(offset = 0, limit = 20)

        assertEquals(body, result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun loadActivity_givenHttpError_carriesParsedServerMessage() = runTest {
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.getActivity(offset = 0, limit = 20) } returns Response.error(500, errBody)

        val result = newRepo().loadActivity(offset = 0, limit = 20)

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun loadActivity_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.getActivity(offset = 0, limit = 20) } throws java.io.IOException("boom")

        val result = newRepo().loadActivity(offset = 0, limit = 20)

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    // ── clear() ──

    @Test
    fun clear_resetsAccountTiersAndLoaded() = runTest {
        val account = LoyaltyAccountDto(currentTier = 3)
        val tiers = LoyaltyTiersResponseDto(tiers = listOf(TierInfoDto(tier = 1)))
        coEvery { api.getMy() } returns Response.success(account)
        coEvery { api.getTiers() } returns Response.success(tiers)
        val repo = newRepo()
        repo.refresh()
        assertEquals(account, repo.account.value)
        assertTrue(repo.loaded.value)

        repo.clear()

        assertEquals(null, repo.account.value)
        assertEquals(emptyList<TierInfoDto>(), repo.tiers.value)
        assertEquals(false, repo.loaded.value)
    }

    // ── staleness watermark ──

    @Test
    fun staleness_isStaleUntilAFetchSucceeds() = runTest {
        val repo = newRepo()
        assertTrue("a never-fetched cache must read stale", repo.staleness.isStale())

        coEvery { api.getMy() } returns Response.success(LoyaltyAccountDto(currentTier = 1))
        coEvery { api.getTiers() } returns Response.success(LoyaltyTiersResponseDto(tiers = emptyList()))
        repo.refresh()

        assertFalse("a successful refresh must stamp the watermark", repo.staleness.isStale())
    }

    @Test
    fun staleness_staysStaleWhenTheFetchFails() = runTest {
        // A failed fetch left the cache untouched, so the next screen entry must
        // still be allowed to retry.
        coEvery { api.getMy() } returns Response.error(500, "{}".toResponseBody("application/json".toMediaType()))

        val repo = newRepo()
        repo.refresh()

        assertTrue(repo.staleness.isStale())
    }

    @Test
    fun clear_resetsStalenessSoTheNextUserRefetches() = runTest {
        coEvery { api.getMy() } returns Response.success(LoyaltyAccountDto(currentTier = 3))
        coEvery { api.getTiers() } returns Response.success(LoyaltyTiersResponseDto(tiers = emptyList()))
        val repo = newRepo()
        repo.refresh()
        assertFalse(repo.staleness.isStale())

        repo.clear()

        assertTrue("sign-out must not leave the next session reading this one as fresh", repo.staleness.isStale())
    }
}
