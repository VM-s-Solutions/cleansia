package cz.cleansia.core.snackbar

import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

/**
 * App-wide snackbar bus. Any VM, repository, or interceptor can push messages;
 * the [GlobalSnackbarHost] composable at the root of the nav tree renders them.
 *
 * Buffer: 3 messages. Overflow drops oldest so a burst of errors never pins
 * the UI to a stale one.
 */
@Singleton
class SnackbarController @Inject constructor() {

    private val _messages = MutableSharedFlow<SnackbarMessage>(
        replay = 0,
        extraBufferCapacity = 3,
        onBufferOverflow = BufferOverflow.DROP_OLDEST,
    )
    val messages: SharedFlow<SnackbarMessage> = _messages.asSharedFlow()

    fun show(message: SnackbarMessage) {
        _messages.tryEmit(message)
    }

    // Convenience wrappers — both raw-text and i18n-key variants.

    fun showError(text: String) = show(SnackbarMessage.FromString(text, Severity.Error))
    fun showErrorKey(key: Int) = show(SnackbarMessage.FromRes(key, Severity.Error))

    fun showSuccess(text: String) = show(SnackbarMessage.FromString(text, Severity.Success))
    fun showSuccessKey(key: Int) = show(SnackbarMessage.FromRes(key, Severity.Success))

    fun showInfo(text: String) = show(SnackbarMessage.FromString(text, Severity.Info))
    fun showInfoKey(key: Int) = show(SnackbarMessage.FromRes(key, Severity.Info))

    fun showWarning(text: String) = show(SnackbarMessage.FromString(text, Severity.Warning))
    fun showWarningKey(key: Int) = show(SnackbarMessage.FromRes(key, Severity.Warning))
}

/**
 * Raw vs resource-based messages. Resource-based preserves i18n correctness
 * when the device locale changes mid-lifetime (message resolves at render time).
 */
sealed class SnackbarMessage {
    abstract val severity: Severity

    data class FromString(val text: String, override val severity: Severity) : SnackbarMessage()

    data class FromRes(
        val stringRes: Int,
        override val severity: Severity,
    ) : SnackbarMessage()
}

enum class Severity { Error, Success, Info, Warning }
