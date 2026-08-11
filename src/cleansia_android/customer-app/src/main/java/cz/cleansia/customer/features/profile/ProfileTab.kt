package cz.cleansia.customer.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForwardIos
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.DarkMode
import androidx.compose.material.icons.outlined.DeleteForever
import androidx.compose.material.icons.outlined.Devices
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material.icons.outlined.Gavel
import androidx.compose.material.icons.outlined.HelpOutline
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.WorkspacePremium
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.customer.ui.theme.BrandGradients
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.customer.ui.theme.Sky600
import cz.cleansia.customer.ui.theme.asList
import kotlinx.datetime.toJavaLocalDateTime
import kotlinx.datetime.toLocalDateTime

private data class ProfileRow(
    val key: String,
    val icon: ImageVector,
    val labelRes: Int,
)

/**
 * Wolt/Bolt-style profile — tall gradient hero with rounded bottom corners,
 * big avatar + name + tier badge + edit pill CTA. A stats card
 * overlaps the hero bottom. Settings below are grouped into rounded sections.
 */
@Composable
fun ProfileTab(
    modifier: Modifier = Modifier,
    user: CurrentUser? = null,
    isPlus: Boolean = false,
    onLogout: () -> Unit = {},
    onRowClick: (key: String) -> Unit = {},
    onAvatarLoadFailed: () -> Unit = {},
    onAvatarLoadSucceeded: () -> Unit = {},
) {
    // Fall back to placeholders while the first /GetCurrent call is in flight
    // (or in Compose previews). The blanks render as empty initials / empty email,
    // which matches a skeleton-style loading treatment without flashing fake data.
    val firstName = user?.firstName ?: ""
    val lastName = user?.lastName ?: ""
    val email = user?.email ?: ""
    val tier = stringResource(if (isPlus) R.string.profile_tier_plus else R.string.profile_tier_regular)
    val totalBookings = user?.totalBookings ?: 0
    val savedDisplay = formatSaved(user?.totalSavings, user?.savingsCurrencyCode)
    val memberSince = formatMemberSince(user?.memberSince)

    val accountRows = listOf(
        ProfileRow("addresses", Icons.Outlined.Home, R.string.profile_row_addresses),
        // "Disputes" opens the My Disputes list (Wave 2 Phase 6). Gavel is
        // the canonical formal-complaint glyph in material-icons-extended.
        ProfileRow("disputes", Icons.Outlined.Gavel, R.string.profile_row_disputes),
    )
    val preferencesRows = listOf(
        ProfileRow("notifications", Icons.Outlined.NotificationsNone, R.string.profile_row_notifications),
        ProfileRow("appearance", Icons.Outlined.DarkMode, R.string.profile_row_appearance),
        ProfileRow("language", Icons.Outlined.Language, R.string.profile_row_language),
        ProfileRow("security", Icons.Outlined.Lock, R.string.profile_row_security),
        ProfileRow("devices", Icons.Outlined.Devices, R.string.profile_row_devices),
    )
    val supportRows = listOf(
        ProfileRow("help", Icons.Outlined.HelpOutline, R.string.profile_row_help),
    )

    // Logout confirm — destructive, irreversible from the user's POV (kicks
    // back to SignIn + clears tokens). The "Log out" row only flips the flag;
    // the actual `onLogout()` callback fires after the user confirms.
    var showLogoutDialog by remember { mutableStateOf(false) }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .verticalScroll(rememberScrollState()),
    ) {
        // 1. Hero + stats card (stats overlap the hero's bottom edge)
        Box {
            ProfileHero(
                firstName = firstName,
                lastName = lastName,
                email = email,
                tier = tier,
                photo = avatarPhotoFor(user, AvatarDraft.Unchanged),
                onEditClick = { onRowClick("edit") },
                onAvatarLoadFailed = onAvatarLoadFailed,
                onAvatarLoadSucceeded = onAvatarLoadSucceeded,
            )
            // Stats card sits on the hero's curved lip, half in half out.
            Box(
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .offset(y = 40.dp)
                    .padding(horizontal = 20.dp),
            ) {
                StatsCard(
                    totalBookings = totalBookings,
                    saved = savedDisplay,
                    memberSince = memberSince,
                )
            }
        }

        // Spacer absorbs the overlap height
        Spacer(Modifier.height(56.dp))

        // Cleansia Plus card — high-value upsell when inactive, status +
        // cancel action when active. Renders nothing on first composition
        // until /api/Membership/GetMine resolves (avoids a stale-state flash).
        cz.cleansia.customer.features.membership.MembershipManagementCard(
            modifier = Modifier.padding(horizontal = 20.dp),
            onSubscribeClick = { onRowClick("subscribe_plus") },
        )
        Spacer(Modifier.height(12.dp))

        // Recurring bookings entry — always reachable: a lapsed membership does
        // not stop a schedule, so this is the route to the stop button.
        cz.cleansia.customer.features.profile.RecurringEntryRow(
            modifier = Modifier.padding(horizontal = 20.dp),
            onClick = { onRowClick("recurring_bookings") },
        )
        Spacer(Modifier.height(18.dp))

        // 2. Account (only saved addresses now — "Edit profile" moved into the hero)
        SettingsSection(rows = accountRows, onClick = onRowClick)
        Spacer(Modifier.height(18.dp))

        // 3. Preferences
        SettingsSection(
            title = stringResource(R.string.profile_section_preferences),
            rows = preferencesRows,
            onClick = onRowClick,
        )
        Spacer(Modifier.height(18.dp))

        // 4. Support
        SettingsSection(
            title = stringResource(R.string.profile_section_support),
            rows = supportRows,
            onClick = onRowClick,
        )

        Spacer(Modifier.height(28.dp))

        LogoutRow(onLogout = { showLogoutDialog = true })

        Spacer(Modifier.height(10.dp))

        DeleteAccountRow(onClick = { onRowClick("delete_account") })

        Spacer(Modifier.height(20.dp))

        Text(
            stringResource(R.string.profile_app_version),
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 20.dp),
            textAlign = TextAlign.Center,
        )
        // Reserve room for the floating island bottom nav.
        Spacer(Modifier.height(108.dp))
    }

    if (showLogoutDialog) {
        CleansiaDialog(
            onDismiss = { showLogoutDialog = false },
            title = stringResource(R.string.profile_logout_dialog_title),
            message = stringResource(R.string.profile_logout_dialog_message),
            icon = Icons.AutoMirrored.Outlined.Logout,
            destructive = true,
            confirmLabel = stringResource(R.string.profile_logout_dialog_confirm),
            onConfirm = {
                showLogoutDialog = false
                onLogout()
            },
            dismissLabel = stringResource(R.string.profile_logout_dialog_cancel),
        )
    }
}

