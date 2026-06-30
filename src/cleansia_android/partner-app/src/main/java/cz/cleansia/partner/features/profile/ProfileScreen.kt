package cz.cleansia.partner.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.AccountBalance
import androidx.compose.material.icons.outlined.Badge
import androidx.compose.material.icons.outlined.DarkMode
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Devices
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.core.settings.ThemePreference
import cz.cleansia.partner.features.main.MainBottomNavInset
import cz.cleansia.partner.features.settings.SettingsViewModel

@Composable
fun ProfileScreen(
    onNavigateBack: () -> Unit,
    onNavigateToPersonal: () -> Unit,
    onNavigateToAddress: () -> Unit,
    onNavigateToIdentification: () -> Unit,
    onNavigateToBank: () -> Unit,
    onNavigateToEmergency: () -> Unit,
    onNavigateToDocuments: () -> Unit,
    onNavigateToLanguage: () -> Unit,
    onNavigateToTheme: () -> Unit,
    onNavigateToDevices: () -> Unit,
    onSignedOut: () -> Unit,
    viewModel: ProfileViewModel = hiltViewModel(),
    settingsViewModel: SettingsViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val settings by settingsViewModel.settings.collectAsStateWithLifecycle()
    val settingsUi by settingsViewModel.uiState.collectAsStateWithLifecycle()

    LaunchedEffect(viewModel) { viewModel.signedOut.collect { onSignedOut() } }
    LaunchedEffect(settingsUi.isSignedOut) {
        if (settingsUi.isSignedOut) onSignedOut()
    }

    // Logout confirm — destructive, irreversible from the user's POV (kicks
    // back to SignIn + clears tokens). The LogoutRow only flips the flag; the
    // actual `viewModel.signOut()` call fires after the user confirms. Forced
    // sign-out flows (e.g. token revocation from SettingsViewModel) bypass
    // this dialog and route straight through `onSignedOut` above.
    var showLogoutDialog by remember { mutableStateOf(false) }

    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    when (val s = uiState) {
        ProfileUiState.Loading -> {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }
        }
        is ProfileUiState.Loaded -> {
            val employee = s.employee
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background),
                contentPadding = PaddingValues(bottom = MainBottomNavInset),
                verticalArrangement = Arrangement.spacedBy(Spacing.M),
            ) {
                item {
                    ProfileHero(
                        employee = employee,
                        contractStatus = s.contractStatus,
                        statusBarTop = statusBarTop,
                    )
                }
                item {
                    SectionGroup(title = stringResource(R.string.profile_group_account)) {
                        ProfileSectionRow(
                            icon = Icons.Outlined.Person,
                            title = stringResource(R.string.personal),
                            summary = displayName(employee).ifBlank { stringResource(R.string.no_data) },
                            onClick = onNavigateToPersonal,
                        )
                        RowDivider()
                        ProfileSectionRow(
                            icon = Icons.Outlined.Place,
                            title = stringResource(R.string.address),
                            summary = displayAddress(employee).ifBlank { stringResource(R.string.no_data) },
                            onClick = onNavigateToAddress,
                        )
                        RowDivider()
                        ProfileSectionRow(
                            icon = Icons.Outlined.Phone,
                            title = stringResource(R.string.emergency_contact),
                            summary = displayEmergency(employee).ifBlank { stringResource(R.string.no_data) },
                            onClick = onNavigateToEmergency,
                        )
                    }
                }
                item {
                    SectionGroup(title = stringResource(R.string.profile_group_work_legal)) {
                        ProfileSectionRow(
                            icon = Icons.Outlined.Badge,
                            title = stringResource(R.string.identification_title),
                            summary = employee.passportId?.takeIf { it.isNotBlank() }
                                ?: stringResource(R.string.no_data),
                            onClick = onNavigateToIdentification,
                        )
                        RowDivider()
                        ProfileSectionRow(
                            icon = Icons.Outlined.AccountBalance,
                            title = stringResource(R.string.bank_details),
                            summary = employee.iban?.takeIf { it.isNotBlank() }
                                ?: stringResource(R.string.no_data),
                            onClick = onNavigateToBank,
                        )
                        RowDivider()
                        ProfileSectionRow(
                            icon = Icons.Outlined.Description,
                            title = stringResource(R.string.my_documents),
                            summary = stringResource(R.string.documents_summary_view),
                            onClick = onNavigateToDocuments,
                        )
                    }
                }
                item {
                    // Preferences: same row pattern as every other
                    // section — current value as the at-a-glance
                    // summary, tap opens a dedicated picker screen.
                    // Drops the inline dropdown chrome that looked
                    // out of place vs the rest of the page.
                    SectionGroup(title = stringResource(R.string.profile_group_preferences)) {
                        ProfileSectionRow(
                            icon = Icons.Outlined.Language,
                            title = stringResource(R.string.language),
                            summary = languageSummary(settings.language),
                            onClick = onNavigateToLanguage,
                        )
                        RowDivider()
                        ProfileSectionRow(
                            icon = Icons.Outlined.DarkMode,
                            title = stringResource(R.string.theme),
                            summary = themeSummary(settings.theme),
                            onClick = onNavigateToTheme,
                        )
                        RowDivider()
                        ProfileSectionRow(
                            icon = Icons.Outlined.Devices,
                            title = stringResource(R.string.devices_title),
                            summary = stringResource(R.string.profile_devices_summary),
                            onClick = onNavigateToDevices,
                        )
                    }
                }
                item {
                    LogoutRow(onClick = { showLogoutDialog = true })
                    Spacer(Modifier.height(Spacing.M))
                }
            }
        }
        ProfileUiState.Error -> {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = stringResource(R.string.error_generic),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
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
                viewModel.signOut()
            },
            dismissLabel = stringResource(R.string.profile_logout_dialog_cancel),
        )
    }
}

