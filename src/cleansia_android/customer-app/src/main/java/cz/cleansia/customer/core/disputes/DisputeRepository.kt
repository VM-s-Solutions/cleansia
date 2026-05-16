package cz.cleansia.customer.core.disputes
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.AuthAuthenticator

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.toRequestBody

/**
 * Cache + orchestrator for the signed-in user's disputes.
 *
 * Lifetime: `@Singleton` — lives for the app process. Caches the list of
 * disputes ([disputes]) and supports additive pagination via [loadNextPage].
 * Cleared on sign-out / account-delete so the next user doesn't inherit this
 * one's data — call sites are wired in [cz.cleansia.core.auth.AuthAuthenticator],
 * [cz.cleansia.customer.core.auth.AuthRepository], and
 * [cz.cleansia.customer.core.user.UserRepository] alongside the matching
 * OrderRepository / AddressRepository clear() hooks.
 *
 * Error model mirrors [cz.cleansia.customer.core.orders.OrderRepository]:
 *  - Foreground operations ([refresh], [getById], [create], [addMessage])
 *    surface failures via the shared [SnackbarController] and return a
 *    sentinel value indicating failure (null / null / null / false).
 *  - Background page loads ([loadNextPage]) are silent — scrolling again retries.
 *
 * Deliberately does NOT auto-invalidate or refetch on mutations. After a
 * successful [create] or [addMessage], the calling VM is expected to trigger
 * its own [refresh] / [getById] call. This avoids racing against a sheet
 * that's still open and mirrors OrderRepository's mutation behaviour.
 *
 * Dispute details are not cached — each [getById] hits the network. Message
 * threads change frequently (new staff replies), so a stale cache would be
 * more confusing than useful; VMs pull-to-refresh freely.
 */
@Singleton
class DisputeRepository @Inject constructor(
    private val api: DisputeApi,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : cz.cleansia.core.auth.SessionScopedCache {
    private val _disputes = MutableStateFlow<List<DisputeListItemDto>>(emptyList())
    val disputes: StateFlow<List<DisputeListItemDto>> = _disputes.asStateFlow()

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
            val resp = networkCall { api.getPaged(offset = 0, limit = pageSize) }
                ?: return appContext.getString(R.string.error_generic_network)
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                return msg
            }
            val body = resp.body() ?: return null
            _disputes.value = body.data
            _totalRecords.value = body.total
            _loaded.value = true
            return null
        } finally {
            _loading.value = false
        }
    }

    /**
     * Append the next page to the cache, if we have not already exhausted
     * [totalRecords]. Silent on failure — the user can trigger another load
     * by scrolling again.
     */
    suspend fun loadNextPage(): String? {
        if (_loadingMore.value) return null
        if (_disputes.value.size >= _totalRecords.value) return null
        _loadingMore.value = true
        try {
            val resp = networkCall { api.getPaged(offset = _disputes.value.size, limit = pageSize) }
                ?: return null
            if (!resp.isSuccessful) return null
            val body = resp.body() ?: return null
            _disputes.value = _disputes.value + body.data
            _totalRecords.value = body.total
            return null
        } finally {
            _loadingMore.value = false
        }
    }

    /**
     * Fetch a single dispute's details (including messages + evidence).
     * Returns null on failure — snackbar is surfaced automatically.
     */
    suspend fun getById(id: String): DisputeDetailsDto? {
        val resp = networkCall { api.getById(id) } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Create a new dispute against an order. Returns the new dispute's id on
     * success, null on failure — snackbar surfaced automatically.
     *
     * Frontend should validate `description.length in 10..2000` before calling.
     */
    suspend fun create(orderId: String, reason: Int, description: String): String? {
        val resp = networkCall {
            api.create(CreateDisputeRequest(orderId = orderId, reason = reason, description = description))
        } ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /**
     * Post a reply on an existing dispute. Returns true on success, false on
     * failure — snackbar surfaced automatically. The calling VM should follow
     * up with [getById] to pick up the persisted message.
     */
    suspend fun addMessage(disputeId: String, content: String): Boolean {
        val resp = networkCall {
            api.addMessage(AddDisputeMessageRequest(disputeId = disputeId, message = content))
        } ?: return false
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return false
        }
        return true
    }

    /**
     * Upload a single evidence file (image or PDF, max 10MB) for an existing
     * dispute. Returns the persisted evidence DTO on success, null on failure
     * — snackbar surfaced automatically. The caller (DisputeDetailViewModel)
     * is expected to follow up with [getById] to refresh the dispute thread.
     *
     * Size cap is enforced server-side too, but pre-check on the caller side
     * avoids burning a network round-trip on a doomed request.
     */
    suspend fun uploadEvidence(
        disputeId: String,
        fileBytes: ByteArray,
        fileName: String,
        mimeType: String,
    ): UploadDisputeEvidenceResponse? {
        val disputeIdPart = disputeId.toRequestBody("text/plain".toMediaTypeOrNull())
        val fileBody: RequestBody = fileBytes.toRequestBody(mimeType.toMediaTypeOrNull())
        val filePart = MultipartBody.Part.createFormData("file", fileName, fileBody)
        val resp = networkCall { api.uploadEvidence(disputeId = disputeIdPart, file = filePart) }
            ?: return null
        if (!resp.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
            snackbar.showError(msg)
            return null
        }
        return resp.body()
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    override suspend fun clear() {
        _disputes.value = emptyList()
        _totalRecords.value = 0
        _loaded.value = false
    }
}
