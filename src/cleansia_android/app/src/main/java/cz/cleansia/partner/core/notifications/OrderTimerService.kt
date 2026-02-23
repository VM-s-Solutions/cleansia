package cz.cleansia.partner.core.notifications

import android.app.Notification
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.app.NotificationChannel
import android.os.IBinder
import android.os.SystemClock
import android.util.Log
import android.widget.RemoteViews
import androidx.core.app.NotificationCompat
import cz.cleansia.partner.MainActivity
import cz.cleansia.partner.R
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class OrderTimerService : Service() {

    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var timerJob: Job? = null

    private var orderNumber: String = ""
    private var estimatedMinutes: Int = 0
    private var startedAtMillis: Long = 0L

    private var hasSentCautionAlert = false
    private var hasSentUrgentAlert = false
    private var hasSentOvertimeAlert = false

    companion object {
        private const val TAG = "OrderTimerService"
        private const val ACTION_START = "START"
        private const val ACTION_STOP = "STOP"
        private const val EXTRA_ORDER_NUMBER = "order_number"
        private const val EXTRA_ESTIMATED_MINUTES = "estimated_minutes"
        private const val EXTRA_STARTED_AT_MILLIS = "started_at_millis"

        // Text colors
        private const val COLOR_TEXT_PRIMARY = 0xFFECEFF1.toInt()
        private const val COLOR_TEXT_SECONDARY = 0xFF90A4AE.toInt()

        // Status accent colors
        private const val COLOR_PLENTY = 0xFF4DB6AC.toInt()    // Teal
        private const val COLOR_CAUTION = 0xFFFFB74D.toInt()   // Amber
        private const val COLOR_URGENT = 0xFFEF5350.toInt()    // Red
        private const val COLOR_OVERTIME = 0xFFEF5350.toInt()  // Red

        fun start(
            context: Context,
            orderNumber: String,
            estimatedMinutes: Int,
            startedAtMillis: Long
        ) {
            Log.d(TAG, "start() called: order=$orderNumber est=$estimatedMinutes startedAt=$startedAtMillis")
            try {
                // CRITICAL: Create notification channel BEFORE starting the service
                // On Samsung devices, the channel must exist before startForegroundService()
                // to ensure the notification is displayed on first launch
                ensureNotificationChannel(context)

                val intent = Intent(context, OrderTimerService::class.java).apply {
                    action = ACTION_START
                    putExtra(EXTRA_ORDER_NUMBER, orderNumber)
                    putExtra(EXTRA_ESTIMATED_MINUTES, estimatedMinutes)
                    putExtra(EXTRA_STARTED_AT_MILLIS, startedAtMillis)
                }
                Log.d(TAG, "start() calling startForegroundService...")
                context.startForegroundService(intent)
                Log.d(TAG, "start() startForegroundService returned successfully")
            } catch (e: Exception) {
                Log.e(TAG, "Failed to start foreground service", e)
            }
        }

        /**
         * Ensures the notification channel exists before starting the service.
         * This is critical for Samsung devices where the channel must be registered
         * before startForegroundService() is called for the notification to appear.
         */
        private fun ensureNotificationChannel(context: Context) {
            val nm = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

            // Check if channel exists
            val existing = nm.getNotificationChannel(NotificationHelper.CHANNEL_ORDER_TIMER)
            if (existing != null) {
                Log.d(TAG, "ensureNotificationChannel: channel already exists with importance=${existing.importance}")
                // If channel exists with lower importance, delete and recreate
                if (existing.importance < NotificationManager.IMPORTANCE_DEFAULT) {
                    Log.d(TAG, "ensureNotificationChannel: upgrading channel importance")
                    nm.deleteNotificationChannel(NotificationHelper.CHANNEL_ORDER_TIMER)
                } else {
                    return // Channel exists with correct importance
                }
            }

            Log.d(TAG, "ensureNotificationChannel: creating channel")
            nm.createNotificationChannel(
                NotificationChannel(
                    NotificationHelper.CHANNEL_ORDER_TIMER,
                    context.getString(R.string.notification_channel_timer),
                    NotificationManager.IMPORTANCE_DEFAULT
                ).apply {
                    description = context.getString(R.string.notification_channel_timer_desc)
                    setShowBadge(false)
                    setSound(null, null)
                }
            )
            Log.d(TAG, "ensureNotificationChannel: channel created successfully")
        }

        fun stop(context: Context) {
            Log.d(TAG, "stop() called")
            try {
                val intent = Intent(context, OrderTimerService::class.java).apply {
                    action = ACTION_STOP
                }
                context.startService(intent)
            } catch (_: Exception) { }
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        Log.d(TAG, "onCreate called")

        val nm = getSystemService(NotificationManager::class.java)

        // Log channel state for debugging
        val channel = nm.getNotificationChannel(NotificationHelper.CHANNEL_ORDER_TIMER)
        Log.d(TAG, "onCreate: channel exists=${channel != null}, importance=${channel?.importance}")

        // Double-check channel exists (should already be created in start())
        if (channel == null) {
            Log.w(TAG, "onCreate: channel was null, creating defensively")
            nm.createNotificationChannel(
                NotificationChannel(
                    NotificationHelper.CHANNEL_ORDER_TIMER,
                    getString(R.string.notification_channel_timer),
                    NotificationManager.IMPORTANCE_DEFAULT
                ).apply {
                    setShowBadge(false)
                    setSound(null, null)
                }
            )
        }

        val placeholder = NotificationCompat.Builder(this, NotificationHelper.CHANNEL_ORDER_TIMER)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(getString(R.string.notification_channel_timer))
            .setSilent(true)
            .setOngoing(true)
            .build()

        startForeground(NotificationHelper.NOTIFICATION_ID_TIMER, placeholder)
        Log.d(TAG, "startForeground called with placeholder notification")
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> {
                // Cancel any existing timer (but don't stop the service)
                timerJob?.cancel()
                timerJob = null

                orderNumber = intent.getStringExtra(EXTRA_ORDER_NUMBER) ?: ""
                estimatedMinutes = intent.getIntExtra(EXTRA_ESTIMATED_MINUTES, 0)
                startedAtMillis = intent.getLongExtra(EXTRA_STARTED_AT_MILLIS, 0L)
                Log.d(TAG, "START order=$orderNumber est=${estimatedMinutes}min")
                hasSentCautionAlert = false
                hasSentUrgentAlert = false
                hasSentOvertimeAlert = false
                startTimerLoop()
            }
            ACTION_STOP -> {
                Log.d(TAG, "STOP")
                stopTimerAndService()
            }
        }
        return START_NOT_STICKY
    }

    // ── Status helpers ──────────────────────────────────────────────────

    private enum class TimerStatus { PLENTY, CAUTION, URGENT, OVERTIME }

    private fun timerStatus(pctRemaining: Float, overtime: Boolean) = when {
        overtime -> TimerStatus.OVERTIME
        pctRemaining > 0.70f -> TimerStatus.PLENTY
        pctRemaining > 0.30f -> TimerStatus.CAUTION
        else -> TimerStatus.URGENT
    }

    private fun accentColor(status: TimerStatus) = when (status) {
        TimerStatus.PLENTY -> COLOR_PLENTY
        TimerStatus.CAUTION -> COLOR_CAUTION
        TimerStatus.URGENT -> COLOR_URGENT
        TimerStatus.OVERTIME -> COLOR_OVERTIME
    }

    private fun statusLabel(status: TimerStatus) = when (status) {
        TimerStatus.OVERTIME -> getString(R.string.notification_overtime_title)
        TimerStatus.URGENT -> getString(R.string.notification_urgent_title)
        TimerStatus.CAUTION -> getString(R.string.notification_caution_title)
        TimerStatus.PLENTY -> getString(R.string.notification_in_progress)
    }

    // ── Chronometer setup ───────────────────────────────────────────────

    private fun setupChronometer(
        rv: RemoteViews,
        elapsedSec: Long,
        remainingSec: Long,
        overtime: Boolean
    ) {
        if (overtime) {
            val otSec = elapsedSec - estimatedMinutes * 60L
            val base = SystemClock.elapsedRealtime() - otSec * 1000
            rv.setChronometer(R.id.notification_chronometer, base, "-%s", true)
            rv.setTextColor(R.id.notification_chronometer, COLOR_OVERTIME)
        } else {
            val base = SystemClock.elapsedRealtime() + remainingSec * 1000
            rv.setChronometer(R.id.notification_chronometer, base, "%s", true)
            rv.setTextColor(R.id.notification_chronometer, COLOR_TEXT_PRIMARY)
            rv.setChronometerCountDown(R.id.notification_chronometer, true)
        }
    }

    // ── Build notification ──────────────────────────────────────────────

    private fun buildNotification(elapsedSec: Long): Notification {
        val totalSec = estimatedMinutes * 60L
        val remainingSec = totalSec - elapsedSec
        val overtime = remainingSec < 0
        val elapsedMin = (elapsedSec / 60).toInt()
        val elapsedS = (elapsedSec % 60).toInt()

        val pctRemaining = if (totalSec > 0)
            (remainingSec.toFloat() / totalSec).coerceIn(0f, 1f) else 0f
        val fraction = if (totalSec > 0)
            (elapsedSec.toFloat() / totalSec).coerceIn(0f, 1f) else 0f

        val status = timerStatus(pctRemaining, overtime)
        val accent = accentColor(status)
        val remainingMin = (remainingSec / 60).toInt()

        // Subtitle text
        val subtitle = when (status) {
            TimerStatus.OVERTIME -> getString(R.string.notification_overtime, elapsedMin - estimatedMinutes)
            TimerStatus.URGENT -> getString(R.string.notification_urgent_subtitle, remainingMin.coerceAtLeast(0))
            TimerStatus.CAUTION -> getString(R.string.notification_caution_subtitle, remainingMin)
            TimerStatus.PLENTY -> getString(R.string.notification_in_progress)
        }

        // ── Collapsed ───────────────────────────────────────────────────
        val collapsed = RemoteViews(packageName, R.layout.notification_timer)
        collapsed.setTextViewText(
            R.id.notification_title,
            getString(R.string.notification_order_timer, orderNumber)
        )
        collapsed.setTextViewText(R.id.notification_subtitle, subtitle)
        collapsed.setTextColor(
            R.id.notification_subtitle,
            if (overtime) COLOR_OVERTIME else COLOR_TEXT_SECONDARY
        )
        setupChronometer(collapsed, elapsedSec, remainingSec, overtime)

        // ── Expanded ────────────────────────────────────────────────────
        val expanded = RemoteViews(packageName, R.layout.notification_timer_expanded)
        expanded.setTextViewText(
            R.id.notification_title,
            getString(R.string.notification_order_timer, orderNumber)
        )
        expanded.setTextViewText(R.id.notification_subtitle, subtitle)
        expanded.setTextColor(
            R.id.notification_subtitle,
            if (overtime) COLOR_OVERTIME else COLOR_TEXT_SECONDARY
        )

        // Status label (accent-colored)
        expanded.setTextViewText(R.id.notification_status, statusLabel(status))
        expanded.setTextColor(R.id.notification_status, accent)

        // Chronometer
        setupChronometer(expanded, elapsedSec, remainingSec, overtime)

        // Progress bar (native ProgressBar widget — Android handles updates reliably)
        val progressPerMille = (fraction * 1000).toInt().coerceIn(0, 1000)
        expanded.setProgressBar(R.id.notification_progress, 1000, progressPerMille, false)

        // Elapsed / estimated labels
        expanded.setTextViewText(
            R.id.notification_elapsed_label,
            getString(R.string.notification_elapsed_detailed, elapsedMin, elapsedS)
        )
        expanded.setTextViewText(
            R.id.notification_estimated_label,
            getString(R.string.notification_estimated, estimatedMinutes)
        )

        // Tap intent
        val tapIntent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP
            data = android.net.Uri.parse("cleansia://partner/orders")
        }
        val pendingIntent = PendingIntent.getActivity(
            this, 0, tapIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, NotificationHelper.CHANNEL_ORDER_TIMER)
            .setSmallIcon(R.drawable.ic_notification)
            .setCustomContentView(collapsed)
            .setCustomBigContentView(expanded)
            .setStyle(NotificationCompat.DecoratedCustomViewStyle())
            .setColor(accent)
            .setOngoing(true)
            .setSilent(true)
            .setContentIntent(pendingIntent)
            .setCategory(NotificationCompat.CATEGORY_PROGRESS)
            .setWhen(System.currentTimeMillis())
            .setShowWhen(false)
            .build()
    }

    // ── Timer loop ──────────────────────────────────────────────────────

    private fun startTimerLoop() {
        Log.d(TAG, "startTimerLoop() called")
        timerJob?.cancel()
        timerJob = serviceScope.launch {
            Log.d(TAG, "Timer coroutine started")
            while (true) {
                try {
                    val elapsedMs = System.currentTimeMillis() - startedAtMillis
                    val elapsedSec = elapsedMs / 1000
                    val elapsedMin = (elapsedMs / 60_000).toInt()
                    val overtime = elapsedMin >= estimatedMinutes

                    if (elapsedSec % 10 == 0L) {
                        Log.d(TAG, "Timer tick: ${elapsedSec}s elapsed")
                    }

                    val notification = buildNotification(elapsedSec)
                    val nm = getSystemService(NotificationManager::class.java)
                    nm.notify(NotificationHelper.NOTIFICATION_ID_TIMER, notification)

                    // Milestone alerts
                    if (estimatedMinutes > 0) {
                        val frac = elapsedMin.toFloat() / estimatedMinutes

                        if (frac >= 0.7f && !hasSentCautionAlert) {
                            hasSentCautionAlert = true
                            showAlert(
                                NotificationHelper.NOTIFICATION_ID_CAUTION,
                                getString(R.string.notification_caution_title),
                                getString(R.string.notification_caution_message, orderNumber, estimatedMinutes - elapsedMin)
                            )
                        }
                        if (frac >= 0.9f && !hasSentUrgentAlert) {
                            hasSentUrgentAlert = true
                            showAlert(
                                NotificationHelper.NOTIFICATION_ID_URGENT,
                                getString(R.string.notification_urgent_title),
                                getString(R.string.notification_urgent_message, orderNumber, estimatedMinutes - elapsedMin)
                            )
                        }
                        if (overtime && !hasSentOvertimeAlert) {
                            hasSentOvertimeAlert = true
                            showAlert(
                                NotificationHelper.NOTIFICATION_ID_OVERTIME,
                                getString(R.string.notification_overtime_title),
                                getString(R.string.notification_overtime_message, orderNumber)
                            )
                        }
                    }
                } catch (e: Exception) {
                    Log.e(TAG, "Error in timer loop", e)
                }

                delay(1_000)
            }
        }
    }

    private fun showAlert(id: Int, title: String, message: String) {
        val intent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP
            data = android.net.Uri.parse("cleansia://partner/orders")
        }
        val pi = PendingIntent.getActivity(
            this, id, intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        val n = NotificationCompat.Builder(this, NotificationHelper.CHANNEL_ORDER_ALERTS)
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(title)
            .setContentText(message)
            .setAutoCancel(true)
            .setContentIntent(pi)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
        getSystemService(NotificationManager::class.java).notify(id, n)
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    private fun stopTimerAndService() {
        timerJob?.cancel()
        timerJob = null
        val nm = getSystemService(NotificationManager::class.java)
        nm.cancel(NotificationHelper.NOTIFICATION_ID_TIMER)
        nm.cancel(NotificationHelper.NOTIFICATION_ID_CAUTION)
        nm.cancel(NotificationHelper.NOTIFICATION_ID_URGENT)
        nm.cancel(NotificationHelper.NOTIFICATION_ID_OVERTIME)
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    override fun onDestroy() {
        super.onDestroy()
        serviceScope.cancel()
    }
}
