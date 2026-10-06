package cz.cleansia.customer.features.payments

import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.customer.R

/**
 * The customer owes a company money, so no new booking of any kind is taken until it is paid through its
 * pay link or written off (owner ruling 2026-10-06). → /product/business-rules#card-guarantee
 */
object UnpaidReceivable {
    const val KEY = "order.unpaid_receivable"

    fun refuses(keys: Iterable<String>): Boolean = KEY in keys

    fun refuses(error: ApiError?): Boolean = error is ApiError.BadRequest &&
        (error.errorKey == KEY || error.validationErrors.orEmpty().values.any { KEY in it })
}

@Composable
fun UnpaidReceivableDialog(onPay: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.unpaid_receivable_title)) },
        text = { Text(stringResource(R.string.error_order_unpaid_receivable)) },
        confirmButton = { CleansiaTextButton(onClick = onPay) { Text(stringResource(R.string.unpaid_receivable_pay)) } },
        dismissButton = { CleansiaTextButton(onClick = onDismiss) { Text(stringResource(R.string.common_close)) } },
    )
}
