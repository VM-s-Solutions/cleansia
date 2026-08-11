package cz.cleansia.core.snackbar

import cz.cleansia.core.R
import cz.cleansia.core.network.ApiError
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

    /**
     * The render path for an [ApiError], and the reason it exists rather than each call site writing
     * `showError(error.getUserMessage())`: a wire-contract violation is built in `:core`, where
     * there is no `Context`, so its `message` can only ever be an English floor. Emitting it as a
     * resource defers the lookup to the render, which is the one place that knows the locale — the
     * property [SnackbarMessage.FromRes] was added for.
     *
     * Every other arm already carries copy the layer that built it localized (the repositories
     * resolve theirs through the app's own parser/translator), so those pass straight through. The
     * diagnostic is never consulted here; it is not for reading.
     */
    fun showError(error: ApiError) = show(error.toErrorSnackbar())
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

/**
 * The decision [SnackbarController.showError] makes, as a value so it can be asserted on directly
 * rather than through a replay-less [SharedFlow].
 *
 * A wire-contract violation is the only [ApiError] built without a `Context` — `:core` has none —
 * so its `message` can only ever be an English floor. Emitting it as a resource defers the lookup to
 * the render, which is the one place that knows the locale. Every other arm already carries copy the
 * layer that built it localized, and passes through untouched: collapsing those into one generic
 * line would delete every backend message the customer needs.
 */
fun ApiError.toErrorSnackbar(): SnackbarMessage =
    if (this is ApiError.Server && diagnostic != null) {
        SnackbarMessage.FromRes(R.string.core_error_server, Severity.Error)
    } else {
        SnackbarMessage.FromString(getUserMessage(), Severity.Error)
    }
