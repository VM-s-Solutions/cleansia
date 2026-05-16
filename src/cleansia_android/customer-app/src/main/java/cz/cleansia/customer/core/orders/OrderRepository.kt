package cz.cleansia.customer.core.orders
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.AuthAuthenticator

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import javax.inject.Inject
import kotlinx.coroutines.CancellationException
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Cache + orchestrator for the signed-in user's orders.
 *
 * Lifetime: `@Singleton` — lives for the app process. Caches the list of
 * orders (`orders`) and supports additive pagination via [loadNextPage].
 * Cleared on sign-out / account-delete so the next user doesn't inherit this
 * one's data — call sites are wired in AuthAuthenticator, AuthRepository,
 * and UserRepository alongside the matching AddressRepository.clear() hooks.
 *
 * Error model mirrors [cz.cleansia.customer.core.data.AddressRepository]:
 *  - Foreground operations ([refresh], [getById]) surface failures via
 *    the shared [SnackbarController] and return a translated error string.
 *  - Background page loads ([loadNextPage]) are silent on network errors —
 *    pulling down or scrolling again will retry.
 */
@Singleton
class OrderRepository @Inject constructor(
    private val api: OrderApi,
    private val snackbar: SnackbarController,
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

    private val pageSize = 20

    /**
     * Fetch page 0 and replace the cache. Intended for pull-to-refresh and
     * initial screen loads.
     *
     * @return null on success, localized error message on failure.
     */
    suspend fun refresh(): String? {
        if (_loading.value) return null
        _loading.value = true
        try {
            val resp = networkCall { api.getMyOrders(offset = 0, limit = pageSize) }
                ?: return appContext.getString(R.string.error_generic_network)
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                return msg
            }
            val body = resp.body() ?: return null
            _orders.value = body.data
            // Backend PagedData wrapper exposes total under `total`; expose it as
            // totalRecords in the repo API to keep consumers agnostic of the wire name.
            _totalRecords.value = body.total
            _loaded.value = true
            return null
        } finally {
            _loading.value = false
        }
    }

    /**
     * Append the next page to the cache, if we have not already exhausted
     * [totalRecords]. Silent on failure — the user can trigger another load by
     * scrolling again.
     */
    suspend fun loadNextPage(): String? {
        if (_loadingMore.value) return null
        if (_orders.value.size >= _totalRecords.value) return null // nothing more to load
        _loadingMore.value = true
        try {
            // Silent on background page loads — user can retry by scrolling again.
            val resp = networkCall { api.getMyOrders(offset = _orders.value.size, limit = pageSize) }
                ?: return null
            if (!resp.isSuccessful) return null
            val body = resp.body() ?: return null
            _orders.value = _orders.value + body.data
            _totalRecords.value = body.total
            return null
        } finally {
            _loadingMore.value = false
        }
    }

    /**
     * Fetch a single order's detail. Returns null on failure — snackbar is
     * surfaced automatically.
     */
    suspend fun getById(id: String): OrderDetailDto? {
        val resp = networkCall { api.getById(id) } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Cancel an order. Returns the parsed response (fee rate / refund details)
     * on success, or null on failure — snackbar surfaced automatically.
     *
     * Deviates from the usual fire-and-forget String? pattern because cancel
     * needs to expose both success-vs-failure AND the fee/refund numbers the
     * confirmation screen renders. Null = failure; non-null = success.
     */
    suspend fun cancel(orderId: String, reason: String?): CancelOrderResponse? {
        val resp = networkCall { api.cancel(CancelOrderRequest(orderId = orderId, reason = reason)) }
            ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Wave 3.3 — confirm a Pending recurring-template order. Cash response
     * means the order's already Confirmed + Paid backend-side; the caller
     * should refetch + show success. Card response carries the Stripe
     * PaymentIntent fields the mobile PaymentSheet needs.
     *
     * Null return = failure (snackbar surfaced automatically).
     */
    suspend fun confirmRecurring(orderId: String): ConfirmRecurringOrderResponse? {
        val resp = networkCall {
            api.confirmRecurring(ConfirmRecurringOrderRequest(orderId = orderId))
        } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Submit (or update) a review on a completed order. Returns the persisted
     * review on success, or null on failure — snackbar surfaced automatically.
     * Same null-vs-non-null convention as [cancel].
     */
    suspend fun submitReview(orderId: String, rating: Int, comment: String?): OrderReviewDto? {
        val resp = networkCall {
            api.submitReview(SubmitReviewRequest(orderId = orderId, rating = rating, comment = comment))
        } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Download the receipt PDF and save it to
     * `{context.cacheDir}/receipts/{orderId}.pdf`. Returns the saved File on
     * success, null on failure — snackbar surfaced automatically. Creates the
     * `receipts/` subdirectory on first use.
     *
     * Uses streaming copy so we don't have to load the full PDF into memory.
     */
    suspend fun downloadReceipt(orderId: String): File? {
        val resp = networkCall { api.downloadReceipt(orderId) } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        val body = resp.body() ?: return null
        return try {
            val dir = File(appContext.cacheDir, "receipts").apply { mkdirs() }
            val file = File(dir, "$orderId.pdf")
            body.byteStream().use { input ->
                file.outputStream().use { output -> input.copyTo(output) }
            }
            file
        } catch (ce: CancellationException) {
            throw ce
        } catch (t: Throwable) {
            snackbar.showError(appContext.getString(R.string.error_generic_network))
            null
        }
    }

    /**
     * Fetch the before/after photos for an order. Returns null on failure —
     * snackbar surfaced automatically. Fetcher pattern — mirrors [getById].
     */
    suspend fun getPhotos(orderId: String): OrderPhotosResponse? {
        val resp = networkCall { api.getPhotos(orderId) } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Cleaners the calling user has had a Completed order with. Drives the
     * "request your favorite cleaner" picker on the booking flow. Silent on
     * failure — the picker just shows an empty state and the booking proceeds
     * without a preference (backend matching algorithm picks normally).
     */
    suspend fun getMyServingCleaners(): List<ServingCleanerDto> {
        val resp = networkCall { api.getMyServingCleaners() } ?: return emptyList()
        return if (resp.isSuccessful) resp.body().orEmpty() else emptyList()
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    override suspend fun clear() {
        _orders.value = emptyList()
        _totalRecords.value = 0
        _loaded.value = false
    }
}
