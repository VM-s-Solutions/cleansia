package cz.cleansia.customer.features.booking

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
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
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.PersonOutline
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.ServingCleanerDto
import cz.cleansia.core.ui.components.CleansiaDialog

/**
 * Plus-only picker that lets the customer pre-request a cleaner they've
 * already worked with. Hidden when:
 *  - the user is not a Plus member
 *  - the backend returned no eligible cleaners (first-time customer or no
 *    Completed orders yet)
 *
 * Tapping the row opens a list dialog. Selection writes to
 * [BookingState.preferredEmployeeId]; the booking submit picks it up and the
 * matching algorithm boosts that cleaner's score.
 *
 * Eligibility list is fetched once per opening of the booking sheet — no
 * cache invalidation, since the set only grows on order completion (next
 * sheet open will re-fetch fresh).
 */
@Composable
fun PreferredCleanerPicker(
    selectedEmployeeId: String?,
    onSelect: (employeeId: String?, fullName: String?) -> Unit,
    viewModel: PreferredCleanerViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    val membership = viewModel.membershipRepository
    val orders = viewModel.orderRepository

    val membershipState by membership.current.collectAsState()
    val isPlus = membershipState?.hasMembership == true

    var cleaners by remember { mutableStateOf<List<ServingCleanerDto>>(emptyList()) }
    var loaded by remember { mutableStateOf(false) }

    // Refresh membership state once on first composition — repo is a singleton
    // shared with the Profile tab, so it might be stale or unfetched depending
    // on what the user did before opening the booking sheet.
    LaunchedEffect(Unit) {
        if (membershipState == null) {
            membership.refresh()
        }
    }

    // Fetch only once Plus is confirmed — saves a round trip for non-Plus users.
    LaunchedEffect(isPlus) {
        if (isPlus && !loaded) {
            cleaners = orders.getMyServingCleaners()
            loaded = true
        }
    }

    if (!isPlus || cleaners.isEmpty()) return

    var dialogOpen by remember { mutableStateOf(false) }
    val selected = remember(selectedEmployeeId, cleaners) {
        cleaners.firstOrNull { it.employeeId == selectedEmployeeId }
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .clickable { dialogOpen = true }
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(36.dp)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.15f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.PersonOutline,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(
                stringResource(R.string.booking_preferred_cleaner_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = selected?.fullName ?: stringResource(R.string.booking_preferred_cleaner_subtitle),
                style = MaterialTheme.typography.bodySmall,
                color = if (selected != null) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        if (selected != null) {
            IconButton(onClick = { onSelect(null, null) }) {
                Icon(
                    Icons.Outlined.Close,
                    contentDescription = stringResource(R.string.booking_preferred_cleaner_clear),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
        } else {
            Icon(
                Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
    }

    if (dialogOpen) {
        CleansiaDialog(
            onDismiss = { dialogOpen = false },
            title = stringResource(R.string.booking_preferred_cleaner_dialog_title),
            confirmLabel = stringResource(R.string.common_back),
            onConfirm = { dialogOpen = false },
            content = {
                LazyColumn(
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier.height(320.dp),
                ) {
                    items(cleaners, key = { it.employeeId }) { cleaner ->
                        CleanerRow(
                            cleaner = cleaner,
                            isSelected = cleaner.employeeId == selectedEmployeeId,
                            onClick = {
                                onSelect(cleaner.employeeId, cleaner.fullName)
                                dialogOpen = false
                            },
                        )
                    }
                }
            },
        )
    }
}

@Composable
private fun CleanerRow(
    cleaner: ServingCleanerDto,
    isSelected: Boolean,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(
                if (isSelected) MaterialTheme.colorScheme.primary.copy(alpha = 0.10f)
                else MaterialTheme.colorScheme.surface,
            )
            .border(
                1.dp,
                if (isSelected) MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(12.dp),
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(36.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.20f)),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = cleaner.fullName.take(1).uppercase(),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
        Spacer(Modifier.width(12.dp))
        Text(
            text = cleaner.fullName,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
    }
}