/**
 * Compact, flat hero — row layout: circular initials avatar on the
 * left, name / email / status-chip stacked on the right. No gradient;
 * sits on `colorScheme.background` like the rest of the page so the
 * profile reads as one continuous flat surface, not a marketing
 * banner glued to a list. Status chip vertically center-aligned with
 * the rest of the right-hand stack.
 */
@Composable
private fun ProfileHero(
    employee: EmployeeItem,
    contractStatus: ContractStatus?,
    statusBarTop: androidx.compose.ui.unit.Dp,
) {
    val initials = remember(employee.firstName, employee.lastName) {
        initialsOf(employee.firstName, employee.lastName)
    }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = statusBarTop + Spacing.M, bottom = Spacing.S)
            .padding(horizontal = Spacing.M),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        InitialsAvatar(initials = initials)
        Spacer(Modifier.width(Spacing.M))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = displayName(employee).ifBlank { stringResource(R.string.no_data) },
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
            )
            employee.email?.takeIf { it.isNotBlank() }?.let { email ->
                Text(
                    text = email,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
                )
            }
            if (contractStatus != null) {
                Spacer(Modifier.height(6.dp))
                StatusChip(contractStatus)
            }
        }
    }
}

/**
 * Initials avatar — primary-container tint, brand-blue bold text.
 * Sized to dominate the hero so the profile page reads as the
 * cleaner's identity at a glance.
 */
@Composable
private fun InitialsAvatar(initials: String) {
    Box(
        modifier = Modifier
            .size(80.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f)),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = initials,
            style = MaterialTheme.typography.headlineSmall.copy(
                fontWeight = FontWeight.Bold,
                fontSize = 28.sp,
            ),
            color = MaterialTheme.colorScheme.primary,
        )
    }
}

/**
 * Contract-status chip. Color encodes meaning so the chip is
 * skim-readable: green for Approved/Active, amber for Pending,
 * red for Rejected/Terminated. Sits inline with the rest of the
 * hero text, vertically center-aligned via the parent Row.
 */
@Composable
private fun StatusChip(status: ContractStatus) {
    val (labelRes, container, content) = when (status) {
        ContractStatus._1 -> Triple(
            R.string.contract_status_pending,
            StatusAmberContainer,
            StatusAmberContent,
        )
        ContractStatus._2 -> Triple(
            R.string.contract_status_active,
            StatusGreenContainer,
            StatusGreenContent,
        )
        ContractStatus._3 -> Triple(
            R.string.contract_status_terminated,
            MaterialTheme.colorScheme.errorContainer,
            MaterialTheme.colorScheme.onErrorContainer,
        )
        ContractStatus._4 -> Triple(
            R.string.contract_status_approved,
            StatusGreenContainer,
            StatusGreenContent,
        )
        ContractStatus._5 -> Triple(
            R.string.contract_status_rejected,
            MaterialTheme.colorScheme.errorContainer,
            MaterialTheme.colorScheme.onErrorContainer,
        )
    }
    Row(
        modifier = Modifier
            .clip(CircleShape)
            .background(container)
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // Leading colored dot — same hue as the chip's content color
        // so the user can scan-read the status even when the chip
        // is small or the text wraps. Pure indicator chip.
        Box(
            modifier = Modifier
                .size(6.dp)
                .clip(CircleShape)
                .background(content),
        )
        Spacer(Modifier.width(6.dp))
        Text(
            text = stringResource(labelRes),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = content,
        )
    }
}

