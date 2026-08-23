package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface PendingOffersCardUiState {
    data object Hidden : PendingOffersCardUiState
    data class Visible(val count: Int, val soonestRespondByUtc: String?) : PendingOffersCardUiState
}

/**
 * A reservation is rare and time-limited, so it gets no tab of its own: a permanent Offers tab would
 * charge every cleaner an empty state every day for something most of them will see a handful of times
 * a year. It rides the dashboard instead — the one screen everybody opens — and disappears entirely
 * when nothing is waiting, the same shape the one-time radius prompt uses.
 */
@HiltViewModel
class PendingOffersCardViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
) : ViewModel() {

    val uiState: StateFlow<PendingOffersCardUiState> = ordersRepository.pendingOffers
        .map { offers ->
            if (offers.isEmpty()) {
                PendingOffersCardUiState.Hidden
            } else {
                PendingOffersCardUiState.Visible(
                    count = offers.size,
                    soonestRespondByUtc = soonestOffer(offers)?.respondByUtc,
                )
            }
        }
        .stateIn(viewModelScope, SharingStarted.Eagerly, PendingOffersCardUiState.Hidden)

    init {
        refresh()
    }

    fun refresh() {
        if (!ordersRepository.arePendingOffersStale()) return
        viewModelScope.launch { ordersRepository.refreshPendingOffers() }
    }
}

@Composable
fun PendingOffersCardContent(
    count: Int,
    soonestRespondByUtc: String?,
    onOpenOffers: () -> Unit,
    nowMillis: Long = System.currentTimeMillis(),
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
            .clickable(onClick = onOpenOffers)
            .padding(Spacing.M),
    ) {
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = stringResource(R.string.offers_card_title),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f),
            )
            if (count > 1) {
                Text(
                    text = stringResource(R.string.offers_card_more, count - 1),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        Spacer(Modifier.height(Spacing.XS))
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
                text = reservedUntilLabel(soonestRespondByUtc, nowMillis),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.weight(1f),
            )
            Text(
                text = stringResource(R.string.offers_card_cta),
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(16.dp),
            )
        }
    }
}

@Preview
@Composable
private fun PendingOffersCardPreview() {
    CleansiaPartnerTheme {
        PendingOffersCardContent(
            count = 2,
            soonestRespondByUtc = "2026-08-10T18:40:00Z",
            onOpenOffers = {},
            nowMillis = 0L,
        )
    }
}
