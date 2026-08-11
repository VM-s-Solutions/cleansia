package cz.cleansia.core.network

import android.content.Context

/**
 * Resolves an [ApiError] to copy for a screen, for the call sites that render it themselves rather
 * than pushing it onto the snackbar bus.
 *
 * One rule, stated once, and it is [ApiError.messageRes]: a string `:core` built is a floor in one
 * language, so it is replaced by the resource at render; a string the calling layer built is already
 * in the customer's language and passes through. Collapsing the second case into a generic line
 * would delete every backend message the customer needs — *"This job is no longer available"* must
 * not become *"Something went wrong"*.
 *
 * `SnackbarController.showError` makes the same decision through `SnackbarMessage.FromRes`, which is
 * the better form where it is available because it re-resolves if the locale changes mid-lifetime;
 * this is the form for a value that has to be a `String` right now —
 * [cz.cleansia.core.ui.state.ActionState.Error], whose own contract says *"[message] is
 * pre-localized — call sites render it directly"*.
 *
 * [ApiError.Server.diagnostic] is never read here. It is triage, and it is not for a customer.
 */
fun ApiError.userMessage(context: Context): String =
    messageRes?.let(context::getString) ?: getUserMessage()
