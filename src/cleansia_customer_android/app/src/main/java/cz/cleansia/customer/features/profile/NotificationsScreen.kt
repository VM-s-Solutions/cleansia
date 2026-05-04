package cz.cleansia.customer.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Email
import androidx.compose.material.icons.outlined.NotificationsActive
import androidx.compose.material.icons.outlined.Sms
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material.icons.outlined.TrackChanges
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
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
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.Poppins

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NotificationsScreen(onBack: () -> Unit = {}) {
    var bookingUpdates by remember { mutableStateOf(true) }
    var cleanerMessages by remember { mutableStateOf(true) }
    var reminders by remember { mutableStateOf(true) }
    var promos by remember { mutableStateOf(false) }
    var reviews by remember { mutableStateOf(true) }
    var emailUpdates by remember { mutableStateOf(true) }
    var smsUpdates by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        TopAppBar(
            title = { Text(stringResource(R.string.notifications_title), style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold)) },
            navigationIcon = {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back)) }
            },
            colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.surface),
        )

        Column(
            modifier = Modifier
                .verticalScroll(rememberScrollState())
                .padding(20.dp),
        ) {
            SectionCard(stringResource(R.string.notifications_section_push)) {
                ToggleRow(
                    icon = Icons.Outlined.NotificationsActive,
                    title = stringResource(R.string.notifications_booking_updates),
                    subtitle = stringResource(R.string.notifications_booking_updates_desc),
                    checked = bookingUpdates,
                    onCheckedChange = { bookingUpdates = it },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.TrackChanges,
                    title = stringResource(R.string.notifications_cleaner_messages),
                    subtitle = stringResource(R.string.notifications_cleaner_messages_desc),
                    checked = cleanerMessages,
                    onCheckedChange = { cleanerMessages = it },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.NotificationsActive,
                    title = stringResource(R.string.notifications_reminders),
                    subtitle = stringResource(R.string.notifications_reminders_desc),
                    checked = reminders,
                    onCheckedChange = { reminders = it },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.Star,
                    title = stringResource(R.string.notifications_review_requests),
                    subtitle = stringResource(R.string.notifications_review_requests_desc),
                    checked = reviews,
                    onCheckedChange = { reviews = it },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.Star,
                    title = stringResource(R.string.notifications_promos),
                    subtitle = stringResource(R.string.notifications_promos_desc),
                    checked = promos,
                    onCheckedChange = { promos = it },
                )
            }
            Spacer(Modifier.height(16.dp))

            SectionCard(stringResource(R.string.notifications_section_channels)) {
                ToggleRow(
                    icon = Icons.Outlined.Email,
                    title = stringResource(R.string.notifications_email),
                    subtitle = stringResource(R.string.notifications_email_desc),
                    checked = emailUpdates,
                    onCheckedChange = { emailUpdates = it },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.Sms,
                    title = stringResource(R.string.notifications_sms),
                    subtitle = stringResource(R.string.notifications_sms_desc),
                    checked = smsUpdates,
                    onCheckedChange = { smsUpdates = it },
                )
            }
            Spacer(Modifier.height(32.dp))
        }
    }
}

@Composable
private fun SectionCard(title: String, content: @Composable () -> Unit) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(
            title,
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(start = 4.dp, bottom = 8.dp),
        )
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(16.dp))
                .background(MaterialTheme.colorScheme.surface)
                .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp)),
        ) {
            content()
        }
    }
}

@Composable
private fun ToggleRow(
    icon: ImageVector,
    title: String,
    subtitle: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(36.dp)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(icon, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(18.dp))
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onSurface)
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Switch(
            checked = checked,
            onCheckedChange = onCheckedChange,
            colors = SwitchDefaults.colors(
                checkedThumbColor = MaterialTheme.colorScheme.onPrimary,
                checkedTrackColor = MaterialTheme.colorScheme.primary,
            ),
        )
    }
}

@Composable
private fun RowDivider() {
    HorizontalDivider(
        modifier = Modifier.padding(start = 62.dp),
        color = MaterialTheme.colorScheme.outlineVariant,
    )
}

@Preview(widthDp = 390, heightDp = 900)
@Composable
private fun NotificationsPreview() {
    CleansiaTheme { NotificationsScreen() }
}
