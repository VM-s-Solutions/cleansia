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
import androidx.compose.material.icons.automirrored.outlined.Chat
import androidx.compose.material.icons.outlined.Cancel
import androidx.compose.material.icons.outlined.CardMembership
import androidx.compose.material.icons.outlined.CreditCardOff
import androidx.compose.material.icons.outlined.Email
import androidx.compose.material.icons.outlined.MilitaryTech
import androidx.compose.material.icons.outlined.NotificationsActive
import androidx.compose.material.icons.outlined.Receipt
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
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.core.notifications.NotificationCategoryDto
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.ui.theme.Poppins

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NotificationsScreen(
    onBack: () -> Unit = {},
    viewModel: NotificationPreferencesViewModel = hiltViewModel(),
) {
    // Backend-backed push toggles. Map UI labels → NotificationCategoryDto:
    //  - bookingUpdates → OrderUpdates (Confirmed/InProgress)
    //  - cleanerMessages → CleanerOnTheWay
    //  - reminders → RecurringScheduled
    //  - reviews → OrderCompleted (review CTA fires on completion)
    //  - promos → Promo
    val preferences by viewModel.preferences.collectAsStateWithLifecycle()
    val bookingUpdates = preferences?.orderUpdates ?: true
    val cleanerMessages = preferences?.cleanerOnTheWay ?: true
    val reminders = preferences?.recurringScheduled ?: true
    val reviews = preferences?.orderCompleted ?: true
    val orderCancelled = preferences?.orderCancelled ?: true
    val refundIssued = preferences?.refundIssued ?: true
    val promos = preferences?.promo ?: false
    val membershipExpiring = preferences?.membershipExpiring ?: true
    val membershipCancelled = preferences?.membershipCancelled ?: true
    val tierUpgrade = preferences?.tierUpgrade ?: true
    val disputeReply = preferences?.disputeReply ?: true

    // Email/SMS aren't push categories — backend doesn't model them and we
    // don't currently send SMS at all. Kept as local-only toggles so the
    // UI shape stays the same; replace with their own VM if/when backend
    // adds these channels.
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
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.OrderUpdates, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.TrackChanges,
                    title = stringResource(R.string.notifications_cleaner_messages),
                    subtitle = stringResource(R.string.notifications_cleaner_messages_desc),
                    checked = cleanerMessages,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.CleanerOnTheWay, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.NotificationsActive,
                    title = stringResource(R.string.notifications_reminders),
                    subtitle = stringResource(R.string.notifications_reminders_desc),
                    checked = reminders,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.RecurringScheduled, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.Star,
                    title = stringResource(R.string.notifications_review_requests),
                    subtitle = stringResource(R.string.notifications_review_requests_desc),
                    checked = reviews,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.OrderCompleted, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.Cancel,
                    title = stringResource(R.string.notifications_order_cancelled),
                    subtitle = stringResource(R.string.notifications_order_cancelled_desc),
                    checked = orderCancelled,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.OrderCancelled, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.Star,
                    title = stringResource(R.string.notifications_promos),
                    subtitle = stringResource(R.string.notifications_promos_desc),
                    checked = promos,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.Promo, it) },
                )
            }
            Spacer(Modifier.height(16.dp))

            SectionCard(stringResource(R.string.notifications_section_membership)) {
                ToggleRow(
                    icon = Icons.Outlined.MilitaryTech,
                    title = stringResource(R.string.notifications_tier_upgrade),
                    subtitle = stringResource(R.string.notifications_tier_upgrade_desc),
                    checked = tierUpgrade,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.TierUpgrade, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.CardMembership,
                    title = stringResource(R.string.notifications_membership_expiring),
                    subtitle = stringResource(R.string.notifications_membership_expiring_desc),
                    checked = membershipExpiring,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.MembershipExpiring, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.Outlined.CreditCardOff,
                    title = stringResource(R.string.notifications_membership_cancelled),
                    subtitle = stringResource(R.string.notifications_membership_cancelled_desc),
                    checked = membershipCancelled,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.MembershipCancelled, it) },
                )
            }
            Spacer(Modifier.height(16.dp))

            SectionCard(stringResource(R.string.notifications_section_account)) {
                ToggleRow(
                    icon = Icons.Outlined.Receipt,
                    title = stringResource(R.string.notifications_refund_issued),
                    subtitle = stringResource(R.string.notifications_refund_issued_desc),
                    checked = refundIssued,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.RefundIssued, it) },
                )
                RowDivider()
                ToggleRow(
                    icon = Icons.AutoMirrored.Outlined.Chat,
                    title = stringResource(R.string.notifications_dispute_reply),
                    subtitle = stringResource(R.string.notifications_dispute_reply_desc),
                    checked = disputeReply,
                    onCheckedChange = { viewModel.setCategory(NotificationCategoryDto.DisputeReply, it) },
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

// @Preview removed — NotificationsScreen now requires a Hilt ViewModel,
// which the @Preview harness can't provide. Re-add with a fake VM
// constructor + parameter override if previews become important again.
