package cz.cleansia.customer.features.disputes

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeListItemDto
import cz.cleansia.customer.core.format.disputeStatusColor
import cz.cleansia.customer.core.format.formatOrderDateTime
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.snackbar.SnackbarController
import cz.cleansia.customer.ui.snackbar.SnackbarControllerEntryPoint
import cz.cleansia.customer.ui.theme.Poppins
import dagger.hilt.android.EntryPointAccessors

/**
 * My Disputes screen — lists the signed-in user's disputes with infinite scroll
 * + pull-to-refresh, mirroring [cz.cleansia.customer.features.orders.OrdersTab].
 *
 * Entry points:
 *  - Profile tab → "Disputes" row
 *  - Order detail → "Report issue" footer (routes through CreateDispute first,
 *    which lands here after success via the DisputeDetail screen)
 *
 * The FAB always opens CreateDispute with no orderId — that screen renders an
 * error state explaining dispute filing requires an order, since we don't
 * have a picker UI in Wave 2. Not ideal but acceptable per the phase spec.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DisputesListScreen(
    onBack: () -> Unit = {},
    onDisputeClick: (disputeId: String) -> Unit = {},
    onCreateDispute: () -> Unit = {},
    viewModel: DisputesListViewModel = hiltViewModel(),
) {
    val disputes by viewModel.disputes.collectAsStateWithLifecycle()
    val loading by viewModel.loading.collectAsStateWithLifecycle()
    val loadingMore by viewModel.loadingMore.collectAsStateWithLifecycle()
    val loaded by viewModel.loaded.collectAsStateWithLifecycle()
    val total by viewModel.total.collectAsStateWithLifecycle()

    // Snackbar controller (for the FAB "no order context" hint). Sourced via
    // the standard EntryPoint so the screen doesn't need its own VM dep.
    val context = androidx.compose.ui.platform.LocalContext.current
    val snackbar: SnackbarController = remember {
        EntryPointAccessors
            .fromApplication(context, SnackbarControllerEntryPoint::class.java)
            .snackbarController()
    }
    val fabNoOrderHint = stringResource(R.string.dispute_list_fab_no_order)

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        stringResource(R.string.dispute_list_title),
                        style = MaterialTheme.typography.titleMedium.copy(
                            fontFamily = Poppins,
                            fontWeight = FontWeight.SemiBold,
                        ),
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.common_back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                ),
            )
        },
        floatingActionButton = {
            // The FAB always routes to CreateDispute without an order — the
            // screen itself handles the missing-context case with an error
            // state + snackbar hint. We also surface the hint here so tapping
            // the FAB directly feels informative rather than a dead-end.
            ExtendedFloatingActionButton(
                onClick = {
                    // Surface an info hint about needing order context; the
                    // CreateDispute screen itself also renders an error state
                    // for the no-orderId case. `showInfo` is non-suspend
                    // (channel tryEmit), so we can call it from the lambda.
                    snackbar.showInfo(fabNoOrderHint)
                    onCreateDispute()
                },
                containerColor = MaterialTheme.colorScheme.primary,
                contentColor = MaterialTheme.colorScheme.onPrimary,
                icon = { Icon(Icons.Outlined.Add, contentDescription = null) },
                text = { Text(stringResource(R.string.dispute_list_fab_new)) },
            )
        },
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background),
        ) {
            when {
                loading && !loaded -> LoadingState()
                !loaded && disputes.isEmpty() -> ErrorState(onRetry = viewModel::refresh)
                disputes.isEmpty() -> EmptyState(onBrowseOrders = onBack)
                else -> LoadedList(
                    disputes = disputes,
                    isRefreshing = loading,
                    loadingMore = loadingMore,
                    hasMore = disputes.size < total,
                    onLoadMore = viewModel::loadNextPage,
                    onRefresh = viewModel::refresh,
                    onDisputeClick = onDisputeClick,
                )
            }
        }
    }
}

/* ── States ── */

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun LoadedList(
    disputes: List<DisputeListItemDto>,
    isRefreshing: Boolean,
    loadingMore: Boolean,
    hasMore: Boolean,
    onLoadMore: () -> Unit,
    onRefresh: () -> Unit,
    onDisputeClick: (String) -> Unit,
) {
    val listState = rememberLazyListState()
    val shouldLoadMore by remember(hasMore, loadingMore) {
        derivedStateOf {
            val last = listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0
            val totalItems = listState.layoutInfo.totalItemsCount
            hasMore && !loadingMore && totalItems > 0 && last >= totalItems - 3
        }
    }
    LaunchedEffect(shouldLoadMore) {
        if (shouldLoadMore) onLoadMore()
    }

    val pullState = rememberPullToRefreshState()
    PullToRefreshBox(
        isRefreshing = isRefreshing,
        onRefresh = onRefresh,
        state = pullState,
        modifier = Modifier.fillMaxSize(),
    ) {
        LazyColumn(
            state = listState,
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(horizontal = 20.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            items(disputes, key = { it.id ?: it.hashCode().toString() }) { dispute ->
                DisputeRow(
                    dispute = dispute,
                    onClick = { dispute.id?.let(onDisputeClick) },
                )
            }
            if (loadingMore) {
                item { LoadingMoreRow() }
            }
            item { Spacer(Modifier.height(80.dp)) } // Breathing room under the FAB
        }
    }
}

@Composable
private fun DisputeRow(
    dispute: DisputeListItemDto,
    onClick: () -> Unit,
) {
    val statusColor = disputeStatusColor(dispute.status?.value)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(IntrinsicSize.Min)
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .clickable(onClick = onClick),
    ) {
        // Left status accent strip, same vertical treatment as OrderCard.
        Box(
            modifier = Modifier
                .width(4.dp)
                .fillMaxHeight()
                .background(statusColor),
        )
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    text = dispute.displayOrderNumber?.let { "#$it" } ?: "—",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                StatusPill(
                    label = dispute.status?.name ?: "—",
                    color = statusColor,
                )
            }
            Spacer(Modifier.height(6.dp))
            Text(
                text = dispute.reason?.name ?: "—",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = formatOrderDateTime(dispute.createdOn),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

@Composable
private fun LoadingState() {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

@Composable
private fun ErrorState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(40.dp),
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.dispute_list_error_title),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.dispute_list_error_retry),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onRetry)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

@Composable
private fun EmptyState(onBrowseOrders: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 20.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Image(
            painterResource(R.drawable.mascot_idea),
            contentDescription = null,
            modifier = Modifier.size(160.dp),
        )
        Spacer(Modifier.height(24.dp))
        Text(
            text = stringResource(R.string.dispute_list_empty_title),
            style = MaterialTheme.typography.headlineSmall.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = stringResource(R.string.dispute_list_empty_subtitle),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.dispute_list_empty_cta),
            onClick = onBrowseOrders,
        )
    }
}

@Composable
private fun LoadingMoreRow() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 12.dp),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator(
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(22.dp),
            strokeWidth = 2.dp,
        )
    }
}
