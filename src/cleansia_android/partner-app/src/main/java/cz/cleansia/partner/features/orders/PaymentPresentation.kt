package cz.cleansia.partner.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import cz.cleansia.partner.BuildConfig
import cz.cleansia.partner.R

/**
 * How urgently a payment status should read to the cleaner, independent of
 * which colour the theme eventually paints it.
 */
enum class PaymentSeverity { Success, Warning, Error, Neutral }

/**
 * Labels and tints key off the `Code` envelope's numeric value, never its `name`:
 * the name is the backend's English enum text, so matching on it breaks silently
 * when the wording changes and would leak untranslated copy into the UI. An ordinal
 * we don't know yet (a backend enum addition) reads as a neutral placeholder for the
 * cleaner and as the raw ordinal in DEBUG so the gap surfaces to us instead.
 *
 * Ordinals mirror the backend enums (PaymentType 1=Cash 2=Card; PaymentStatus
 * 1=Pending 2=Paid 3=Failed 4=Refunded 5=Disputed 6=PartiallyRefunded) and the
 * iOS twin in CleansiaPartner/Sources/Features/Orders/PaymentPresentation.swift.
 */
object PaymentPresentation {

    @Composable
    fun methodLabel(code: Int?): String = when (code) {
        1 -> stringResource(R.string.payment_method_cash)
        2 -> stringResource(R.string.payment_method_card)
        else -> rawDiagnostic(code)
    }

    @Composable
    fun statusLabel(code: Int?): String = when (code) {
        1 -> stringResource(R.string.payment_status_pending)
        2 -> stringResource(R.string.payment_status_paid)
        3 -> stringResource(R.string.payment_status_failed)
        4 -> stringResource(R.string.payment_status_refunded)
        5 -> stringResource(R.string.payment_status_disputed)
        6 -> stringResource(R.string.payment_status_partially_refunded)
        else -> rawDiagnostic(code)
    }

    /**
     * Deliberately a plain function, not @Composable: it reads no resources, so
     * keeping it callable outside composition is what makes it unit-testable.
     */
    fun severity(code: Int?): PaymentSeverity = when (code) {
        2 -> PaymentSeverity.Success
        1 -> PaymentSeverity.Warning
        3, 5 -> PaymentSeverity.Error
        else -> PaymentSeverity.Neutral
    }

    private fun rawDiagnostic(code: Int?): String =
        if (BuildConfig.DEBUG && code != null) "#$code" else "—"
}
