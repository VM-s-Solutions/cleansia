package cz.cleansia.customer.core.orders
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.AuthAuthenticator

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.network.requiredBody
import cz.cleansia.core.network.wireResult
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import javax.inject.Inject
import kotlinx.coroutines.CancellationException
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Cache + orchestrator for the user's orders, with additive pagination. `@Singleton`, so it lives for the
 * process.
 *
 * **Cleared on sign-out / account-delete.** Errors carry the parsed message for the snackbar; network
 * failures stay silent because the interceptor owns that toast.
 * -> /mobile-app/patterns#session-wipe
 */
@Singleton
class OrderRepository @Inject constructor(
    private val api: OrderApi,
    @ApplicationContext private val appContext: Context,
) : cz.cleansia.core.auth.SessionScopedCache {
    private val _orders = MutableStateFlow<List<OrderListItemDto>>(emptyList())
    val orders: StateFlow<List<OrderListItemDto>> = _orders.asStateFlow()

    private val _totalRecords = MutableStateFlow(0)
    val totalRecords: StateFlow<Int> = _totalRecords.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    private val _loadingMore = MutableStateFlow(false)
    val loadingMore: StateFlow<Boolean> = _loadingMore.asStateFlow()

    private val _loaded = MutableStateFlow(false)
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    /**
     * Freshness watermark for the page-0 cache. Distinct from [loaded], which is
     * a one-way first-paint latch and so cannot answer "has this aged?" — screens
     * that merely observe [orders] check [Staleness.isStale] on entry to decide
     * whether a background [refresh] is worth the round-trip. Only [refresh]
     * stamps it; [loadNextPage] appends later pages and leaves page 0 as old as
     * it was.
     */
    val staleness = Staleness()

    private val pageSize = 20

    /**
     * Rows the server has SENT so far — the offset of the next page and the term the stop condition
     * compares against [totalRecords]. Not `orders.size`: the page mapper drops an unidentifiable
     * row, so every drop would otherwise re-request the survivors and hold `size >= total` false
     * forever, which is a list that keeps asking for pages it already has.
     */
    private var receivedSoFar = 0

    /** A page the server answered with no rows at all ends paging; the counter alone cannot. */
    private var exhausted = false

    /**
     * Fetch page 0 and replace the cache. Intended for pull-to-refresh and
     * initial screen loads.
     */
    suspend fun refresh(): ApiResult<Unit> = wireResult {
        if (_loading.value) return ApiResult.Success(Unit)
        _loading.value = true
        try {
            val resp = networkCall { api.getMyOrders(offset = 0, limit = pageSize) }
                ?: return networkError()
            if (!resp.isSuccessful) {
                return httpError(resp.errorBody(), resp.code())
            }
            val body = resp.requiredBody()
            _orders.value = body.data
            receivedSoFar = body.receivedCount
            exhausted = body.receivedCount == 0
            // Backend PagedData wrapper exposes total under `total`; expose it as
            // totalRecords in the repo API to keep consumers agnostic of the wire name.
            _totalRecords.value = body.total
            _loaded.value = true
            staleness.markFresh()
            return ApiResult.Success(Unit)
        } finally {
            _loading.value = false
        }
    }

    /**
     * Append the next page to the cache, if we have not already exhausted
     * [totalRecords]. Silent on failure — the caller ignores the
     * [ApiResult.Error] and the user can trigger another load by scrolling again.
     */
    suspend fun loadNextPage(): ApiResult<Unit> = wireResult {
        if (_loadingMore.value) return ApiResult.Success(Unit)
        if (exhausted || receivedSoFar >= _totalRecords.value) return ApiResult.Success(Unit)
        _loadingMore.value = true
        try {
            val resp = networkCall { api.getMyOrders(offset = receivedSoFar, limit = pageSize) }
                ?: return networkError()
            if (!resp.isSuccessful) return httpError(resp.errorBody(), resp.code())
            val body = resp.requiredBody()
            _orders.value = _orders.value + body.data
            receivedSoFar += body.receivedCount
            exhausted = body.receivedCount == 0
            _totalRecords.value = body.total
            return ApiResult.Success(Unit)
        } finally {
            _loadingMore.value = false
        }
    }

    /** Fetch a single order's detail. */
    suspend fun getById(id: String): ApiResult<OrderDetailDto> = wireResult {
        val resp = networkCall { api.getById(id) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * Cancel an order. Returns the parsed response (fee rate / refund details)
     * on success.
     */
    suspend fun cancel(orderId: String, reason: String?): ApiResult<CancelOrderResponse> = wireResult {
        val resp = networkCall { api.cancel(CancelOrderRequest(orderId = orderId, reason = reason)) }
            ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * What cancelling this order would cost, asked before the customer commits.
     * A pure read: opening and closing the sheet spends nothing. The answer is
     * only true for the instant it was computed, so callers re-ask rather than
     * cache it.
     */
    suspend fun getCancellationPreview(orderId: String): ApiResult<CancellationFeePreviewDto> = wireResult {
        val resp = networkCall { api.getCancellationPreview(orderId) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * Wave 3.3 — confirm a Pending recurring-template order. Cash response
     * means the order's already Confirmed + Paid backend-side; the caller
     * should refetch + show success. Card response carries the Stripe
     * PaymentIntent fields the mobile PaymentSheet needs.
     */
    suspend fun confirmRecurring(orderId: String): ApiResult<ConfirmRecurringOrderResponse> = wireResult {
        val resp = networkCall {
            api.confirmRecurring(ConfirmRecurringOrderRequest(orderId = orderId))
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /** Submit (or update) a review on a completed order. Returns the persisted review on success. */
    suspend fun submitReview(
        orderId: String,
        rating: Int,
        comment: String?,
        tags: List<ReviewTag> = emptyList(),
    ): ApiResult<OrderReviewDto> = wireResult {
        val resp = networkCall {
            api.submitReview(
                SubmitReviewRequest(orderId = orderId, rating = rating, comment = comment, tags = tags),
            )
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * Download the receipt PDF and save it to
     * `{context.cacheDir}/receipts/{orderId}.pdf`. Returns the saved File on
     * success. Creates the `receipts/` subdirectory on first use.
     *
     * Uses streaming copy so we don't have to load the full PDF into memory.
     */
    suspend fun downloadReceipt(orderId: String): ApiResult<File> {
        val resp = networkCall { api.downloadReceipt(orderId) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        val body = resp.body() ?: return networkError()
        return try {
            val dir = File(appContext.cacheDir, "receipts").apply { mkdirs() }
            val file = File(dir, "$orderId.pdf")
            body.byteStream().use { input ->
                file.outputStream().use { output -> input.copyTo(output) }
            }
            ApiResult.Success(file)
        } catch (ce: CancellationException) {
            throw ce
        } catch (t: Throwable) {
            // A local file-write failure (not a transport error) — surface the
            // same generic-network message the repo used to toast directly. It
            // is carried in a message-bearing [ApiError.Unknown] so the consuming
            // ViewModel still shows it (an [ApiError.Network] would be swallowed
            // as an infra toast owned by NetworkErrorInterceptor).
            ApiResult.Error(ApiError.Unknown(appContext.getString(R.string.error_generic_network)))
        }
    }

    /** Fetch the before/after photos for an order. Fetcher pattern — mirrors [getById]. */
    suspend fun getPhotos(orderId: String): ApiResult<OrderPhotosResponse> = wireResult {
        val resp = networkCall { api.getPhotos(orderId) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * Cleaners the calling user has had a Completed order with. Drives the
     * "request your favorite cleaner" picker on the booking flow. Silent on
     * failure — the caller ignores the [ApiResult.Error] and the picker just
     * shows an empty state so the booking proceeds without a preference
     * (backend matching algorithm picks normally).
     */
    suspend fun getMyServingCleaners(): ApiResult<List<ServingCleanerDto>> = wireResult {
        val resp = networkCall { api.getMyServingCleaners() } ?: return networkError()
        if (!resp.isSuccessful) return httpError(resp.errorBody(), resp.code())
        return ApiResult.Success(resp.body().orEmpty())
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Nothing> {
        // Carry the message [ApiErrorParser] already resolved from the body so
        // the surfacing ViewModel shows the identical string. The 401 object
        // would drop that message, so it folds into the message-carrying
        // [ApiError.Unknown] alongside the generic fallback.
        val message = ApiErrorParser.parseToUserMessage(appContext, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    override suspend fun clear() {
        _orders.value = emptyList()
        _totalRecords.value = 0
        receivedSoFar = 0
        exhausted = false
        _loaded.value = false
        staleness.reset()
    }
}