// Status chip palette — hardcoded because Material doesn't ship a
// "warning"/"success" container color, and adding them to the theme
// would touch surfaces unrelated to this chip. Two colors per state:
// container (background fill) + content (text color), tuned for AA
// contrast on the chip's tiny labelMedium type.
private val StatusGreenContainer = Color(0xFFD7F4DC)
private val StatusGreenContent = Color(0xFF1E6B30)
private val StatusAmberContainer = Color(0xFFFFE9C2)
private val StatusAmberContent = Color(0xFF7A4D00)

/**
 * Flat, titled section card. Uppercase title above, surface-coloured
 * rounded container below — same recipe as the customer profile's
 * SettingsSection so the two apps read as one family.
 */
@Composable
private fun SectionGroup(title: String, content: @Composable () -> Unit) {
    Column(modifier = Modifier.padding(horizontal = Spacing.M)) {
        Text(
            text = title.uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(start = Spacing.XS, bottom = Spacing.XS),
        )
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(18.dp))
                .background(MaterialTheme.colorScheme.surface),
        ) {
            content()
        }
    }
}

@Composable
private fun RowDivider() {
    HorizontalDivider(
        modifier = Modifier.padding(horizontal = Spacing.M),
        color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f),
    )
}

/**
 * Standard navigable row used everywhere on the profile landing:
 * 32dp circle icon, title + at-a-glance subtitle (e.g. "John Doe"
 * under Personal info), trailing chevron. Subtitle is intentionally
 * kept (customer rows are title-only); partner sections carry more
 * data and the subtitle saves the user a tap to confirm it.
 */
@Composable
private fun ProfileSectionRow(
    icon: ImageVector,
    title: String,
    summary: String,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() }
            .padding(horizontal = Spacing.M, vertical = Spacing.S + 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(32.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
        }
        Spacer(Modifier.width(Spacing.M))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = summary,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1,
            )
        }
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/**
 * Human label for the cleaner's current language preference, shown as
 * the at-a-glance summary on the Preferences row. "System" defers to
 * the device locale.
 */
@Composable
private fun languageSummary(language: LanguagePreference): String = when (language) {
    LanguagePreference.System -> stringResource(R.string.language_system)
    LanguagePreference.English -> "English"
    LanguagePreference.Czech -> "Čeština"
    LanguagePreference.Slovak -> "Slovenčina"
    LanguagePreference.Ukrainian -> "Українська"
    LanguagePreference.Russian -> "Русский"
}

@Composable
private fun themeSummary(theme: ThemePreference): String = stringResource(
    when (theme) {
        ThemePreference.System -> R.string.theme_system
        ThemePreference.Light -> R.string.theme_light
        ThemePreference.Dark -> R.string.theme_dark
    }
)

@Composable
private fun LogoutRow(onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.4f))
            .clickable { onClick() }
            .padding(horizontal = Spacing.M, vertical = Spacing.M),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.Logout,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.error,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(Spacing.M))
        Text(
            text = stringResource(R.string.logout),
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.error,
        )
    }
}

private fun initialsOf(firstName: String?, lastName: String?): String {
    val first = firstName?.firstOrNull()?.uppercaseChar()
    val last = lastName?.firstOrNull()?.uppercaseChar()
    return listOfNotNull(first, last).joinToString("").ifBlank { "?" }
}

private fun displayName(e: EmployeeItem): String =
    listOfNotNull(e.firstName?.takeIf { it.isNotBlank() }, e.lastName?.takeIf { it.isNotBlank() })
        .joinToString(" ")

private fun displayAddress(e: EmployeeItem): String =
    listOfNotNull(
        e.street?.takeIf { it.isNotBlank() },
        e.city?.takeIf { it.isNotBlank() },
        e.zipCode?.takeIf { it.isNotBlank() },
    ).joinToString(", ")

private fun displayEmergency(e: EmployeeItem): String =
    listOfNotNull(
        e.emergencyContactName?.takeIf { it.isNotBlank() },
        e.emergencyContactPhone?.takeIf { it.isNotBlank() },
    ).joinToString(" · ")
