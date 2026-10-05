package cz.cleansia.customer.features.payments

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.ReceiptLong
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.customer.R
import cz.cleansia.customer.core.payments.Receivable
import cz.cleansia.customer.core.payments.SavedCard
import cz.cleansia.customer.ui.theme.CleansiaTheme
import java.util.Locale
import kotlinx.datetime.Instant

@Composable
fun PaymentsScreen(
    onBack: () -> Unit = {},
    viewModel: PaymentsViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val removeState by viewModel.removeState.collectAsStateWithLifecycle()
    val payState by viewModel.payState.collectAsStateWithLifecycle()
    var cardToRemove by remember { mutableStateOf<SavedCard?>(null) }
    // The card a confirmed removal is running for, so its row can show the spinner the dialog used to.
    var removingCardId by remember { mutableStateOf<String?>(null) }
    val uriHandler = LocalUriHandler.current

    LaunchedEffect(viewModel) { viewModel.payLinks.collect { uriHandler.openUri(it) } }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.onResumed() }

    PaymentsScreenContent(
        state = state,
        removingCardId = removingCardId.takeIf { removeState is ActionState.Submitting },
        paying = payState is ActionState.Submitting,
        onBack = onBack,
        onRetry = viewModel::load,
        onPay = viewModel::pay,
        onRemoveRequested = { cardToRemove = it },
    )

    // The system confirm closes on the tap; the row shows the removal running and a refusal is the
    // snackbar's, so nothing holds a dialog open over a request.
    cardToRemove?.let { card ->
        AlertDialog(
            onDismissRequest = { cardToRemove = null },
            title = { Text(stringResource(R.string.payments_card_remove_title)) },
            text = { Text(stringResource(R.string.payments_card_remove_message)) },
            confirmButton = {
                TextButton(
                    onClick = {
                        cardToRemove = null
                        removingCardId = card.id
                        viewModel.remove(card)
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text(stringResource(R.string.payments_card_remove_confirm)) }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { cardToRemove = null }) { Text(stringResource(R.string.common_cancel)) }
            },
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PaymentsScreenContent(
    state: PaymentsUiState,
    removingCardId: String?,
    paying: Boolean,
    onBack: () -> Unit = {},
    onRetry: () -> Unit = {},
    onPay: (Receivable) -> Unit = {},
    onRemoveRequested: (SavedCard) -> Unit = {},
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        TopAppBar(
            title = {
                Text(
                    stringResource(R.string.payments_title),
                    style = MaterialTheme.typography.titleMedium.copy(
                        fontFamily = Poppins,
                        fontWeight = FontWeight.SemiBold,
                    ),
                )
            },
            navigationIcon = {
                IconButton(onClick = onBack) {
                    Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back))
                }
            },
            colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.surface),
        )

        when (state) {
            PaymentsUiState.Loading -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
            }
            PaymentsUiState.Error -> ErrorState(onRetry = onRetry)
            is PaymentsUiState.Loaded -> LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(20.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                if (state.receivables.isNotEmpty()) {
                    item(key = "due-title") { SectionTitle(R.string.payments_due_title) }
                    item(key = "due-intro") { SectionIntro(R.string.payments_due_intro) }
                    items(state.receivables, key = { it.id }) { receivable ->
                        ReceivableCard(receivable = receivable, paying = paying, onPay = { onPay(receivable) })
                    }
                    item(key = "due-gap") { Spacer(Modifier.height(8.dp)) }
                }
                item(key = "card-title") { SectionTitle(R.string.payments_card_title) }
                item(key = "card-intro") { SectionIntro(R.string.payments_card_intro) }
                if (state.cards.isEmpty()) {
                    item(key = "card-empty") { SectionIntro(R.string.payments_card_empty) }
                } else {
                    items(state.cards, key = { it.id }) { card ->
                        SavedCardRow(
                            card = card,
                            removing = removingCardId == card.id,
                            canRemove = removingCardId == null,
                            onRemove = { onRemoveRequested(card) },
                        )
                    }
                }
            }
        }
    }
}

/** The backend's `ReceivableKind`, ordinal by ordinal; an ordinal it adds later reads as the generic label. */
@StringRes
internal fun receivableKindLabelRes(kind: Int): Int? = when (kind) {
    1 -> R.string.receivable_kind_cancellation_fee
    2 -> R.string.receivable_kind_lockout
    3 -> R.string.receivable_kind_unpaid_cash
    4 -> R.string.receivable_kind_top_up
    else -> null
}

@Composable
private fun SectionTitle(@StringRes title: Int) {
    Text(
        stringResource(title),
        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onSurface,
    )
}

@Composable
private fun SectionIntro(@StringRes text: Int) {
    Text(
        stringResource(text),
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
}

@Composable
private fun ReceivableCard(receivable: Receivable, paying: Boolean, onPay: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            LeadingIcon(Icons.AutoMirrored.Outlined.ReceiptLong)
            Spacer(Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    stringResource(receivableKindLabelRes(receivable.kind) ?: R.string.receivable_kind_other),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(
                    stringResource(R.string.payments_due_order, receivable.displayOrderNumber),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Text(
                formatOrderPrice(receivable.amount, receivable.currencyCode),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        Spacer(Modifier.height(12.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.payments_pay_action),
            onClick = onPay,
            size = CleansiaButtonSize.Medium,
            loading = paying,
            enabled = !paying,
        )
    }
}

@Composable
private fun SavedCardRow(
    card: SavedCard,
    removing: Boolean,
    // One removal at a time: the view model drops a second while the first is in flight.
    canRemove: Boolean,
    onRemove: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        LeadingIcon(Icons.Outlined.CreditCard)
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                stringResource(
                    R.string.payments_card_label,
                    card.brand.replaceFirstChar { it.titlecase(Locale.ROOT) },
                    card.last4,
                ),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                stringResource(R.string.payments_card_expires, card.expMonth, card.expYear % 100, card.currencyCode),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        if (removing) {
            Box(Modifier.size(48.dp), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(
                    modifier = Modifier.size(22.dp),
                    strokeWidth = 2.dp,
                    color = MaterialTheme.colorScheme.error,
                )
            }
        } else {
            IconButton(onClick = onRemove, enabled = canRemove) {
                Icon(
                    imageVector = Icons.Outlined.DeleteOutline,
                    contentDescription = stringResource(R.string.payments_card_remove_action),
                    tint = MaterialTheme.colorScheme.error.copy(alpha = if (canRemove) 1f else 0.4f),
                )
            }
        }
    }
}

@Composable
private fun LeadingIcon(icon: ImageVector) {
    Box(
        modifier = Modifier
            .size(44.dp)
            .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(22.dp),
        )
    }
}

@Composable
private fun ErrorState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.payments_error_message),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        CleansiaOutlinedButton(
            text = stringResource(R.string.payments_error_retry),
            onClick = onRetry,
        )
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun PaymentsPreview() {
    CleansiaTheme {
        PaymentsScreenContent(
            state = PaymentsUiState.Loaded(
                receivables = listOf(
                    Receivable(
                        id = "rcv-1",
                        orderId = "ord-1",
                        displayOrderNumber = "CL-1042",
                        kind = 1,
                        amount = 450.0,
                        currencyCode = "CZK",
                        createdOn = Instant.parse("2026-09-28T10:00:00Z"),
                    ),
                ),
                cards = listOf(
                    SavedCard(id = "card-1", brand = "visa", last4 = "4242", expMonth = 4, expYear = 2029, currencyCode = "CZK"),
                ),
            ),
            removingCardId = null,
            paying = false,
        )
    }
}
