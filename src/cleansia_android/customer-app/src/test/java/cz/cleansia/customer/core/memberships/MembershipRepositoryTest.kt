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
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.async
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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
        effectiveEndDate = "2026-07-01",
    )

    private fun swapResponse() = SwapMembershipPlanResponse(
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
        currencyCode = "CZK",
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
        val result = repo.subscribePhase1("plus_monthly", countryId = null)

        assertTrue(result is ApiResult.Success)
        assertEquals(subscriptionResponse(), (result as ApiResult.Success).data)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    /** ADR-0059 D2: both phases carry the chosen market, so the subscription is created in its currency. */
    @Test
    fun bothSubscribePhases_sendTheMarketsCountry() = runTest {
        val sent = mutableListOf<CreateMembershipSubscriptionRequest>()
        coEvery { api.subscribe(capture(sent)) } returns Response.success(subscriptionResponse("mem-1"))
        coEvery { api.getMine() } returns Response.success(membership())

        val repo = newRepo()
        repo.subscribePhase1("plus_monthly", countryId = "svk-id")
        repo.subscribePhase2("plus_monthly", "tok-1", countryId = "svk-id")

        assertEquals(listOf("svk-id", "svk-id"), sent.map { it.countryId })
        assertEquals(listOf(false, true), sent.map { it.paymentMethodConfirmed })
        assertEquals("tok-1", sent[1].idempotencyToken)
    }

    @Test
    fun subscribePhase1_givenHttp400_returnsBadRequestMessage() = runTest {
        coEvery { api.subscribe(any()) } returns Response.error(400, errorBody())

        val repo = newRepo()
        val result = repo.subscribePhase1("plus_monthly", countryId = null)

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.BadRequest)
        assertEquals(unknownMessage, result.error.message)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun subscribePhase1_whenTransportFails_returnsNetworkErrorSilently() = runTest {
        coEvery { api.subscribe(any()) } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.subscribePhase1("plus_monthly", countryId = null)

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
        val result = repo.subscribePhase2("plus_monthly", "tok-1", countryId = null)

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
        val result = repo.subscribePhase2("plus_monthly", "tok-1", countryId = null)

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

    // ── getPlans (cached per market) ──

    @Test
    fun getPlans_givenSuccess_returnsListAndCaches() = runTest {
        coEvery { api.getPlans("cze-id") } returns Response.success(listOf(plan("a"), plan("b")))

        val repo = newRepo()
        val result = repo.getPlans("cze-id")

        assertTrue(result is ApiResult.Success)
        assertEquals(listOf(plan("a"), plan("b")), (result as ApiResult.Success).data)

        // second call served from cache (no second api call)
        val second = repo.getPlans("cze-id")
        assertTrue(second is ApiResult.Success)
        assertEquals(listOf(plan("a"), plan("b")), (second as ApiResult.Success).data)
        coVerify(exactly = 1) { api.getPlans("cze-id") }
    }

    /** The rows are priced in the market's currency, so a list answered for another market is re-read. */
    @Test
    fun getPlans_forAnotherMarket_reReadsRatherThanServingTheCachedList() = runTest {
        coEvery { api.getPlans("cze-id") } returns Response.success(listOf(plan("a")))
        coEvery { api.getPlans("svk-id") } returns Response.success(emptyList())

        val repo = newRepo()
        repo.getPlans("cze-id")
        val svk = repo.getPlans("svk-id")
        val svkAgain = repo.getPlans("svk-id")

        assertEquals(emptyList<MembershipPlanDto>(), (svk as ApiResult.Success).data)
        assertEquals(emptyList<MembershipPlanDto>(), (svkAgain as ApiResult.Success).data)
        coVerify(exactly = 1) { api.getPlans("cze-id") }
        coVerify(exactly = 1) { api.getPlans("svk-id") }
    }

    /** The server's empty answer is a fact about the market ("Plus is not on sale here"), cached like any other. */
    @Test
    fun getPlans_cachesAnEmptyAnswerForTheMarket() = runTest {
        coEvery { api.getPlans("svk-id") } returns Response.success(emptyList())

        val repo = newRepo()
        repo.getPlans("svk-id")
        repo.getPlans("svk-id")

        coVerify(exactly = 1) { api.getPlans("svk-id") }
    }

    @Test
    fun getPlans_givenHttp500_returnsServerErrorAndEmptyCache() = runTest {
        coEvery { api.getPlans(null) } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.getPlans(null)

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun getPlans_concurrentSameMarketSharesOneSuccess() = runTest {
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        val completed = CompletableDeferred<Unit>()
        var calls = 0
        val plans = listOf(plan("shared"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            calls++
            entered.complete(Unit)
            try {
                release.await()
                Response.success(plans)
            } finally {
                completed.complete(Unit)
            }
        }
        val repo = newRepo()
        val first = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        val second = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        try {
            withTimeout(5_000) { entered.await() }
            assertEquals(1, calls)
            release.complete(Unit)
            assertEquals(ApiResult.Success(plans), first.await())
            assertEquals(ApiResult.Success(plans), second.await())
            withTimeout(5_000) { completed.await() }
            assertEquals(ApiResult.Success(plans), repo.getPlans("cze-id"))
            assertEquals(1, calls)
        } finally {
            release.complete(Unit)
            first.cancelAndJoin()
            second.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_concurrentNilMarketSharesFailureThenRetriesFresh() = runTest {
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        val completed = CompletableDeferred<Unit>()
        var calls = 0
        val plans = listOf(plan("retry"))
        coEvery { api.getPlans(null) } coAnswers {
            calls++
            if (calls == 1) {
                entered.complete(Unit)
                try {
                    release.await()
                    Response.error(500, errorBody())
                } finally {
                    completed.complete(Unit)
                }
            } else {
                Response.success(plans)
            }
        }
        val repo = newRepo()
        val first = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans(null) }
        val second = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans(null) }
        try {
            withTimeout(5_000) { entered.await() }
            assertEquals(1, calls)
            release.complete(Unit)
            val error = first.await()
            assertTrue(error is ApiResult.Error)
            assertTrue((error as ApiResult.Error).error is ApiError.Server)
            assertEquals(error, second.await())
            withTimeout(5_000) { completed.await() }
            assertEquals(ApiResult.Success(plans), repo.getPlans(null))
            assertEquals(2, calls)
        } finally {
            release.complete(Unit)
            first.cancelAndJoin()
            second.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_cancellingWaiterLeavesOwnerAndSuccessIntact() = runTest {
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        val completed = CompletableDeferred<Unit>()
        var calls = 0
        val plans = listOf(plan("owner"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            calls++
            entered.complete(Unit)
            try {
                release.await()
                Response.success(plans)
            } finally {
                completed.complete(Unit)
            }
        }
        val repo = newRepo()
        val owner = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        val waiter = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        try {
            withTimeout(5_000) { entered.await() }
            assertEquals(1, calls)
            waiter.cancelAndJoin()
            assertTrue(waiter.isCancelled)
            assertFalse(owner.isCancelled)
            assertFalse(completed.isCompleted)
            release.complete(Unit)
            assertEquals(ApiResult.Success(plans), owner.await())
            withTimeout(5_000) { completed.await() }
            assertEquals(ApiResult.Success(plans), repo.getPlans("cze-id"))
            assertEquals(1, calls)
        } finally {
            release.complete(Unit)
            owner.cancelAndJoin()
            waiter.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_cancellingOwnerLetsActiveWaiterElectFreshChild() = runTest {
        val firstEntered = CompletableDeferred<Unit>()
        val firstRelease = CompletableDeferred<Unit>()
        val firstCompleted = CompletableDeferred<Unit>()
        val replacementEntered = CompletableDeferred<Unit>()
        val replacementRelease = CompletableDeferred<Unit>()
        val replacementCompleted = CompletableDeferred<Unit>()
        var calls = 0
        val plans = listOf(plan("replacement"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            calls++
            if (calls == 1) {
                firstEntered.complete(Unit)
                try {
                    firstRelease.await()
                    Response.success(listOf(plan("cancelled")))
                } finally {
                    firstCompleted.complete(Unit)
                }
            } else {
                replacementEntered.complete(Unit)
                try {
                    replacementRelease.await()
                    Response.success(plans)
                } finally {
                    replacementCompleted.complete(Unit)
                }
            }
        }
        val repo = newRepo()
        val owner = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        val waiter = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        try {
            withTimeout(5_000) { firstEntered.await() }
            assertEquals(1, calls)
            owner.cancelAndJoin()
            withTimeout(5_000) { firstCompleted.await(); replacementEntered.await() }
            assertTrue(owner.isCancelled)
            assertFalse(waiter.isCancelled)
            assertEquals(2, calls)
            replacementRelease.complete(Unit)
            assertEquals(ApiResult.Success(plans), waiter.await())
            withTimeout(5_000) { replacementCompleted.await() }
            assertEquals(ApiResult.Success(plans), repo.getPlans("cze-id"))
            assertEquals(2, calls)
        } finally {
            firstRelease.complete(Unit)
            replacementRelease.complete(Unit)
            owner.cancelAndJoin()
            waiter.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_forceStartsFreshDuringOrdinaryFlightAndLateOwnerCannotReplaceIt() = runTest {
        val oldEntered = CompletableDeferred<Unit>()
        val oldRelease = CompletableDeferred<Unit>()
        val oldCompleted = CompletableDeferred<Unit>()
        val forcedEntered = CompletableDeferred<Unit>()
        val forcedRelease = CompletableDeferred<Unit>()
        var calls = 0
        val fresh = listOf(plan("fresh"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            calls++
            if (calls == 1) {
                oldEntered.complete(Unit)
                try {
                    withContext(NonCancellable) { oldRelease.await() }
                    Response.success(listOf(plan("obsolete")))
                } finally {
                    oldCompleted.complete(Unit)
                }
            } else {
                forcedEntered.complete(Unit)
                forcedRelease.await()
                Response.success(fresh)
            }
        }
        val repo = newRepo()
        val ordinary = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        try {
            withTimeout(5_000) { oldEntered.await() }
            val forced = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id", forceRefresh = true) }
            try {
                withTimeout(5_000) { forcedEntered.await() }
                assertEquals(2, calls)
                forcedRelease.complete(Unit)
                assertEquals(ApiResult.Success(fresh), forced.await())
                oldRelease.complete(Unit)
                withTimeout(5_000) { oldCompleted.await() }
                assertEquals(ApiResult.Success(fresh), ordinary.await())
                assertEquals(ApiResult.Success(fresh), repo.getPlans("cze-id"))
                assertEquals(2, calls)
            } finally {
                oldRelease.complete(Unit)
                forcedRelease.complete(Unit)
                forced.cancelAndJoin()
            }
        } finally {
            oldRelease.complete(Unit)
            forcedRelease.complete(Unit)
            ordinary.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_laterForceReplacesHeldForceWithoutAReplacementLoop() = runTest {
        val oldEntered = CompletableDeferred<Unit>()
        val oldRelease = CompletableDeferred<Unit>()
        val newEntered = CompletableDeferred<Unit>()
        val newRelease = CompletableDeferred<Unit>()
        var calls = 0
        val fresh = listOf(plan("new-force"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            calls++
            if (calls == 1) {
                oldEntered.complete(Unit)
                withContext(NonCancellable) { oldRelease.await() }
                Response.success(listOf(plan("old-force")))
            } else {
                newEntered.complete(Unit)
                newRelease.await()
                Response.success(fresh)
            }
        }
        val repo = newRepo()
        val first = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id", forceRefresh = true) }
        try {
            withTimeout(5_000) { oldEntered.await() }
            val second = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id", forceRefresh = true) }
            try {
                withTimeout(5_000) { newEntered.await() }
                assertEquals(2, calls)
                newRelease.complete(Unit)
                assertEquals(ApiResult.Success(fresh), second.await())
                oldRelease.complete(Unit)
                assertEquals(ApiResult.Success(fresh), first.await())
                assertEquals(2, calls)
                assertEquals(ApiResult.Success(fresh), repo.getPlans("cze-id", forceRefresh = true))
                assertEquals(3, calls)
            } finally {
                oldRelease.complete(Unit)
                newRelease.complete(Unit)
                second.cancelAndJoin()
            }
        } finally {
            oldRelease.complete(Unit)
            newRelease.complete(Unit)
            first.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_cancelledNewerForceCannotSatisfyOlderFreshRequestWithWarmCache() = runTest {
        val oldEntered = CompletableDeferred<Unit>()
        val oldRelease = CompletableDeferred<Unit>()
        val oldCompleted = CompletableDeferred<Unit>()
        val newerEntered = CompletableDeferred<Unit>()
        val newerRelease = CompletableDeferred<Unit>()
        val newerCompleted = CompletableDeferred<Unit>()
        val freshEntered = CompletableDeferred<Unit>()
        val freshRelease = CompletableDeferred<Unit>()
        val freshCompleted = CompletableDeferred<Unit>()
        var calls = 0
        val good = listOf(plan("warm-good"))
        val fresh = listOf(plan("fresh-after-cancel"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            when (++calls) {
                1 -> Response.success(good)
                2 -> {
                    oldEntered.complete(Unit)
                    try {
                        withContext(NonCancellable) { oldRelease.await() }
                        Response.success(listOf(plan("superseded-force")))
                    } finally {
                        oldCompleted.complete(Unit)
                    }
                }
                3 -> {
                    newerEntered.complete(Unit)
                    try {
                        newerRelease.await()
                        Response.success(listOf(plan("cancelled-newer-force")))
                    } finally {
                        newerCompleted.complete(Unit)
                    }
                }
                else -> {
                    freshEntered.complete(Unit)
                    try {
                        freshRelease.await()
                        Response.success(fresh)
                    } finally {
                        freshCompleted.complete(Unit)
                    }
                }
            }
        }
        val repo = newRepo()
        assertEquals(ApiResult.Success(good), repo.getPlans("cze-id"))
        val old = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id", forceRefresh = true) }
        try {
            withTimeout(5_000) { oldEntered.await() }
            val newer = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id", forceRefresh = true) }
            try {
                withTimeout(5_000) { newerEntered.await() }
                newer.cancelAndJoin()
                withTimeout(5_000) { newerCompleted.await() }
                assertTrue(newer.isCancelled)
                oldRelease.complete(Unit)
                withTimeout(5_000) { oldCompleted.await(); freshEntered.await() }
                assertEquals(4, calls)
                freshRelease.complete(Unit)
                assertEquals(ApiResult.Success(fresh), old.await())
                withTimeout(5_000) { freshCompleted.await() }
                assertEquals(ApiResult.Success(fresh), repo.getPlans("cze-id"))
                assertEquals(4, calls)
            } finally {
                oldRelease.complete(Unit)
                newerRelease.complete(Unit)
                freshRelease.complete(Unit)
                newer.cancelAndJoin()
            }
        } finally {
            oldRelease.complete(Unit)
            newerRelease.complete(Unit)
            freshRelease.complete(Unit)
            old.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_marketChangeAnswersOldWaitersSupersededWithoutReelectingOldCountry() = runTest {
        val oldEntered = CompletableDeferred<Unit>()
        val oldRelease = CompletableDeferred<Unit>()
        val newEntered = CompletableDeferred<Unit>()
        val newRelease = CompletableDeferred<Unit>()
        var oldCalls = 0
        var newCalls = 0
        val fresh = listOf(plan("svk"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            oldCalls++
            oldEntered.complete(Unit)
            withContext(NonCancellable) { oldRelease.await() }
            Response.success(listOf(plan("cze")))
        }
        coEvery { api.getPlans("svk-id") } coAnswers {
            newCalls++
            newEntered.complete(Unit)
            newRelease.await()
            Response.success(fresh)
        }
        val repo = newRepo()
        val owner = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        val waiter = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("cze-id") }
        try {
            withTimeout(5_000) { oldEntered.await() }
            assertEquals(1, oldCalls)
            val replacement = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans("svk-id") }
            try {
                withTimeout(5_000) { newEntered.await() }
                newRelease.complete(Unit)
                assertEquals(ApiResult.Success(fresh), replacement.await())
                oldRelease.complete(Unit)
                val ownerAnswer = owner.await()
                assertTrue(
                    "Old owner must stay in its superseded market, answered silently: $ownerAnswer",
                    ownerAnswer is ApiResult.Error && ownerAnswer.error is ApiError.Network,
                )
                assertFalse(owner.isCancelled)
                val waiterAnswer = waiter.await()
                assertTrue(
                    "Old waiter must not re-elect a country request, answered silently: $waiterAnswer",
                    waiterAnswer is ApiResult.Error && waiterAnswer.error is ApiError.Network,
                )
                assertFalse(waiter.isCancelled)
                assertEquals(ApiResult.Success(fresh), repo.getPlans("svk-id"))
                assertEquals(1, oldCalls)
                assertEquals(1, newCalls)
            } finally {
                oldRelease.complete(Unit)
                newRelease.complete(Unit)
                replacement.cancelAndJoin()
            }
        } finally {
            oldRelease.complete(Unit)
            newRelease.complete(Unit)
            owner.cancelAndJoin()
            waiter.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_clearRejectsLateNilMarketFlightAndNewSessionReadsFresh() = runTest {
        val oldEntered = CompletableDeferred<Unit>()
        val oldRelease = CompletableDeferred<Unit>()
        val newEntered = CompletableDeferred<Unit>()
        val newRelease = CompletableDeferred<Unit>()
        var calls = 0
        val fresh = listOf(plan("new-session"))
        coEvery { api.getPlans(null) } coAnswers {
            calls++
            if (calls == 1) {
                oldEntered.complete(Unit)
                withContext(NonCancellable) { oldRelease.await() }
                Response.success(listOf(plan("old-session")))
            } else {
                newEntered.complete(Unit)
                newRelease.await()
                Response.success(fresh)
            }
        }
        val repo = newRepo()
        val old = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans(null) }
        try {
            withTimeout(5_000) { oldEntered.await() }
            repo.clear()
            val new = async(start = CoroutineStart.UNDISPATCHED) { repo.getPlans(null) }
            try {
                withTimeout(5_000) { newEntered.await() }
                newRelease.complete(Unit)
                assertEquals(ApiResult.Success(fresh), new.await())
                oldRelease.complete(Unit)
                val oldAnswer = old.await()
                assertTrue(
                    "Cleared session must not return its old plans, answered silently: $oldAnswer",
                    oldAnswer is ApiResult.Error && oldAnswer.error is ApiError.Network,
                )
                assertFalse(old.isCancelled)
                assertEquals(ApiResult.Success(fresh), repo.getPlans(null))
                assertEquals(2, calls)
            } finally {
                newRelease.complete(Unit)
                new.cancelAndJoin()
            }
        } finally {
            oldRelease.complete(Unit)
            newRelease.complete(Unit)
            old.cancelAndJoin()
        }
    }

    /**
     * A superseded caller is still active: only its market moved. Answering it with a thrown
     * CancellationException would end a plain `collect` that called it, so it is answered silently.
     */
    @Test
    fun getPlans_supersededCallerKeepsAPlainCollectFollowing() = runTest {
        val czeEntered = CompletableDeferred<Unit>()
        val czeRelease = CompletableDeferred<Unit>()
        val fresh = listOf(plan("svk"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            czeEntered.complete(Unit)
            withContext(NonCancellable) { czeRelease.await() }
            Response.success(listOf(plan("cze")))
        }
        coEvery { api.getPlans("svk-id") } returns Response.success(fresh)
        val repo = newRepo()
        val market = MutableStateFlow<String?>("cze-id")
        val answers = mutableListOf<ApiResult<List<MembershipPlanDto>>>()
        val follower = launch(start = CoroutineStart.UNDISPATCHED) {
            market.collect { answers += repo.getPlans(it) }
        }
        try {
            withTimeout(5_000) { czeEntered.await() }
            assertEquals(ApiResult.Success(fresh), repo.getPlans("svk-id"))
            czeRelease.complete(Unit)
            market.value = "svk-id"
            advanceUntilIdle()
            assertTrue("a plain collect must outlive its superseded read", follower.isActive)
            val superseded = answers.first()
            assertTrue(
                "expected the silent superseded answer but got: $superseded",
                superseded is ApiResult.Error && superseded.error is ApiError.Network,
            )
            assertEquals(ApiResult.Success(fresh), answers.last())
        } finally {
            czeRelease.complete(Unit)
            follower.cancelAndJoin()
        }
    }

    @Test
    fun getPlans_forcedFailureRetainsSettledGoodCacheAndSequentialForcesStayFresh() = runTest {
        var calls = 0
        val good = listOf(plan("good"))
        coEvery { api.getPlans("cze-id") } coAnswers {
            calls++
            if (calls == 1) Response.success(good) else Response.error(500, errorBody())
        }
        val repo = newRepo()
        assertEquals(ApiResult.Success(good), repo.getPlans("cze-id"))
        assertTrue(repo.getPlans("cze-id", forceRefresh = true) is ApiResult.Error)
        assertEquals(ApiResult.Success(good), repo.getPlans("cze-id"))
        assertTrue(repo.getPlans("cze-id", forceRefresh = true) is ApiResult.Error)
        assertEquals(3, calls)
    }

    // ── staleness watermark ──

    @Test
    fun staleness_isStaleUntilARefreshSucceeds() = runTest {
        val repo = newRepo()
        assertTrue("a never-fetched cache must read stale", repo.staleness.isStale())

        coEvery { api.getMine() } returns Response.success(membership())
        repo.refresh()

        assertFalse("a successful refresh must stamp the watermark", repo.staleness.isStale())
    }

    @Test
    fun staleness_staysStaleWhenTheRefreshFails() = runTest {
        coEvery { api.getMine() } returns Response.error(500, errorBody())

        val repo = newRepo()
        repo.refresh()

        assertTrue(repo.staleness.isStale())
    }

    @Test
    fun clear_resetsStalenessSoTheNextUserRefetches() = runTest {
        coEvery { api.getMine() } returns Response.success(membership())
        val repo = newRepo()
        repo.refresh()
        assertFalse(repo.staleness.isStale())

        repo.clear()

        assertTrue("sign-out must not leave the next session reading this one as fresh", repo.staleness.isStale())
    }

    /** S11: a lookup still in flight at sign-out must not write the previous user's membership back. */
    @Test
    fun refresh_heldAcrossClearPublishesNothingAndStaysStale() = runTest {
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        coEvery { api.getMine() } coAnswers {
            entered.complete(Unit)
            release.await()
            Response.success(membership())
        }
        val repo = newRepo()
        val old = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh() }
        try {
            withTimeout(5_000) { entered.await() }
            repo.clear()
            release.complete(Unit)
            val result = old.await()
            assertTrue(
                "expected the silent superseded answer but got: $result",
                result is ApiResult.Error && result.error is ApiError.Network,
            )
            assertFalse(old.isCancelled)
            assertEquals(null, repo.current.value)
            assertTrue("the cleared session's read must not stamp the watermark", repo.staleness.isStale())
            assertFalse(repo.loading.value)
        } finally {
            release.complete(Unit)
            old.cancelAndJoin()
        }
    }

    /**
     * A refresh queued before sign-out belongs to the old session and never reaches the API; one made
     * after it reads and publishes the new session's membership.
     */
    @Test
    fun refresh_queuedBehindAClearedOneReadsTheNewSession() = runTest {
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        var calls = 0
        val newSession = membership(hasMembership = false)
        coEvery { api.getMine() } coAnswers {
            calls++
            if (calls == 1) {
                entered.complete(Unit)
                release.await()
                Response.success(membership())
            } else {
                Response.success(newSession)
            }
        }
        val repo = newRepo()
        val old = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh() }
        val queuedBeforeClear = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh() }
        try {
            withTimeout(5_000) { entered.await() }
            repo.clear()
            val new = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh() }
            release.complete(Unit)
            assertEquals(ApiResult.Success(newSession), new.await())
            val stale = queuedBeforeClear.await()
            assertTrue(
                "a refresh queued before sign-out must be answered silently: $stale",
                stale is ApiResult.Error && stale.error is ApiError.Network,
            )
            assertEquals(newSession, repo.current.value)
            assertFalse(repo.staleness.isStale())
            assertEquals("only the held read and the new session's read reach the API", 2, calls)
        } finally {
            release.complete(Unit)
            old.cancelAndJoin()
            queuedBeforeClear.cancelAndJoin()
        }
    }

    /** A market change moves the plans' generation only; it never discards a membership refresh. */
    @Test
    fun refresh_survivesAMarketChangeWhileInFlight() = runTest {
        val entered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        coEvery { api.getMine() } coAnswers {
            entered.complete(Unit)
            release.await()
            Response.success(membership())
        }
        coEvery { api.getPlans("svk-id") } returns Response.success(listOf(plan("svk")))
        val repo = newRepo()
        val refresh = async(start = CoroutineStart.UNDISPATCHED) { repo.refresh() }
        try {
            withTimeout(5_000) { entered.await() }
            assertEquals(ApiResult.Success(listOf(plan("svk"))), repo.getPlans("svk-id"))
            release.complete(Unit)
            assertEquals(ApiResult.Success(membership()), refresh.await())
            assertEquals(membership(), repo.current.value)
            assertFalse(repo.staleness.isStale())
        } finally {
            release.complete(Unit)
            refresh.cancelAndJoin()
        }
    }
}
