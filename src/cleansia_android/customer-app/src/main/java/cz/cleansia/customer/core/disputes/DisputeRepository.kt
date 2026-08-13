package cz.cleansia.customer.core.disputes
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.AuthAuthenticator

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.network.requiredBody
import cz.cleansia.core.network.wireResult
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
 * Cache + orchestrator for the signed-in user's disputes. `@Singleton`, so it lives for the process.
 *
 * **Cleared on sign-out / account-delete** — without that the next account on a shared handset inherits
 * this one's data. Errors carry the parsed message for the ViewModel's snackbar; a network failure stays
 * silent because the interceptor owns that toast.
 * -> /mobile-app/patterns#session-wipe
 */
@Singleton
class DisputeRepository @Inject constructor(
    private val api: DisputeApi,
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
     * Rows the server has SENT so far — the offset of the next page and the term the stop condition
     * compares against [totalRecords]. Not `disputes.size`: the page mapper drops an unidentifiable
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
    suspend fun refresh(): ApiResult<Unit> {
        if (_loading.value) return ApiResult.Success(Unit)
        _loading.value = true
        try {
            val resp = networkCall { api.getPaged(offset = 0, limit = pageSize) } ?: return networkError()
            if (!resp.isSuccessful) {
                return httpError(resp.errorBody(), resp.code())
            }
            val body = resp.requiredBody()
            _disputes.value = body.data
            receivedSoFar = body.receivedCount
            exhausted = body.receivedCount == 0
            _totalRecords.value = body.total
            _loaded.value = true
            return ApiResult.Success(Unit)
        } finally {
            _loading.value = false
        }
    }

    /**
     * Append the next page to the cache, if we have not already exhausted
     * [totalRecords]. Silent on failure — the consuming VM maps the Error to a
     * no-op, so scrolling again retries.
     */
    suspend fun loadNextPage(): ApiResult<Unit> {
        if (_loadingMore.value) return ApiResult.Success(Unit)
        if (exhausted || receivedSoFar >= _totalRecords.value) return ApiResult.Success(Unit)
        _loadingMore.value = true
        try {
            val resp = networkCall { api.getPaged(offset = receivedSoFar, limit = pageSize) }
                ?: return networkError()
            if (!resp.isSuccessful) {
                return httpError(resp.errorBody(), resp.code())
            }
            val body = resp.requiredBody()
            _disputes.value = _disputes.value + body.data
            receivedSoFar += body.receivedCount
            exhausted = body.receivedCount == 0
            _totalRecords.value = body.total
            return ApiResult.Success(Unit)
        } finally {
            _loadingMore.value = false
        }
    }

    /** Fetch a single dispute's details (including messages + evidence). */
    suspend fun getById(id: String): ApiResult<DisputeDetailsDto> {
        val resp = networkCall { api.getById(id) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * Create a new dispute against an order. Returns the new dispute's id on
     * success.
     *
     * Frontend should validate `description.length in 10..2000` before calling.
     */
    suspend fun create(orderId: String, reason: Int, description: String): ApiResult<String> {
        val resp = networkCall {
            api.create(CreateDisputeRequest(orderId = orderId, reason = reason, description = description))
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    /**
     * Post a reply on an existing dispute. The calling VM should follow up with
     * [getById] to pick up the persisted message.
     */
    suspend fun addMessage(disputeId: String, content: String): ApiResult<Unit> {
        val resp = networkCall {
            api.addMessage(AddDisputeMessageRequest(disputeId = disputeId, message = content))
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(Unit)
    }

    /**
     * Upload a single evidence file (image or PDF, max 10MB) for an existing
     * dispute. Returns the persisted evidence DTO on success. The caller
     * (DisputeDetailViewModel) is expected to follow up with [getById] to
     * refresh the dispute thread.
     *
     * Size cap is enforced server-side too, but pre-check on the caller side
     * avoids burning a network round-trip on a doomed request.
     */
    suspend fun uploadEvidence(
        disputeId: String,
        fileBytes: ByteArray,
        fileName: String,
        mimeType: String,
    ): ApiResult<UploadDisputeEvidenceResponse> {
        val disputeIdPart = disputeId.toRequestBody("text/plain".toMediaTypeOrNull())
        val fileBody: RequestBody = fileBytes.toRequestBody(mimeType.toMediaTypeOrNull())
        val filePart = MultipartBody.Part.createFormData("file", fileName, fileBody)
        val resp = networkCall { api.uploadEvidence(disputeId = disputeIdPart, file = filePart) }
            ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return ApiResult.Success(resp.requiredBody())
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))


    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Nothing> {
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
        _disputes.value = emptyList()
        _totalRecords.value = 0
        receivedSoFar = 0
        exhausted = false
        _loaded.value = false
    }
}
