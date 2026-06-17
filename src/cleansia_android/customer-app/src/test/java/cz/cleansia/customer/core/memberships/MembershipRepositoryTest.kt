package cz.cleansia.customer.core.memberships

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import io.mockk.coEvery
import io.mockk.coVerify
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
 * Characterization + post-migration contract for [MembershipRepository].
 *
 * Pins the observable repo behavior across the T-0197 migration from the legacy
 * `T?`-with-swallow-and-log form to the `ApiResult<T>` contract:
 *  - success returns the body in [ApiResult.Success] and warms the cache;
 *  - a transport failure (networkCall returns null) is the SILENT channel —
 *    [ApiResult.Error] carrying [ApiError.Network] (NetworkErrorInterceptor owns
 *    the infra toast, so the consuming ViewModel skips it — no double-toast);
 *  - an HTTP error returns [ApiResult.Error] carrying the parsed
 *    [cz.cleansia.customer.core.auth.ApiErrorParser] message, now surfaced by
 *    the VM.
 *
 * The repo never held a SnackbarController (it logged and returned null); after
 * migration it still doesn't — the snackbar lives in the consuming ViewModel.
 * The standalone [snackbar] mock here asserts the repo never surfaces one.
 */
class MembershipRepositoryTest {

    private lateinit var api: MembershipApi
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

    private fun newRepo() = MembershipRepository(api, appContext)

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    private fun membership(hasMembership: Boolean = true) = GetMyMembershipResponse(
        hasMembership = hasMembership,
        planCode = "plus_monthly",
        planName = "Plus",
    )

    private fun subscriptionResponse(membershipId: String = "") = CreateMembershipSubscriptionResponse(
        membershipId = membershipId,
        setupIntentClientSecret = "seti_secret",
        stripeCustomerId = "cus_1",
        ephemeralKey = "ek_1",
    )

    private fun cancelResponse() = CancelMembershipSubscriptionResponse(
        membershipId = "mem-1",
        effectiveEndDate = "2026-07-01",
    )

    private fun swapResponse() = SwapMembershipPlanResponse(
        membershipId = "mem-1",
        newPlanCode = "plus_yearly",
        currentPeriodEnd = "2026-12-01",
    )

    private fun plan(code: String) = MembershipPlanDto(
        code = code,
        name = "Plan $code",
        price = 199.0,
        monthlyEquivalentPrice = 199.0,
        billingInterval = 1,
        discountPercentage = 10.0,
        freeCancellationWindowHours = 24,
        allowsExpressUpgrade = false,
        trialPeriodDays = 14,
        savingsPercentVsMonthly = 0.0,
    )

    // ── refresh ──

