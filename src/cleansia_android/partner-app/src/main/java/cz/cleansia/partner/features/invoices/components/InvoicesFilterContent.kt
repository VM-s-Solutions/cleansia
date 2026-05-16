package cz.cleansia.partner.features.invoices.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.invoices.InvoiceStatus
import cz.cleansia.partner.features.invoices.viewmodels.InvoiceFilterState
import cz.cleansia.partner.ui.components.FilterSection
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun InvoicesFilterContent(
    filterState: InvoiceFilterState,
    onSearchTermChange: (String) -> Unit,
    onInvoiceStatusToggle: (InvoiceStatus) -> Unit,
    onStartDateChange: (String?) -> Unit,
    onEndDateChange: (String?) -> Unit
) {
    Column(
        modifier = Modifier.padding(bottom = 16.dp)
    ) {
        // Search section
        FilterSection(title = stringResource(R.string.search)) {
            OutlinedTextField(
                value = filterState.searchTerm,
                onValueChange = onSearchTermChange,
                placeholder = { Text(stringResource(R.string.search_invoice)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                trailingIcon = {
                    if (filterState.searchTerm.isNotEmpty()) {
                        IconButton(onClick = { onSearchTermChange("") }) {
                            Icon(
                                imageVector = Icons.Default.Clear,
                                contentDescription = stringResource(R.string.clear_all),
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }
                }
            )
        }

        // Invoice Status section
        FilterSection(title = stringResource(R.string.invoice_status_filter)) {
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                InvoiceStatus.entries.forEach { status ->
                    val isSelected = filterState.invoiceStatuses.contains(status)
                    FilterChip(
                        selected = isSelected,
                        onClick = { onInvoiceStatusToggle(status) },
                        label = { Text(getInvoiceStatusLabel(status)) },
                        colors = FilterChipDefaults.filterChipColors(
                            selectedContainerColor = MaterialTheme.colorScheme.primaryContainer,
                            selectedLabelColor = MaterialTheme.colorScheme.onPrimaryContainer
                        )
                    )
                }
            }
        }

        // Date Range section
        FilterSection(title = stringResource(R.string.date_range)) {
            Column(
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                DatePickerField(
                    label = stringResource(R.string.start_date),
                    value = filterState.startDate,
                    onValueChange = onStartDateChange
                )
                DatePickerField(
                    label = stringResource(R.string.end_date),
                    value = filterState.endDate,
                    onValueChange = onEndDateChange
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DatePickerField(
    label: String,
    value: String?,
    onValueChange: (String?) -> Unit
) {
    var showDatePicker by remember { mutableStateOf(false) }
    val datePickerState = rememberDatePickerState()

    // Display localized date format, but keep API format in the value
    val displayValue = value?.let { DateTimeUtils.formatDate(it) } ?: ""

    OutlinedTextField(
        value = displayValue,
        onValueChange = { },
        label = { Text(label) },
        placeholder = { Text(stringResource(R.string.select_date)) },
        readOnly = true,
        modifier = Modifier
            .fillMaxWidth()
            .clickable { showDatePicker = true },
        trailingIcon = {
            Row {
                if (value != null) {
                    IconButton(onClick = { onValueChange(null) }) {
                        Icon(
                            imageVector = Icons.Default.Clear,
                            contentDescription = stringResource(R.string.clear_all),
                            modifier = Modifier.size(20.dp)
                        )
                    }
                }
                IconButton(onClick = { showDatePicker = true }) {
                    Icon(
                        imageVector = Icons.Default.CalendarToday,
                        contentDescription = stringResource(R.string.select_date),
                        modifier = Modifier.size(20.dp)
                    )
                }
            }
        }
    )

    if (showDatePicker) {
        DatePickerDialog(
            onDismissRequest = { showDatePicker = false },
            confirmButton = {
                TextButton(
                    onClick = {
                        datePickerState.selectedDateMillis?.let { millis ->
                            val dateFormat = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault())
                            onValueChange(dateFormat.format(Date(millis)))
                        }
                        showDatePicker = false
                    }
                ) {
                    Text(stringResource(R.string.confirm))
                }
            },
            dismissButton = {
                TextButton(onClick = { showDatePicker = false }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        ) {
            DatePicker(state = datePickerState)
        }
    }
}

@Composable
private fun getInvoiceStatusLabel(status: InvoiceStatus): String {
    return when (status) {
        InvoiceStatus.PENDING -> stringResource(R.string.invoice_pending)
        InvoiceStatus.APPROVED -> stringResource(R.string.invoice_approved)
        InvoiceStatus.PAID -> stringResource(R.string.invoice_paid)
        InvoiceStatus.DISPUTED -> stringResource(R.string.invoice_disputed)
        InvoiceStatus.REJECTED -> stringResource(R.string.invoice_rejected)
        InvoiceStatus.CANCELLED -> stringResource(R.string.invoice_cancelled)
    }
}
