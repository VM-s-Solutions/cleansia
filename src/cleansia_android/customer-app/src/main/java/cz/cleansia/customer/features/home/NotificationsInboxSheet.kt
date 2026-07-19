package cz.cleansia.customer.features.home

import android.text.format.DateUtils
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.R
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneOffset

/**
 * The notifications inbox reached from the Home bell — the server-backed feed
 * (T-0393 promoted the interim empty-state sheet to the real thing). Fetches
 * page 1 on every open, fires the watermarked mark-all so the badge clears,
 * and keeps the mascot empty state for zero rows. Row taps deep-link through
 * the same resolver push taps use.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NotificationsInboxSheet(
    onDismiss: () -> Unit,
    onOpenRoute: (Any) -> Unit = {},
    viewModel: NotificationsInboxViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    LaunchedEffect(viewModel) { viewModel.open() }
    LaunchedEffect(viewModel) { viewModel.openRoute.collect { onOpenRoute(it) } }

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = MaterialTheme.colorScheme.background,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp, bottom = 40.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(
                text = stringResource(R.string.notifications_inbox_title),
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onBackground,
            )
            Spacer(Modifier.height(16.dp))
            when (val s = state) {
                NotificationsInboxUiState.Loading -> InboxLoadingState()
                NotificationsInboxUiState.Error -> InboxErrorState(onRetry = viewModel::open)
                is NotificationsInboxUiState.Loaded ->
                    if (s.items.isEmpty()) {
                        InboxEmptyState()
                    } else {
                        InboxFeedList(
                            state = s,
                            onRowClick = viewModel::onRowClick,
                            onLoadMore = viewModel::loadMore,
                        )
                    }
            }
        }
    }
}

@Composable
private fun InboxLoadingState() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(220.dp),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

@Composable
private fun InboxErrorState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 32.dp, vertical = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.notifications_inbox_error_message),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.notifications_inbox_error_retry),
            onClick = onRetry,
        )
    }
}

@Composable
private fun InboxEmptyState() {
    Column(
        modifier = Modifier.fillMaxWidth(),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Spacer(Modifier.height(8.dp))
        Image(
            painter = painterResource(R.drawable.mascot_leaning),
            contentDescription = null,
            modifier = Modifier.size(160.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.notifications_inbox_empty_title),
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = stringResource(R.string.notifications_inbox_empty_subtitle),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(horizontal = 32.dp),
        )
    }
}

@Composable
private fun InboxFeedList(
    state: NotificationsInboxUiState.Loaded,
    onRowClick: (NotificationFeedItem) -> Unit,
    onLoadMore: () -> Unit,
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(max = 480.dp),
        contentPadding = PaddingValues(horizontal = 20.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        items(state.items, key = { it.id }) { item ->
            NotificationFeedRow(item = item, onClick = { onRowClick(item) })
        }
        if (state.canLoadMore) {
            item(key = "load-more") {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 12.dp),
                    contentAlignment = Alignment.Center,
                ) {
                    CircularProgressIndicator(
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(24.dp),
                    )
                }
                // Composed only when scrolled into view — that's the load
                // trigger. Keyed on the list size so a sentinel that stays
                // visible after an append fires again for the next page;
                // the VM's loadingMore guard absorbs duplicate calls.
                LaunchedEffect(state.items.size) { onLoadMore() }
            }
        }
    }
}

@Composable
private fun NotificationFeedRow(item: NotificationFeedItem, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(14.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.Outlined.NotificationsNone,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = item.body,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 3,
                overflow = TextOverflow.Ellipsis,
            )
            val relativeTime = remember(item.createdOn) { formatRelativeTime(item.createdOn) }
            if (relativeTime.isNotBlank()) {
                Spacer(Modifier.height(6.dp))
                Text(
                    text = relativeTime,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        if (item.unread) {
            Spacer(Modifier.width(8.dp))
            Box(
                modifier = Modifier
                    .size(8.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary),
            )
        }
    }
}

/**
 * Locale-aware "5 minutes ago"-style timestamp via the platform's
 * [DateUtils] — free translations in every supported locale. Defensive
 * against both the offset-carrying and the bare-UTC ISO forms the backend
 * can emit; malformed input renders no timestamp rather than crashing.
 */
private fun formatRelativeTime(iso: String): String {
    val millis = runCatching { OffsetDateTime.parse(iso).toInstant().toEpochMilli() }
        .recoverCatching { LocalDateTime.parse(iso).toInstant(ZoneOffset.UTC).toEpochMilli() }
        .getOrNull() ?: return ""
    return DateUtils.getRelativeTimeSpanString(
        millis,
        System.currentTimeMillis(),
        DateUtils.MINUTE_IN_MILLIS,
    ).toString()
}