    @Test
    fun refresh_givenSuccess_populatesCacheAndReturnsSuccess() = runTest {
        coEvery { api.getMine() } returns Response.success(membership())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(membership(), (result as ApiResult.Success).data)
        assertEquals(membership(), repo.current.value)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_givenHttp500_returnsServerErrorAndKeepsCache() = runTest {
        coEvery { api.getMine() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        assertEquals(serverMessage, result.error.message)
        assertEquals(null, repo.current.value)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refresh_whenTransportFails_returnsNetworkErrorSilently() = runTest {
        coEvery { api.getMine() } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue(
            "transport failure must carry ApiError.Network so the VM keeps it silent",
            (result as ApiResult.Error).error is ApiError.Network,
        )
        assertEquals(networkMessage, result.error.message)
        assertEquals(false, repo.loading.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── subscribePhase1 ──

    @Test
    fun subscribePhase1_givenSuccess_returnsBody() = runTest {
        coEvery { api.subscribe(any()) } returns Response.success(subscriptionResponse())

        val repo = newRepo()
        val result = repo.subscribePhase1("plus_monthly")

        assertTrue(result is ApiResult.Success)
        assertEquals(subscriptionResponse(), (result as ApiResult.Success).data)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun subscribePhase1_givenHttp400_returnsBadRequestMessage() = runTest {
        coEvery { api.subscribe(any()) } returns Response.error(400, errorBody())

        val repo = newRepo()
        val result = repo.subscribePhase1("plus_monthly")

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.BadRequest)
        assertEquals(unknownMessage, result.error.message)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun subscribePhase1_whenTransportFails_returnsNetworkErrorSilently() = runTest {
        coEvery { api.subscribe(any()) } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.subscribePhase1("plus_monthly")

        assertTrue(result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── subscribePhase2 (success invalidates the cache) ──

    @Test
    fun subscribePhase2_givenSuccess_returnsBodyAndRefreshesCache() = runTest {
        coEvery { api.subscribe(any()) } returns Response.success(subscriptionResponse("mem-99"))
        coEvery { api.getMine() } returns Response.success(membership())

        val repo = newRepo()
        val result = repo.subscribePhase2("plus_monthly", "tok-1")

        assertTrue(result is ApiResult.Success)
        assertEquals("mem-99", (result as ApiResult.Success).data.membershipId)
        // success invalidates the cache via a follow-up getMine()
        coVerify { api.getMine() }
        assertEquals(membership(), repo.current.value)
    }

    @Test
    fun subscribePhase2_givenHttp500_returnsErrorAndDoesNotRefresh() = runTest {
        coEvery { api.subscribe(any()) } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.subscribePhase2("plus_monthly", "tok-1")

        assertTrue(result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        coVerify(exactly = 0) { api.getMine() }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── cancel (success invalidates the cache) ──

    @Test
    fun cancel_givenSuccess_returnsBodyAndRefreshesCache() = runTest {
        coEvery { api.cancel() } returns Response.success(cancelResponse())
        coEvery { api.getMine() } returns Response.success(membership())

        val repo = newRepo()
        val result = repo.cancel()

        assertTrue(result is ApiResult.Success)
        assertEquals("2026-07-01", (result as ApiResult.Success).data.effectiveEndDate)
        coVerify { api.getMine() }
    }

    @Test
    fun cancel_givenHttp500_returnsErrorAndDoesNotRefresh() = runTest {
        coEvery { api.cancel() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.cancel()

        assertTrue(result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        coVerify(exactly = 0) { api.getMine() }
    }

    // ── swapPlan (success invalidates the cache) ──

    @Test
    fun swapPlan_givenSuccess_returnsBodyAndRefreshesCache() = runTest {
        coEvery { api.swapPlan(any()) } returns Response.success(swapResponse())
        coEvery { api.getMine() } returns Response.success(membership())

        val repo = newRepo()
        val result = repo.swapPlan("plus_yearly")

        assertTrue(result is ApiResult.Success)
        assertEquals("plus_yearly", (result as ApiResult.Success).data.newPlanCode)
        coVerify { api.getMine() }
    }

    @Test
    fun swapPlan_givenHttp400_returnsBadRequestAndDoesNotRefresh() = runTest {
        coEvery { api.swapPlan(any()) } returns Response.error(400, errorBody())

        val repo = newRepo()
        val result = repo.swapPlan("plus_yearly")

        assertTrue(result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.BadRequest)
        coVerify(exactly = 0) { api.getMine() }
    }

    // ── getPlans (cached catalog; Unit-of-cache semantics preserved) ──

    @Test
    fun getPlans_givenSuccess_returnsListAndCaches() = runTest {
        coEvery { api.getPlans() } returns Response.success(listOf(plan("a"), plan("b")))

        val repo = newRepo()
        val result = repo.getPlans()

        assertTrue(result is ApiResult.Success)
        assertEquals(listOf(plan("a"), plan("b")), (result as ApiResult.Success).data)

        // Second call returns the cache without a second network hit.
        val second = repo.getPlans()
        assertTrue(second is ApiResult.Success)
        assertEquals(listOf(plan("a"), plan("b")), (second as ApiResult.Success).data)
        coVerify(exactly = 1) { api.getPlans() }
    }

    @Test
    fun getPlans_givenHttp500_returnsServerErrorAndEmptyCache() = runTest {
        coEvery { api.getPlans() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.getPlans()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }
}