// 0.45 is the largest fraction that still leaves the name column 56dp at 320dp. Nothing sets a lower
// bound now that the label is a verb alone: en needs 0.26, ru needs 0.54 — more than the name can
// spare — so ru ellipsizes below ~376dp by design.
private const val EditChipMaxWidthFraction = 0.45f

// The gradient runs edge to edge under the status bar and the row starts this far below it, matching
// iOS's `.padding(.top, 48 + topInset)` on the same hero.
private val HeroContentTopPadding = 48.dp

@Composable
private fun ProfileHero(
    firstName: String,
    lastName: String,
    email: String,
    tier: String,
    onEditClick: () -> Unit,
    photo: AvatarPhoto? = null,
    onAvatarLoadFailed: () -> Unit = {},
    onAvatarLoadSucceeded: () -> Unit = {},
) {
    val initials = "${firstName.firstOrNull() ?: ""}${lastName.firstOrNull() ?: ""}"
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .background(brush = Brush.verticalGradient(BrandGradients.blue().asList()))
            .padding(
                start = Spacing.ML,
                end = Spacing.ML,
                top = statusBarTop + HeroContentTopPadding,
                bottom = Spacing.XXL,
            ),
    ) {
        // RowScope.weight only divides what is left AFTER unweighted children are measured, so an
        // unbounded chip starves the name column to nothing once the label is long (ru/uk). Capping
        // the chip keeps the name's share; the chip ellipsizes instead.
        val editChipMaxWidth = maxWidth * EditChipMaxWidthFraction

        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
            Box(
                modifier = Modifier
                    .size(72.dp)
                    .background(Color.White, CircleShape)
                    .border(3.dp, Color.White.copy(alpha = 0.35f), CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                ProfileAvatarContent(
                    initials = initials,
                    // The circle is fixed white in both themes, so the initials pin the light-mode
                    // brand blue; the theme-adaptive primary drops to 2.1:1 against it in dark.
                    initialsStyle = MaterialTheme.typography.headlineSmall,
                    initialsColor = Sky600,
                    photo = photo,
                    onLoadFailed = onAvatarLoadFailed,
                    onLoadSucceeded = onAvatarLoadSucceeded,
                )
            }
            Spacer(Modifier.width(14.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    "$firstName $lastName",
                    style = MaterialTheme.typography.headlineSmall,
                    color = Color.White,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                if (email.isNotEmpty()) {
                    Spacer(Modifier.height(Spacing.Hair))
                    Text(
                        email,
                        style = MaterialTheme.typography.bodyMedium,
                        color = Color.White.copy(alpha = 0.85f),
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                Spacer(Modifier.height(6.dp))
                TierBadge(tier)
            }
            Spacer(Modifier.width(Spacing.S))
            // Deliberate mixed alignment: avatar and text stay top-anchored while the chip
            // centres against the full row height.
            EditProfileChip(
                onClick = onEditClick,
                modifier = Modifier
                    .align(Alignment.CenterVertically)
                    .widthIn(max = editChipMaxWidth),
            )
        }
    }
}

@Composable
private fun EditProfileChip(onClick: () -> Unit, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier
            .clip(RoundedCornerShape(999.dp))
            .background(Color.White.copy(alpha = 0.22f))
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = Spacing.XS),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Outlined.Edit,
            null,
            tint = Color.White,
            modifier = Modifier.size(14.dp),
        )
        Spacer(Modifier.width(6.dp))
        Text(
            stringResource(R.string.profile_row_edit),
            style = MaterialTheme.typography.labelLarge,
            color = Color.White,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun TierBadge(tier: String) {
    Row(
        modifier = Modifier
            .background(Color.White.copy(alpha = 0.22f), RoundedCornerShape(999.dp))
            .padding(horizontal = Spacing.XS, vertical = 3.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Outlined.WorkspacePremium,
            null,
            tint = Color.White,
            modifier = Modifier.size(12.dp),
        )
        Spacer(Modifier.width(Spacing.XXS))
        Text(
            tier,
            style = MaterialTheme.typography.labelSmall,
            color = Color.White,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
        )
    }
}

/* ── Floating stats card — overlaps the hero bottom lip ── */

/** "%.0f Kč" style, mirroring the booking total formatter; symbol-less when the
 *  user has no realized orders (currency null). */
/**
 * Null is "no profile loaded yet", the same skeleton state the blank name and email above render. It
 * can no longer be a dropped `totalSavings`: [CurrentUser] refuses that at the wire rather than
 * telling a customer with 4 800 Kc of savings that they have saved nothing.
 */
internal fun formatSaved(amount: Double?, currencyCode: String?): String {
    if (amount == null) return "\u2014"
    val symbol = when (currencyCode?.uppercase()) {
        "CZK" -> "Kč"
        "EUR" -> "€"
        "USD" -> "$"
        null -> null
        else -> currencyCode
    }
    return if (symbol == null) "%.0f".format(amount) else "%.0f %s".format(amount, symbol)
}

/** Account-creation instant → "MMM yyyy" (e.g. "Feb 2025"); em dash if unknown. */
internal fun formatMemberSince(instant: kotlinx.datetime.Instant?): String =
    instant
        ?.toLocalDateTime(kotlinx.datetime.TimeZone.currentSystemDefault())
        ?.toJavaLocalDateTime()
        ?.format(java.time.format.DateTimeFormatter.ofPattern("MMM yyyy", java.util.Locale.getDefault()))
        ?: "—"

@Composable
private fun StatsCard(totalBookings: Int, saved: String, memberSince: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .shadow(elevation = 12.dp, shape = RoundedCornerShape(18.dp), clip = false)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(vertical = 16.dp, horizontal = 12.dp)
            .height(androidx.compose.foundation.layout.IntrinsicSize.Max),
        horizontalArrangement = Arrangement.SpaceEvenly,
    ) {
        StatItem(
            value = totalBookings.toString(),
            label = stringResource(R.string.profile_stat_bookings),
            modifier = Modifier.weight(1f).fillMaxHeight(),
        )
        StatDivider()
        StatItem(
            value = saved,
            label = stringResource(R.string.profile_stat_saved),
            modifier = Modifier.weight(1f).fillMaxHeight(),
        )
        StatDivider()
        StatItem(
            value = memberSince,
            label = stringResource(R.string.profile_stat_member_since),
            modifier = Modifier.weight(1f).fillMaxHeight(),
        )
    }
}

@Composable
private fun StatItem(value: String, label: String, modifier: Modifier) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(
            value,
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold, fontFamily = Poppins),
            color = MaterialTheme.colorScheme.onBackground,
            maxLines = 1,
        )
        Spacer(Modifier.height(2.dp))
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            maxLines = 2,
            overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun StatDivider() {
    Box(
        Modifier
            .padding(horizontal = 4.dp)
            .width(1.dp)
            .height(32.dp)
            .background(MaterialTheme.colorScheme.outlineVariant),
    )
}

