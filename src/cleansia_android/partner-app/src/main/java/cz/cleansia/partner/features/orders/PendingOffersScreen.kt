package cz.cleansia.partner.features.orders

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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
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
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateRange
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.MascotEmptyState
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme

/**
 * A refusal the cleaner is owed an explanation for. It carries the [action] because the two failures
 * are not the same failure: a refused confirm is the platform handing back a job it had already put
 * the cleaner's name on, while a refused decline changed nothing at all.
 */
data class OfferRefusal(
    val action: OfferAction,
    val displayOrderNumber: String?,
    val reason: String,
)

@Composable
fun PendingOffersScreen(
    onNavigateBack: () -> Unit,
    onOpenOrder: (String) -> Unit,
    viewModel: PendingOffersViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val actionState by viewModel.actionState.collectAsStateWithLifecycle()
    val attempt by viewModel.attempt.collectAsStateWithLifecycle()
    val refusal by viewModel.offerRefusal.collectAsStateWithLifecycle()

    var pendingDecline by remember { mutableStateOf<PendingOffer?>(null) }

    LaunchedEffect(viewModel) { viewModel.confirmed.collect { onOpenOrder(it) } }

    val inFlight = attempt?.takeIf { actionState is ActionState.Submitting }

    PendingOffersScreenContent(
        uiState = uiState,
        refusal = refusal,
        inFlight = inFlight,
        onNavigateBack = onNavigateBack,
        onRetry = viewModel::refresh,
        onConfirm = viewModel::confirm,
        onDeclineRequested = { pendingDecline = it },
        onDismissRefusal = viewModel::dismissRefusal,
    )

    pendingDecline?.let { offer ->
        CleansiaDialog(
            onDismiss = { pendingDecline = null },
            title = stringResource(R.string.offer_decline_title),
            message = stringResource(R.string.offer_decline_body),
            confirmLabel = stringResource(R.string.offer_decline_cta),
            dismissLabel = stringResource(R.string.cancel),
            destructive = true,
            onConfirm = {
                pendingDecline = null
                viewModel.decline(offer)
            },
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PendingOffersScreenContent(
    uiState: PendingOffersUiState,
    refusal: OfferRefusal?,
    inFlight: OfferAttempt?,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    onConfirm: (PendingOffer) -> Unit,
    onDeclineRequested: (PendingOffer) -> Unit,
    onDismissRefusal: () -> Unit,
    nowMillis: Long = System.currentTimeMillis(),
) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(stringResource(R.string.offers_title), style = MaterialTheme.typography.titleLarge) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
        containerColor = MaterialTheme.colorScheme.background,
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            when (uiState) {
                PendingOffersUiState.Loading -> Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center,
                ) { CircularProgressIndicator() }

                PendingOffersUiState.Error -> OffersErrorState(onRetry = onRetry)

                is PendingOffersUiState.Loaded ->
                    if (uiState.offers.isEmpty()) {
                        MascotEmptyState(
                            painter = painterResource(R.drawable.mascot_resting),
                            text = stringResource(R.string.offer_empty),
                            verticallyCentered = true,
                        )
                    } else {
                        LazyColumn(
                            modifier = Modifier.fillMaxSize(),
                            contentPadding = PaddingValues(Spacing.M),
                            verticalArrangement = Arrangement.spacedBy(Spacing.M),
                        ) {
                            item {
                                Text(
                                    text = stringResource(R.string.offers_subtitle),
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                            items(uiState.offers, key = { it.id.orEmpty() }) { offer ->
                                PendingOfferCard(
                                    offer = offer,
                                    nowMillis = nowMillis,
                                    inFlightAction = inFlight?.takeIf { it.orderId == offer.id }?.action,
                                    actionsLocked = inFlight != null,
                                    onConfirm = { onConfirm(offer) },
                                    onDecline = { onDeclineRequested(offer) },
                                )
                            }
                        }
                    }
            }
        }
    }

    if (refusal != null) {
        OfferRefusalDialog(refusal = refusal, onDismiss = onDismissRefusal)
    }
}

/**
 * The platform owns this failure and says so. A reservation spends no capacity, so the take gate can
 * refuse a job the cleaner was told was theirs — the server's own reason is quoted verbatim, wrapped
 * in the sentence that puts the mistake where it belongs. Shared with the order detail so the broken
 * promise reads identically wherever it is met.
 */
@Composable
fun OfferRefusalDialog(refusal: OfferRefusal, onDismiss: () -> Unit) {
    val copy = offerRefusalCopy(refusal.action)
    CleansiaDialog(
        onDismiss = onDismiss,
        title = refusal.displayOrderNumber
            ?.let { stringResource(copy.titleRes) + " · $it" }
            ?: stringResource(copy.titleRes),
        message = stringResource(copy.bodyRes, refusal.reason),
        confirmLabel = stringResource(R.string.ok),
        onConfirm = onDismiss,
    )
}

@Composable
private fun OffersErrorState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().padding(Spacing.XL),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = stringResource(R.string.error_generic),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.M))
        CleansiaPrimaryButton(text = stringResource(R.string.retry), onClick = onRetry)
    }
}

