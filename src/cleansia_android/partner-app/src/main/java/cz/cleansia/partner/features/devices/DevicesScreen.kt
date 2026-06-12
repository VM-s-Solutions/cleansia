package cz.cleansia.partner.features.devices

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.Devices
import androidx.compose.material.icons.outlined.Laptop
import androidx.compose.material.icons.outlined.PhoneIphone
import androidx.compose.material.icons.outlined.Smartphone
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.core.devices.UserDeviceDto
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

/**
 * "Your devices" self-service — lists every handset registered to the
 * account, flags the one the cleaner is holding, and lets them revoke any
 * OTHER device (kills its push registration and its session). Read path is
 * GET /api/Device/Mine; revoke is DELETE /api/Device/{rowId}.
 */
@Composable
fun DevicesScreen(
    onNavigateBack: () -> Unit,
    viewModel: DevicesViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val revokeState by viewModel.revokeState.collectAsStateWithLifecycle()
    var deviceToRevoke by remember { mutableStateOf<UserDeviceDto?>(null) }

    LaunchedEffect(viewModel) { viewModel.revoked.collect { deviceToRevoke = null } }

    DevicesScreenContent(
        state = state,
        revokeState = revokeState,
        deviceToRevoke = deviceToRevoke,
        onNavigateBack = onNavigateBack,
        onRetry = viewModel::load,
        onRevokeRequested = { deviceToRevoke = it },
        onRevokeConfirmed = { viewModel.revoke(it.id) },
        onRevokeDismissed = { deviceToRevoke = null },
    )
}

@Composable
fun DevicesScreenContent(
    state: DevicesUiState,
    revokeState: ActionState,
    deviceToRevoke: UserDeviceDto?,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    onRevokeRequested: (UserDeviceDto) -> Unit,
    onRevokeConfirmed: (UserDeviceDto) -> Unit,
    onRevokeDismissed: () -> Unit,
) {
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Spacer(Modifier.height(statusBarTop))
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = Spacing.XS, vertical = Spacing.XS),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onNavigateBack) {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.back),
                    tint = MaterialTheme.colorScheme.onBackground,
                )
            }
            Spacer(Modifier.width(Spacing.S))
            Text(
                text = stringResource(R.string.devices_title),
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onBackground,
            )
        }

        when (state) {
            DevicesUiState.Loading -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
            DevicesUiState.Error -> ErrorState(onRetry = onRetry)
            is DevicesUiState.Loaded -> {
                if (state.devices.isEmpty()) {
                    EmptyState()
                } else {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(
                            start = Spacing.M,
                            end = Spacing.M,
                            top = Spacing.S,
                            bottom = Spacing.L,
                        ),
                        verticalArrangement = Arrangement.spacedBy(Spacing.M),
                    ) {
                        item(key = "intro") {
                            Text(
                                text = stringResource(R.string.devices_intro),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        items(state.devices, key = { it.id }) { device ->
                            DeviceCard(
                                device = device,
                                onRevoke = { onRevokeRequested(device) },
                            )
                        }
                    }
                }
            }
        }
    }

    deviceToRevoke?.let { device ->
        CleansiaDialog(
            onDismiss = onRevokeDismissed,
            title = stringResource(R.string.devices_revoke_dialog_title),
            message = stringResource(R.string.devices_revoke_dialog_message, platformLabel(device.platform)),
            icon = Icons.AutoMirrored.Outlined.Logout,
            destructive = true,
            confirmLabel = stringResource(R.string.devices_revoke_dialog_confirm),
            confirmEnabled = revokeState !is ActionState.Submitting,
            onConfirm = { onRevokeConfirmed(device) },
            dismissLabel = stringResource(R.string.cancel),
            content = (revokeState as? ActionState.Error)?.let { error ->
                {
                    Text(
                        text = error.message,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            },
        )
    }
}

@Composable
private fun DeviceCard(device: UserDeviceDto, onRevoke: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(Spacing.L),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconHalo(icon = platformIcon(device.platform))
        Spacer(Modifier.width(Spacing.M))
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = platformLabel(device.platform),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                if (device.isCurrent) {
                    Spacer(Modifier.width(Spacing.S))
                    CurrentDeviceChip()
                }
            }
            formatLastActive(device.lastActiveAt)?.let { lastActive ->
                Spacer(Modifier.height(2.dp))
                Text(
                    text = stringResource(R.string.devices_last_active, lastActive),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        if (!device.isCurrent) {
            IconButton(onClick = onRevoke) {
                Icon(
                    imageVector = Icons.Outlined.DeleteOutline,
                    contentDescription = stringResource(R.string.devices_revoke_action),
                    tint = MaterialTheme.colorScheme.error,
                )
            }
        }
    }
}

@Composable
private fun CurrentDeviceChip() {
    Box(
        modifier = Modifier
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primaryContainer)
            .padding(horizontal = 10.dp, vertical = 3.dp),
    ) {
        Text(
            text = stringResource(R.string.devices_this_device),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
    }
}

@Composable
private fun IconHalo(icon: ImageVector) {
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
            imageVector = Icons.Outlined.Devices,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(Spacing.M))
        Text(
            text = stringResource(R.string.devices_error_message),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.L))
        CleansiaPrimaryButton(
            text = stringResource(R.string.retry),
            onClick = onRetry,
        )
    }
}

@Composable
private fun EmptyState() {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Text(
            text = stringResource(R.string.devices_empty),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun platformLabel(platform: String?): String = when (platform?.lowercase()) {
    "android" -> stringResource(R.string.devices_platform_android)
    "ios" -> stringResource(R.string.devices_platform_ios)
    "web" -> stringResource(R.string.devices_platform_web)
    else -> platform ?: stringResource(R.string.no_data)
}

private fun platformIcon(platform: String?): ImageVector = when (platform?.lowercase()) {
    "android" -> Icons.Outlined.Smartphone
    "ios" -> Icons.Outlined.PhoneIphone
    "web" -> Icons.Outlined.Laptop
    else -> Icons.Outlined.Devices
}

private val lastActiveFormatter = DateTimeFormatter.ofPattern("d MMM yyyy · HH:mm", Locale.getDefault())

private fun formatLastActive(iso: String?): String? {
    if (iso.isNullOrBlank()) return null
    return runCatching {
        ZonedDateTime.parse(iso)
            .withZoneSameInstant(ZoneId.systemDefault())
            .format(lastActiveFormatter)
    }.getOrNull()
        ?: runCatching { LocalDate.parse(iso).toString() }.getOrNull()
}
