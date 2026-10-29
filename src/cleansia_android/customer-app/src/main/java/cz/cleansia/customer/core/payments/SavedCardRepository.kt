package cz.cleansia.customer.core.payments

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.safeApiCall
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.job
import kotlinx.coroutines.CancellationException
import kotlinx.serialization.json.Json

data class CardCaptureMarket(val countryId: String?, val currencyCode: String?)

sealed interface SavedCardCapture {
    val attempt: Long
    data class Starting(override val attempt: Long) : SavedCardCapture
    data class Sheet(override val attempt: Long, val setup: SavedCardSetup) : SavedCardCapture
    data class Pending(override val attempt: Long, val savedCardId: String, val verifying: Boolean = false) : SavedCardCapture
}

/** [cards] is null until the first read answers; a capture Stripe has not confirmed yet is not in it. */
@Singleton
class SavedCardRepository @Inject constructor(
    private val api: SavedCardApi,
    private val json: Json,
) : SessionScopedCache {

    private val _cards = MutableStateFlow<List<SavedCard>?>(null)
    val cards: StateFlow<List<SavedCard>?> = _cards.asStateFlow()
    private val _captures = MutableStateFlow<Map<CardCaptureMarket, SavedCardCapture>>(emptyMap())
    val captures: StateFlow<Map<CardCaptureMarket, SavedCardCapture>> = _captures.asStateFlow()
    private val verificationJobs = mutableMapOf<CardCaptureMarket, Job>()
    private val captureLock = Any()
    private var sessionGeneration = 0L
    private var nextAttempt = 0L
    val sessionEpoch: Long get() = synchronized(captureLock) { sessionGeneration }

    suspend fun refresh(): ApiResult<List<SavedCard>> = refresh(synchronized(captureLock) { sessionGeneration })

    private suspend fun refresh(generation: Long): ApiResult<List<SavedCard>> {
        val result = safeApiCall(json) { api.getMine() }
        return synchronized(captureLock) {
            if (generation != sessionGeneration) staleSession()
            else result.onSuccess { _cards.value = it }
        }
    }

    suspend fun startCapture(consentAccepted: Boolean, countryId: String?): ApiResult<SavedCardSetup> =
        safeApiCall(json) { api.createSetupIntent(consentAccepted, countryId) }

    suspend fun beginCapture(market: CardCaptureMarket, consentAccepted: Boolean, expectedSessionEpoch: Long = sessionEpoch): ApiResult<SavedCardSetup>? {
        val (attempt, generation) = synchronized(captureLock) {
            if (expectedSessionEpoch != sessionGeneration || !consentAccepted || _captures.value.containsKey(market)) return null
            val attempt = ++nextAttempt
            setCaptureLocked(market, SavedCardCapture.Starting(attempt))
            attempt to sessionGeneration
        }
        try {
            val result = startCapture(consentAccepted, market.countryId)
            return synchronized(captureLock) {
                if (generation != sessionGeneration || _captures.value[market]?.attempt != attempt) return null
                when (result) {
                    is ApiResult.Success -> setCaptureLocked(market, SavedCardCapture.Sheet(attempt, result.data))
                    is ApiResult.Error -> setCaptureLocked(market, null)
                }
                result
            }
        } catch (cancelled: CancellationException) {
            synchronized(captureLock) {
                if (_captures.value[market]?.attempt == attempt) setCaptureLocked(market, null)
            }
            throw cancelled
        }
    }

    fun sheetCompleted(market: CardCaptureMarket, attempt: Long): Boolean = synchronized(captureLock) {
        val current = _captures.value[market] as? SavedCardCapture.Sheet ?: return false
        if (current.attempt != attempt) return false
        setCaptureLocked(market, SavedCardCapture.Pending(attempt, current.setup.savedCardId))
        true
    }

    fun setupForPresentation(market: CardCaptureMarket, attempt: Long, expectedSessionEpoch: Long): SavedCardSetup? = synchronized(captureLock) {
        if (expectedSessionEpoch != sessionGeneration) return null
        val sheet = _captures.value[market] as? SavedCardCapture.Sheet ?: return null
        sheet.setup.takeIf { sheet.attempt == attempt }
    }

    fun abandonCapture(market: CardCaptureMarket, attempt: Long) {
        val job = synchronized(captureLock) {
            if (_captures.value[market]?.attempt != attempt) return
            val job = verificationJobs.remove(market)
            setCaptureLocked(market, null)
            job
        }
        job?.cancel()
    }

    fun pauseVerification(market: CardCaptureMarket) {
        val job = synchronized(captureLock) { verificationJobs[market] }
        job?.cancel()
    }

    /** A completed sheet is only pending until the webhook-backed list contains this exact identity. */
    suspend fun verifyCapture(market: CardCaptureMarket, reads: Int = 8, expectedSessionEpoch: Long = sessionEpoch): ApiResult<Boolean>? {
        val job = currentCoroutineContext().job
        val (pending, generation) = synchronized(captureLock) {
            if (expectedSessionEpoch != sessionGeneration) return null
            val pending = _captures.value[market] as? SavedCardCapture.Pending ?: return null
            if (verificationJobs[market]?.isActive == true) return null
            verificationJobs[market] = job
            setCaptureLocked(market, pending.copy(verifying = true))
            pending to sessionGeneration
        }
        try {
            repeat(reads) { index ->
                if (index > 0) delay(1500)
                val result = refresh(generation)
                synchronized(captureLock) {
                    if (generation != sessionGeneration || _captures.value[market]?.attempt != pending.attempt) return null
                    when (result) {
                        is ApiResult.Error -> return result
                        is ApiResult.Success -> if (result.data.any { it.id == pending.savedCardId && (market.currencyCode == null || it.currencyCode.equals(market.currencyCode, true)) }) {
                            setCaptureLocked(market, null)
                            return ApiResult.Success(true)
                        }
                    }
                }
            }
            return ApiResult.Success(false)
        } finally {
            synchronized(captureLock) {
                if (verificationJobs[market] === job) {
                    verificationJobs.remove(market)
                    val current = _captures.value[market] as? SavedCardCapture.Pending
                    if (current?.attempt == pending.attempt) setCaptureLocked(market, current.copy(verifying = false))
                }
            }
        }
    }

    private fun setCaptureLocked(market: CardCaptureMarket, capture: SavedCardCapture?) {
        _captures.value = if (capture == null) _captures.value - market else _captures.value + (market to capture)
    }

    suspend fun remove(savedCardId: String): ApiResult<Unit> {
        val generation = synchronized(captureLock) { sessionGeneration }
        val result = safeApiCall(json) { api.remove(savedCardId) }.map { }
        return synchronized(captureLock) {
            if (generation != sessionGeneration) staleSession()
            else result.onSuccess { _cards.value = _cards.value?.filterNot { it.id == savedCardId } }
        }
    }

    override suspend fun clear() {
        val jobs = synchronized(captureLock) {
            sessionGeneration++
            val jobs = verificationJobs.values.toList()
            verificationJobs.clear()
            _captures.value = emptyMap()
            _cards.value = null
            jobs
        }
        jobs.forEach { it.cancel() }
    }

    private fun staleSession() = ApiResult.Error(ApiError.Network("Session changed", cz.cleansia.core.R.string.core_error_no_connection))
}
