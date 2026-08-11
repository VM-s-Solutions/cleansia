package cz.cleansia.core.network

import android.content.Context
import cz.cleansia.core.R

/**
 * Resolves an [ApiError] to copy for a screen, for the call sites that render it themselves rather
 * than pushing it onto the snackbar bus.
 *
 * One rule, stated once: a **wire-contract violation is the only arm built without a `Context`**, so
 * `ApiError.Server.message` on that arm can only ever be the English floor from `:core`. Everything
 * else already carries copy the layer that built it localized. `SnackbarController.showError` makes
 * the same decision through [SnackbarMessage.FromRes], which is the better form where it is
 * available because it re-resolves if the locale changes mid-lifetime; this is the form for a value
 * that has to be a `String` right now — [cz.cleansia.core.ui.state.ActionState.Error], whose own
 * contract says *"[message] is pre-localized — call sites render it directly"*.
 *
 * [ApiError.Server.diagnostic] is never read here. It is triage, and it is not for a customer.
 */
fun ApiError.userMessage(context: Context): String =
    if (this is ApiError.Server && diagnostic != null) {
        context.getString(R.string.core_error_server)
    } else {
        getUserMessage()
    }
