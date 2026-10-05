package cz.cleansia.customer.features.profile

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
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.Devices
import androidx.compose.material.icons.outlined.Laptop
import androidx.compose.material.icons.outlined.PhoneIphone
import androidx.compose.material.icons.outlined.Smartphone
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
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.core.ui.theme.primaryIconOnContainer
import cz.cleansia.core.ui.theme.primaryTextOnContainer
import cz.cleansia.customer.R
import cz.cleansia.customer.core.devices.UserDeviceDto
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.customer.ui.theme.CleansiaTheme
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

/**
 * "Your devices" self-service — lists every device registered to the
 * account, flags the one the user is holding, and lets them revoke any of
 * them (kills its push registration and its session). Revoking the current
 * device doubles as an instant sign-out. Read path is GET /api/Device/Mine;
 * revoke is DELETE /api/Device/{rowId}.
 */
@Composable
fun DevicesScreen(
    onBack: () -> Unit = {},
    viewModel: DevicesViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val revokeState by viewModel.revokeState.collectAsStateWithLifecycle()
    var deviceToRevoke by remember { mutableStateOf<UserDeviceDto?>(null) }
    // The device a confirmed revoke is running for, so its row can show the spinner the dialog used to.
    var revokingId by remember { mutableStateOf<String?>(null) }

    DevicesScreenContent(
        state = state,
        revokingId = revokingId.takeIf { revokeState is ActionState.Submitting },
        onBack = onBack,
        onRetry = viewModel::load,
        onRevokeRequested = { deviceToRevoke = it },
    )

    // Self-revoke gets distinct copy: revoking THIS device signs you out now. The system confirm closes
    // on the tap; the row shows the revoke running and a refusal is the snackbar's.
    deviceToRevoke?.let { device ->
        AlertDialog(
            onDismissRequest = { deviceToRevoke = null },
            title = {
                Text(
                    stringResource(
                        if (device.isCurrent) R.string.devices_self_revoke_dialog_title else R.string.devices_revoke_dialog_title,
                    ),
                )
            },
            text = {
                Text(
                    if (device.isCurrent) {
                        stringResource(R.string.devices_self_revoke_dialog_message)
                    } else {
                        stringResource(R.string.devices_revoke_dialog_message, platformLabel(device.platform))
                    },
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        deviceToRevoke = null
                        revokingId = device.id
                        viewModel.revoke(device)
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) {
                    Text(
                        stringResource(
                            if (device.isCurrent) R.string.devices_self_revoke_dialog_confirm else R.string.devices_revoke_dialog_confirm,
                        ),
                    )
                }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { deviceToRevoke = null }) { Text(stringResource(R.string.common_cancel)) }
            },
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DevicesScreenContent(
    state: DevicesUiState,
    revokingId: String?,
    onBack: () -> Unit = {},
    onRetry: () -> Unit = {},
    onRevokeRequested: (UserDeviceDto) -> Unit = {},
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        TopAppBar(
            title = {
                Text(
                    stringResource(R.string.devices_title),
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
            DevicesUiState.Loading -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                }
            }
            DevicesUiState.Error -> ErrorState(onRetry = onRetry)
            is DevicesUiState.Loaded -> {
                if (state.devices.isEmpty()) {
                    EmptyState()
                } else {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(20.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
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
                                revoking = revokingId == device.id,
                                canRevoke = revokingId == null,
                                onRevoke = { onRevokeRequested(device) },
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DeviceCard(
    device: UserDeviceDto,
    revoking: Boolean,
    // One revoke at a time: the view model drops a second while the first is in flight.
    canRevoke: Boolean,
    onRevoke: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = platformIcon(device.platform),
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primaryIconOnContainer,
                modifier = Modifier.size(22.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = platformLabel(device.platform),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                if (device.isCurrent) {
                    Spacer(Modifier.width(8.dp))
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
        // The current device is revocable too: it reads as "sign out this device now"
        // (distinct icon + dialog copy) and ends the session at 0s instead of leaving a zombie
        // session for the ≤30s revocation directory to catch.
        if (revoking) {
            Box(Modifier.size(48.dp), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(
                    modifier = Modifier.size(22.dp),
                    strokeWidth = 2.dp,
                    color = MaterialTheme.colorScheme.error,
                )
            }
        } else {
            IconButton(onClick = onRevoke, enabled = canRevoke) {
                Icon(
                    imageVector = if (device.isCurrent) {
                        Icons.AutoMirrored.Outlined.Logout
                    } else {
                        Icons.Outlined.DeleteOutline
                    },
                    contentDescription = stringResource(
                        if (device.isCurrent) R.string.devices_self_revoke_action else R.string.devices_revoke_action,
                    ),
                    tint = MaterialTheme.colorScheme.error.copy(alpha = if (canRevoke) 1f else 0.4f),
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
            color = MaterialTheme.colorScheme.primaryTextOnContainer,
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
            text = stringResource(R.string.devices_error_message),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.devices_error_retry),
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
    else -> platform ?: stringResource(R.string.devices_platform_unknown)
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

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun DevicesPreview() {
    CleansiaTheme {
        DevicesScreenContent(
            state = DevicesUiState.Loaded(
                listOf(
                    UserDeviceDto(
                        id = "row-1",
                        platform = "android",
                        deviceId = "abc",
                        lastActiveAt = "2026-06-10T08:00:00+00:00",
                        isCurrent = true,
                    ),
                    UserDeviceDto(
                        id = "row-2",
                        platform = "ios",
                        deviceId = "def",
                        lastActiveAt = "2026-06-01T08:00:00+00:00",
                        isCurrent = false,
                    ),
                ),
            ),
            revokingId = null,
        )
    }
}