/* ── Settings section: optional title + rounded card with rows ── */

@Composable
private fun SettingsSection(
    title: String? = null,
    rows: List<ProfileRow>,
    onClick: (key: String) -> Unit,
) {
    Column(modifier = Modifier.padding(horizontal = 20.dp)) {
        if (title != null) {
            Text(
                title,
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(start = 4.dp, bottom = 10.dp),
            )
        }
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(18.dp))
                .background(MaterialTheme.colorScheme.surface),
        ) {
            rows.forEachIndexed { idx, row ->
                SettingsRow(row = row, onClick = { onClick(row.key) })
                if (idx < rows.lastIndex) {
                    HorizontalDivider(
                        modifier = Modifier.padding(start = 56.dp),
                        color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.6f),
                    )
                }
            }
        }
    }
}

@Composable
private fun SettingsRow(row: ProfileRow, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(32.dp)
                .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                row.icon,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
        }
        Spacer(Modifier.width(14.dp))
        Text(
            stringResource(row.labelRes),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        Icon(
            Icons.AutoMirrored.Outlined.ArrowForwardIos,
            null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(14.dp),
        )
    }
}

/* ── Logout — slightly different treatment to signal destructive action ── */

@Composable
private fun LogoutRow(onLogout: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .clickable(onClick = onLogout)
            .padding(horizontal = 16.dp, vertical = 16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(32.dp)
                .background(MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.AutoMirrored.Outlined.Logout,
                null,
                tint = MaterialTheme.colorScheme.error,
                modifier = Modifier.size(18.dp),
            )
        }
        Spacer(Modifier.width(14.dp))
        Text(
            stringResource(R.string.profile_logout),
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.error,
            modifier = Modifier.weight(1f),
        )
    }
}

