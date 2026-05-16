package cz.cleansia.partner.features.orders.viewmodels

import android.content.Context
import android.util.Log
import cz.cleansia.partner.core.notifications.OrderTimerService
import cz.cleansia.partner.domain.models.orders.OrderDetail
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.format.DateTimeFormatter
import java.time.format.DateTimeParseException

class OrderTimerManager(
    private val appContext: Context,
    private val scope: CoroutineScope,
    private val onElapsedUpdate: (Long) -> Unit,
    private val onTimerStateChange: (Boolean, Instant?) -> Unit
) {
    private var timerJob: Job? = null

    fun parseStartedAt(startedAt: String?): Instant? {
        if (startedAt.isNullOrBlank()) return null

        return try {
            Instant.parse(startedAt)
        } catch (e: DateTimeParseException) {
            try {
                java.time.LocalDateTime.parse(startedAt, DateTimeFormatter.ISO_LOCAL_DATE_TIME)
                    .atZone(java.time.ZoneId.systemDefault())
                    .toInstant()
            } catch (e: DateTimeParseException) {
                null
            }
        }
    }

    fun startTimer(startedAt: Instant, order: OrderDetail?) {
        timerJob?.cancel()
        timerJob = null

        timerJob = scope.launch {
            onTimerStateChange(true, startedAt)

            Log.d("OrderDetailsVM", "startTimer: order=${order?.orderNumber} est=${order?.estimatedTime} startedAt=$startedAt")
            if (order != null) {
                val estimatedMinutes = order.estimatedTime ?: 0
                if (estimatedMinutes > 0) {
                    OrderTimerService.start(
                        context = appContext,
                        orderNumber = order.orderNumber,
                        estimatedMinutes = estimatedMinutes,
                        startedAtMillis = startedAt.toEpochMilli()
                    )
                } else {
                    Log.w("OrderDetailsVM", "startTimer: estimatedMinutes is 0, skipping service start")
                }
            } else {
                Log.w("OrderDetailsVM", "startTimer: order is null, skipping service start")
            }

            while (isActive) {
                val now = Instant.now()
                val elapsedSeconds = java.time.Duration.between(startedAt, now).seconds

                onElapsedUpdate(elapsedSeconds)

                delay(1000)
            }
        }
    }

    fun stopTimer() {
        timerJob?.cancel()
        timerJob = null
        onTimerStateChange(false, null)
        OrderTimerService.stop(appContext)
    }

    fun startTimerManually(order: OrderDetail?) {
        val now = Instant.now()
        startTimer(now, order)
    }

    fun cancel() {
        timerJob?.cancel()
        timerJob = null
    }
}
