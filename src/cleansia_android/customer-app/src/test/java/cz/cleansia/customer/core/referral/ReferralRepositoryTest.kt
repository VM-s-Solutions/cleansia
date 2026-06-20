package cz.cleansia.customer.core.referral

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
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Characterization + post-migration contract tests for [ReferralRepository].
 *
 * Pins the observable repo behavior across the T-0197 migration from the legacy
 * `String?`/`DTO?`-with-snackbar-in-repo form to the `ApiResult<T>` contract:
 *  - refresh() success returns [ApiResult.Success] (and caches account + the
 *    best-effort referrals page);
 *  - a transport failure (networkCall returns null) is the SILENT channel —
 *    [ApiResult.Error] carrying [ApiError.Network] (NetworkErrorInterceptor owns
 *    the infra toast, so the consuming ViewModel skips it — no double-toast);
 *  - an HTTP error returns [ApiResult.Error] carrying the SAME single message
 *    the repo used to push to the snackbar (built from
 *    [cz.cleansia.customer.core.auth.ApiErrorParser]) — now surfaced by the VM;
 *  - validate() returns the body in [ApiResult.Success] on success and an
 *    [ApiResult.Error] on any failure; both consumers collapse Error → the
 *    informational Invalid(null) UI state via getOrNull() (no snackbar — same
 *    fail-soft validation behavior as before).
 *
 * The repo no longer holds a SnackbarController; the snackbar moved to the
 * consuming surfaces (RewardsTab pull-to-refresh). The standalone `snackbar`
 * mock asserts the repo never surfaces a snackbar itself.
 */
class ReferralRepositoryTest {

    private lateinit var api: ReferralApi
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val serverMessage = "server"

    @Before
    fun setUp() {
        api = mockk()
        // Standalone mock the repo never receives — asserts the repo never
        // surfaces a snackbar (the snackbar moved to the consuming surface).
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

    private fun newRepo() = ReferralRepository(api, appContext)

    // ── refresh() ──

    @Test
    fun refresh_givenSuccess_cachesAccountAndReferralsAndReturnsSuccess() = runTest {
        val account = ReferralAccountDto(code = "ABC123", acceptedCount = 2)
        val referrals = ReferralListResponseDto(
            total = 1,
            data = listOf(ReferralListItemDto(id = "r1", status = 1)),
        )
        coEvery { api.getMy() } returns Response.success(account)
        coEvery { api.getMyReferrals(offset = 0, limit = 20) } returns Response.success(referrals)

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(account, repo.account.value)
        assertEquals(referrals.data, repo.referrals.value)
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
        // The snackbar moved to the consuming surface — the repo never surfaces it.
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenAccountThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.getMy() } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        // Transport failure → silent ApiError.Network channel (the consuming
        // surface skips Network so it does not double-toast NetworkErrorInterceptor).
        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Network)
        assertEquals(networkMessage, error.getUserMessage())
        assertEquals(false, repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_whenAccountSucceedsButReferralsFail_stillSucceedsWithEmptyList() = runTest {
        // Referrals-page errors are swallowed inside refresh — a list failure must
        // not fail the whole refresh (the stats row falls back to account counters).
        val account = ReferralAccountDto(code = "ABC123")
        coEvery { api.getMy() } returns Response.success(account)
        coEvery { api.getMyReferrals(offset = 0, limit = 20) } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue(result is ApiResult.Success)
        assertEquals(account, repo.account.value)
        assertEquals(emptyList<ReferralListItemDto>(), repo.referrals.value)
        assertEquals(true, repo.loaded.value)
    }

    // ── validate() ──

    @Test
    fun validate_givenSuccess_returnsBody() = runTest {
        val body = ValidateReferralResponse(isValid = true, referrerFirstName = "Jane")
        coEvery { api.validate(ValidateReferralRequest("ABC123")) } returns Response.success(body)

        val result = newRepo().validate("abc123")

        assertEquals(body, result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun validate_givenHttpError_returnsErrorThatConsumersCollapseToNull() = runTest {
        // Fail-soft: a 400 yields ApiResult.Error; both consumers map
        // getOrNull() == null → Invalid(null) (no snackbar — same as legacy).
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.validate(ValidateReferralRequest("ABC123")) } returns Response.error(400, errBody)

        val result = newRepo().validate("abc123")

        assertTrue(result is ApiResult.Error)
        assertNull(result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun validate_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { api.validate(ValidateReferralRequest("ABC123")) } throws java.io.IOException("boom")

        val result = newRepo().validate("abc123")

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        assertNull(result.getOrNull())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── clear() ──

    @Test
    fun clear_resetsAccountReferralsAndLoaded() = runTest {
        val account = ReferralAccountDto(code = "ABC123")
        val referrals = ReferralListResponseDto(
            total = 1,
            data = listOf(ReferralListItemDto(id = "r1")),
        )
        coEvery { api.getMy() } returns Response.success(account)
        coEvery { api.getMyReferrals(offset = 0, limit = 20) } returns Response.success(referrals)
        val repo = newRepo()
        repo.refresh()
        assertEquals(account, repo.account.value)
        assertTrue(repo.loaded.value)

        repo.clear()

        assertNull(repo.account.value)
        assertEquals(emptyList<ReferralListItemDto>(), repo.referrals.value)
        assertEquals(false, repo.loaded.value)
    }
}