/* ── Delete account — subtle outlined treatment below Logout. GDPR-required. ── */

@Composable
private fun DeleteAccountRow(onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(32.dp)
                .background(MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.DeleteForever,
                null,
                tint = MaterialTheme.colorScheme.error,
                modifier = Modifier.size(18.dp),
            )
        }
        Spacer(Modifier.width(14.dp))
        Text(
            stringResource(R.string.profile_delete_account),
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.error,
            modifier = Modifier.weight(1f),
        )
    }
}

@Preview(widthDp = 411, heightDp = 1100)
@Composable
private fun ProfileTabPreview() {
    CleansiaTheme { ProfileTab() }
}

@Preview(widthDp = 411)
@Composable
private fun ProfileHeroPreview() {
    CleansiaTheme {
        ProfileHero(
            firstName = "Jane",
            lastName = "Doe",
            email = "jane@example.com",
            tier = stringResource(R.string.profile_tier_regular),
            onEditClick = {},
        )
    }
}

@Preview(widthDp = 320)
@Composable
private fun ProfileHeroLongNamePreview() {
    CleansiaTheme {
        ProfileHero(
            firstName = "Bartholomew-Maximilian",
            lastName = "Fitzgerald-Wellington",
            email = "bartholomew.fitzgerald.wellington@verylongdomainexample.co.uk",
            tier = stringResource(R.string.profile_tier_plus),
            onEditClick = {},
        )
    }
}

@Preview(widthDp = 390)
@Composable
private fun ProfileHeroNoEmailPreview() {
    CleansiaTheme {
        ProfileHero(
            firstName = "Jane",
            lastName = "Doe",
            email = "",
            tier = stringResource(R.string.profile_tier_regular),
            onEditClick = {},
        )
    }
}

@Preview(locale = "ru", widthDp = 320)
@Composable
private fun ProfileHeroRussianPreview() {
    CleansiaTheme {
        ProfileHero(
            firstName = "Александр",
            lastName = "Иванов",
            email = "aleksandr.ivanov@example.com",
            tier = stringResource(R.string.profile_tier_regular),
            onEditClick = {},
        )
    }
}

@Preview(locale = "uk", widthDp = 320)
@Composable
private fun ProfileHeroUkrainianNarrowPreview() {
    CleansiaTheme {
        ProfileHero(
            firstName = "Володимир",
            lastName = "Ковальчук",
            email = "volodymyr.kovalchuk@example.com",
            tier = stringResource(R.string.profile_tier_regular),
            onEditClick = {},
        )
    }
}
