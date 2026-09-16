package cz.cleansia.customer.features.orders

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.state.ActionState

@Composable
fun GuestOrderScreen(
    onBack: () -> Unit,
    viewModel: GuestOrderViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val preview by viewModel.preview.collectAsStateWithLifecycle()
    val cancelState by viewModel.cancelState.collectAsStateWithLifecycle()
    val showCancellation by viewModel.showCancellation.collectAsStateWithLifecycle()
    var number by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var code by remember { mutableStateOf("") }
    val submitting = cancelState is ActionState.Submitting

    DisposableEffect(viewModel) { onDispose { viewModel.clear() } }
    BackHandler(enabled = submitting) {}
    LaunchedEffect(state) {
        if (state is GuestOrderUiState.Cancelled) {
            number = ""
            email = ""
            code = ""
        }
    }

    Column(
        modifier = Modifier.fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .safeDrawingPadding()
            .imePadding(),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack, enabled = !submitting) {
                Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back))
            }
            Text(stringResource(R.string.guest_order_title), style = MaterialTheme.typography.titleLarge)
        }
        Column(
            modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp, vertical = 16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text(stringResource(R.string.guest_order_intro), style = MaterialTheme.typography.bodyMedium)
            CleansiaTextField(
                value = number,
                onValueChange = { number = it; viewModel.onCredentialsChanged() },
                label = stringResource(R.string.guest_order_number),
                enabled = !submitting,
            )
            CleansiaTextField(
                value = email,
                onValueChange = { email = it; viewModel.onCredentialsChanged() },
                label = stringResource(R.string.login_email),
                keyboardType = KeyboardType.Email,
                enabled = !submitting,
            )
            CleansiaTextField(
                value = code,
                onValueChange = { code = it; viewModel.onCredentialsChanged() },
                label = stringResource(R.string.guest_order_code),
                enabled = !submitting,
            )
            CleansiaPrimaryButton(
                text = stringResource(R.string.guest_order_lookup),
                onClick = { viewModel.lookup(number, email, code) },
                loading = state is GuestOrderUiState.Loading,
                enabled = !submitting && state !is GuestOrderUiState.Loading &&
                    number.isNotBlank() && email.isNotBlank() && code.isNotBlank(),
            )
            when (val current = state) {
                GuestOrderUiState.Empty -> Text(
                    stringResource(R.string.guest_order_empty),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                GuestOrderUiState.Loading -> Text(stringResource(R.string.guest_order_loading))
                is GuestOrderUiState.Error -> Text(current.message, color = MaterialTheme.colorScheme.error)
                is GuestOrderUiState.Cancelled -> Text(current.message, color = MaterialTheme.colorScheme.primary)
                is GuestOrderUiState.Loaded -> {
                    val order = current.order
                    Text(order.displayOrderNumber, style = MaterialTheme.typography.titleMedium)
                    Text(stringResource(R.string.order_detail_date))
                    Text(formatOrderDateTime(order.cleaningDateTime))
                    Text(stringResource(R.string.order_detail_total))
                    Text(formatOrderPrice(order.totalPrice, order.currencyCode))
                    orderStatusLabelRes(order.status)?.let { Text(stringResource(it)) }
                    if (guestOrderCanCancel(order.status)) {
                        CleansiaOutlinedButton(
                            text = stringResource(R.string.guest_order_cancel),
                            onClick = viewModel::openCancellation,
                            enabled = !submitting,
                        )
                    } else {
                        Text(stringResource(R.string.guest_order_cannot_cancel))
                    }
                }
            }
        }
    }
    if (showCancellation) {
        CancelOrderSheet(
            previewState = preview,
            onDismiss = viewModel::dismissCancellation,
            onConfirm = viewModel::cancel,
            onRetryPreview = viewModel::loadPreview,
            isSubmitting = submitting,
            errorMessage = (cancelState as? ActionState.Error)?.message,
            requireValidPreview = true,
            maxReasonLength = 500,
        )
    }
}
