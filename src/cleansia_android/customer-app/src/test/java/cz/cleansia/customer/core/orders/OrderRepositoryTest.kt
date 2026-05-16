package cz.cleansia.customer.core.orders
import cz.cleansia.core.auth.NetworkErrorInterceptor

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.customer.R
import cz.cleansia.core.snackbar.SnackbarController
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.async
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * OrderRepository tests — refresh, pagination, getById, cancel, submitReview.
 * Uses Turbine to validate StateFlow transitions on `loading` and `orders`.
 */
class OrderRepositoryTest {

    private lateinit var api: OrderApi
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val unknownMessage = "Something went wrong. Please try again."

    @Before
    fun setUp() {
        api = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns unknownMessage
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.getString(R.string.error_generic_server) } returns "server"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newRepo() = OrderRepository(api, snackbar, appContext)

    private fun listItem(id: String) = OrderListItemDto(id = id)

    // ── refresh() ──

    @Test
    fun refresh_givenSuccessfulPage_updatesCacheAndFlipsLoadedTrue() = runTest {
        val page = OrderListResponseDto(
            pageNumber = 1,
            pageSize = 20,
            total = 1,
            data = listOf(listItem("o-1")),
        )
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.success(page)

        val repo = newRepo()
        val result = repo.refresh()

        assertNull("expected null on success but got: $result", result)
        assertEquals(listOf(listItem("o-1")), repo.orders.value)
        assertEquals(1, repo.totalRecords.value)
        assertTrue(repo.loaded.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_whenApiThrows_returnsNetworkMessageAndDoesNotClearCache() = runTest {
        // Pre-populate the cache via a successful first refresh, then fail the next.
        val firstPage = OrderListResponseDto(total = 1, data = listOf(listItem("o-1")))
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.success(firstPage)

        val repo = newRepo()
        repo.refresh()
        assertEquals(1, repo.orders.value.size)

        // Now fail the next refresh — cache should be preserved.
        coEvery { api.getMyOrders(offset = 0, limit = 20) } throws java.io.IOException("boom")
        val msg = repo.refresh()

        // Repo returns the network message for the caller's fallback UI, but does
        // NOT surface a snackbar — NetworkErrorInterceptor already shows one for
        // infrastructure failures, so doing it again would double-toast.
        assertEquals(networkMessage, msg)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertEquals("cache must be preserved on refresh failure", 1, repo.orders.value.size)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_givenHttpError_parsesAndSurfacesMessage() = runTest {
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.error(500, errBody)

        val msg = newRepo().refresh()

        // Empty body + 500 → server fallback string
        assertEquals("server", msg)
        verify { snackbar.showError("server") }
    }

    // ── loadNextPage() ──

    @Test
    fun loadNextPage_appendsToExistingItemsAndStopsWhenExhausted() = runTest {
        // Initial refresh with total=2 and one item → repo knows there's more to load.
        val firstPage = OrderListResponseDto(total = 2, data = listOf(listItem("o-1")))
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.success(firstPage)
        val repo = newRepo()
        repo.refresh()

        coEvery { api.getMyOrders(offset = 1, limit = 20) } returns Response.success(
            OrderListResponseDto(total = 2, data = listOf(listItem("o-2"))),
        )

        repo.loadNextPage()
        assertEquals(listOf(listItem("o-1"), listItem("o-2")), repo.orders.value)

        // Second call: cache size now equals total → repo short-circuits, no API call.
        repo.loadNextPage()
        coVerify(exactly = 1) { api.getMyOrders(offset = 1, limit = 20) }
    }

    @Test
    fun loadNextPage_doesNothingWhenAlreadyLoading() = runTest {
        // Bring repo into the "has-more-pages" state so loadNextPage isn't a
        // no-op for size-vs-total reasons.
        val firstPage = OrderListResponseDto(total = 5, data = listOf(listItem("o-1")))
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.success(firstPage)
        val repo = newRepo()
        repo.refresh()

        // Set up a slow response so we can fire two concurrent calls.
        val gate = kotlinx.coroutines.CompletableDeferred<Response<OrderListResponseDto>>()
        coEvery { api.getMyOrders(offset = 1, limit = 20) } coAnswers { gate.await() }

        val first = async { repo.loadNextPage() }
        // Run the first loadNextPage() body up to the suspending API call so
        // that _loadingMore is true when the second call below evaluates.
        runCurrent()
        val second = repo.loadNextPage()
        assertNull("second concurrent call should bail out", second)

        gate.complete(Response.success(OrderListResponseDto(total = 5, data = listOf(listItem("o-2")))))
        first.await()

        // Only one network call should have been made.
        coVerify(exactly = 1) { api.getMyOrders(offset = 1, limit = 20) }
    }

    @Test
    fun loadNextPage_swallowsNetworkErrorsSilently() = runTest {
        val firstPage = OrderListResponseDto(total = 5, data = listOf(listItem("o-1")))
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.success(firstPage)
        val repo = newRepo()
        repo.refresh()

        coEvery { api.getMyOrders(offset = 1, limit = 20) } throws java.io.IOException("boom")
        val msg = repo.loadNextPage()

        assertNull("background page failures must be silent", msg)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertEquals(false, repo.loadingMore.value)
    }

    // ── getById() ──

    @Test
    fun getById_givenSuccess_returnsBody() = runTest {
        val detail = OrderDetailDto(id = "o-1")
        coEvery { api.getById("o-1") } returns Response.success(detail)

        val result = newRepo().getById("o-1")

        assertEquals(detail, result)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun getById_givenHttp404_returnsNullAndSurfacesParsedMessage() = runTest {
        // Note: source treats 404 like any other error — surfaces snackbar with
        // the parsed message and returns null. Documented here so the contract
        // is explicit. If product wants 404 to be silent, a TODO can flag it.
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.getById("o-x") } returns Response.error(404, errBody)

        val result = newRepo().getById("o-x")

        assertNull(result)
        // Empty body + 404 → falls into the "else" branch → unknownMessage
        verify { snackbar.showError(unknownMessage) }
    }

    @Test
    fun getById_whenApiThrows_returnsNullAndDoesNotDoubleToast() = runTest {
        // Infrastructure failures (IOException, 5xx) are surfaced by
        // NetworkErrorInterceptor — repos must not also showError, or the user
        // sees two toasts for the same failure.
        coEvery { api.getById(any()) } throws java.io.IOException("boom")
        val result = newRepo().getById("o-1")
        assertNull(result)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── cancel() ──

    @Test
    fun cancel_givenSuccess_returnsResponse() = runTest {
        val resp = CancelOrderResponse(orderId = "o-1", feeRate = 0.5, refundAmount = 10.0)
        coEvery { api.cancel(any()) } returns Response.success(resp)

        val result = newRepo().cancel("o-1", reason = "changed mind")

        assertEquals(resp, result)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun cancel_givenHttpError_returnsNullAndSurfacesSnackbar() = runTest {
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { api.cancel(any()) } returns Response.error(400, errBody)

        val result = newRepo().cancel("o-1", reason = null)

        assertNull(result)
        verify(exactly = 1) { snackbar.showError(any<String>()) }
    }

    // ── submitReview() ──

    @Test
    fun submitReview_givenSuccess_returnsReview() = runTest {
        val review = OrderReviewDto(id = "r-1", orderId = "o-1", rating = 5)
        coEvery { api.submitReview(any()) } returns Response.success(review)

        val result = newRepo().submitReview("o-1", rating = 5, comment = "great")

        assertEquals(review, result)
    }

    @Test
    fun submitReview_whenApiThrows_returnsNullAndDoesNotDoubleToast() = runTest {
        // See getById_whenApiThrows_… — repos defer infrastructure-error toasts
        // to NetworkErrorInterceptor.
        coEvery { api.submitReview(any()) } throws java.io.IOException("boom")
        val result = newRepo().submitReview("o-1", rating = 5, comment = null)
        assertNull(result)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── clear() ──

    @Test
    fun clear_resetsOrdersAndCounters() = runTest {
        val firstPage = OrderListResponseDto(total = 1, data = listOf(listItem("o-1")))
        coEvery { api.getMyOrders(offset = 0, limit = 20) } returns Response.success(firstPage)
        val repo = newRepo()
        repo.refresh()
        assertEquals(1, repo.orders.value.size)
        assertTrue(repo.loaded.value)

        repo.clear()

        assertEquals(emptyList<OrderListItemDto>(), repo.orders.value)
        assertEquals(0, repo.totalRecords.value)
        assertEquals(false, repo.loaded.value)
    }

    // ── loading flow transitions (Turbine) ──

    @Test
    fun refresh_loadingFlowTransitionsTrueThenFalse() = runTest {
        // Hold the API in flight so we can observe the loading=true window.
        val gate = kotlinx.coroutines.CompletableDeferred<Response<OrderListResponseDto>>()
        coEvery { api.getMyOrders(offset = 0, limit = 20) } coAnswers { gate.await() }

        val repo = newRepo()
        repo.loading.test {
            assertEquals(false, awaitItem())
            val job = async { repo.refresh() }
            // Drive the scheduler so refresh() flips _loading to true and suspends.
            runCurrent()
            assertEquals(true, awaitItem())
            gate.complete(
                Response.success(OrderListResponseDto(total = 1, data = listOf(listItem("o-1")))),
            )
            job.await()
            assertEquals(false, awaitItem())
            cancelAndIgnoreRemainingEvents()
        }
        assertNotNull(repo.orders.value.firstOrNull())
    }
}