@Composable
private fun PendingOfferCard(
    offer: PendingOffer,
    nowMillis: Long,
    inFlightAction: OfferAction?,
    actionsLocked: Boolean,
    onConfirm: () -> Unit,
    onDecline: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface, RoundedCornerShape(20.dp))
            .padding(Spacing.M),
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        ReservedForYouRow(respondByUtc = offer.respondByUtc, nowMillis = nowMillis)

        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = formatOrderDateRange(offer.cleaningDateTime, offer.estimatedTime),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                offer.displayOrderNumber?.takeIf { it.isNotBlank() }?.let {
                    Text(
                        text = it,
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            Text(
                text = formatOrderPrice(offer.totalPrice, offer.currencyCode),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
        }

        // City and a truncated postcode — the pre-acceptance ceiling for every cleaner-facing surface.
        // The server sends nothing finer and the screen asks for nothing finer.
        offer.customerAddressApproximate?.takeIf { it.isNotBlank() }?.let { address ->
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                Icon(
                    imageVector = Icons.Outlined.LocationOn,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(16.dp),
                )
                Text(
                    text = address,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        val rooms = offer.rooms
        val baths = offer.bathrooms
        if (rooms > 0 || baths > 0) {
            Text(
                text = listOfNotNull(
                    rooms.takeIf { it > 0 }?.let { pluralStringResource(R.plurals.scope_rooms, it, it) },
                    baths.takeIf { it > 0 }?.let { pluralStringResource(R.plurals.scope_baths, it, it) },
                ).joinToString(" · "),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }

        CleansiaPrimaryButton(
            text = stringResource(R.string.offer_confirm),
            onClick = onConfirm,
            loading = inFlightAction == OfferAction.Confirm,
            enabled = !actionsLocked,
            modifier = Modifier.fillMaxWidth(),
        )
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
            TextButton(onClick = onDecline, enabled = !actionsLocked) {
                Text(stringResource(R.string.offer_decline))
            }
        }
    }
}

/**
 * The deadline is stated, never counted down: the hold's real expiry lives on the server, so a
 * remaining-time label on a screen left open drifts into a promise the client cannot keep.
 */
@Composable
fun reservedUntilLabel(respondByUtc: String?, nowMillis: Long): String {
    val deadline = respondByUtc?.let { respondBy(it, nowMillis) }
    return when (deadline?.day) {
        RespondByDay.Today -> stringResource(R.string.offer_reserved_until_today, deadline.time)
        RespondByDay.Tomorrow -> stringResource(R.string.offer_reserved_until_tomorrow, deadline.time)
        RespondByDay.Later -> stringResource(R.string.offer_reserved_until_date, deadline.date, deadline.time)
        RespondByDay.Ended, null -> stringResource(R.string.offer_reserved_ended)
    }
}

/** The disclosure that turns a priority into an assignment: this job is held for you, and until when. */
@Composable
fun ReservedForYouRow(respondByUtc: String, nowMillis: Long = System.currentTimeMillis()) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Icon(
            imageVector = Icons.Outlined.Schedule,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(16.dp),
        )
        Text(
            text = reservedUntilLabel(respondByUtc, nowMillis),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
    }
}

@Preview
@Composable
private fun PendingOffersScreenPreview() {
    CleansiaPartnerTheme {
        PendingOffersScreenContent(
            uiState = PendingOffersUiState.Loaded(
                listOf(
                    PendingOffer(
                        id = "1",
                        displayOrderNumber = "CL-2026-0042",
                        cleaningDateTime = "2026-08-12T09:00:00Z",
                        estimatedTime = 180,
                        respondByUtc = "2026-08-10T18:40:00Z",
                        customerAddressApproximate = "Praha 4 · 14000",
                        rooms = 3,
                        bathrooms = 1,
                        totalPrice = 1850.0,
                        currencyCode = "CZK",
                    ),
                ),
            ),
            refusal = OfferRefusal(
                OfferAction.Confirm,
                "CL-2026-0042",
                "You've reached your weekly order limit.",
            ),
            inFlight = null,
            onNavigateBack = {},
            onRetry = {},
            onConfirm = {},
            onDeclineRequested = {},
            onDismissRefusal = {},
            nowMillis = 0L,
        )
    }
}
