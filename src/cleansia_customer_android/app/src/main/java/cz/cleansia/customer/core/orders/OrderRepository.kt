package cz.cleansia.customer.core.orders

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.ui.snackbar.SnackbarController
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import javax.inject.Inject
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
) {
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
            val resp = try {
                api.getMyOrders(offset = 0, limit = pageSize)
            } catch (t: Throwable) {
                val msg = appContext.getString(R.string.error_generic_network)
                snackbar.showError(msg)
                return msg
            }
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
            val resp = try {
                api.getMyOrders(offset = _orders.value.size, limit = pageSize)
            } catch (t: Throwable) {
                // Silent on background page loads — user can retry by scrolling again.
                return null
            }
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
        return try {
            val resp = api.getById(id)
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                null
            } else {
                resp.body()
            }
        } catch (t: Throwable) {
            snackbar.showError(appContext.getString(R.string.error_generic_network))
            null
        }
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
        return try {
            val resp = api.cancel(CancelOrderRequest(orderId = orderId, reason = reason))
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                null
            } else {
                resp.body()
            }
        } catch (t: Throwable) {
            snackbar.showError(appContext.getString(R.string.error_generic_network))
            null
        }
    }

    /**
     * Submit (or update) a review on a completed order. Returns the persisted
     * review on success, or null on failure — snackbar surfaced automatically.
     * Same null-vs-non-null convention as [cancel].
     */
    suspend fun submitReview(orderId: String, rating: Int, comment: String?): OrderReviewDto? {
        return try {
            val resp = api.submitReview(
                SubmitReviewRequest(orderId = orderId, rating = rating, comment = comment),
            )
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                null
            } else {
                resp.body()
            }
        } catch (t: Throwable) {
            snackbar.showError(appContext.getString(R.string.error_generic_network))
            null
        }
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
        return try {
            val resp = api.downloadReceipt(orderId)
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                return null
            }
            val body = resp.body() ?: return null
            val dir = File(appContext.cacheDir, "receipts").apply { mkdirs() }
            val file = File(dir, "$orderId.pdf")
            body.byteStream().use { input ->
                file.outputStream().use { output -> input.copyTo(output) }
            }
            file
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
        return try {
            val resp = api.getPhotos(orderId)
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                null
            } else {
                resp.body()
            }
        } catch (t: Throwable) {
            snackbar.showError(appContext.getString(R.string.error_generic_network))
            null
        }
    }

    /**
     * Cleaners the calling user has had a Completed order with. Drives the
     * "request your favorite cleaner" picker on the booking flow. Silent on
     * failure — the picker just shows an empty state and the booking proceeds
     * without a preference (backend matching algorithm picks normally).
     */
    suspend fun getMyServingCleaners(): List<ServingCleanerDto> {
        return try {
            val resp = api.getMyServingCleaners()
            if (resp.isSuccessful) resp.body().orEmpty() else emptyList()
        } catch (t: Throwable) {
            emptyList()
        }
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    suspend fun clear() {
        _orders.value = emptyList()
        _totalRecords.value = 0
        _loaded.value = false
    }
}
