package cz.cleansia.partner.features.invoices.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeInvoiceStatus
import cz.cleansia.partner.ui.theme.StatusCancelledBg
import cz.cleansia.partner.ui.theme.StatusCancelledText
import cz.cleansia.partner.ui.theme.StatusCompletedBg
import cz.cleansia.partner.ui.theme.StatusCompletedText
import cz.cleansia.partner.ui.theme.StatusConfirmedBg
import cz.cleansia.partner.ui.theme.StatusConfirmedText
import cz.cleansia.partner.ui.theme.StatusFailedBg
import cz.cleansia.partner.ui.theme.StatusFailedText
import cz.cleansia.partner.ui.theme.StatusPendingBg
import cz.cleansia.partner.ui.theme.StatusPendingText

/**
 * Pill for invoice status. Backend enum:
 *   1 Pending, 2 Approved, 3 Paid, 4 Disputed, 5 Rejected, 6 Cancelled.
 */
@Composable
fun InvoiceStatusBadge(status: EmployeeInvoiceStatus?) {
    val (label, bg, fg) = when (status) {
        EmployeeInvoiceStatus._1 -> Triple(stringResource(R.string.invoice_status_pending), StatusPendingBg, StatusPendingText)
        EmployeeInvoiceStatus._2 -> Triple(stringResource(R.string.invoice_status_approved), StatusConfirmedBg, StatusConfirmedText)
        EmployeeInvoiceStatus._3 -> Triple(stringResource(R.string.invoice_status_paid), StatusCompletedBg, StatusCompletedText)
        EmployeeInvoiceStatus._4 -> Triple(stringResource(R.string.invoice_status_disputed), StatusFailedBg, StatusFailedText)
        EmployeeInvoiceStatus._5 -> Triple(stringResource(R.string.invoice_status_rejected), StatusFailedBg, StatusFailedText)
        EmployeeInvoiceStatus._6 -> Triple(stringResource(R.string.invoice_status_cancelled), StatusCancelledBg, StatusCancelledText)
        null -> Triple("—", Color.LightGray, Color.DarkGray)
    }
    Text(
        text = label,
        modifier = Modifier
            .clip(RoundedCornerShape(50))
            .background(bg)
            .padding(horizontal = 10.dp, vertical = 4.dp),
        style = MaterialTheme.typography.labelSmall,
        color = fg,
        fontWeight = FontWeight.SemiBold,
    )
}
