package cz.cleansia.partner.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.CameraAlt
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SelectableDates
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
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
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaPhoneInput
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/**
 * Personal-info editor — uses the shared SectionScaffold (TopAppBar
 * back arrow + scrollable body). Top of the body shows an initials
 * avatar with a (non-functional, see below) change-photo pill to
 * match the customer profile's edit screen silhouette; the photo
 * upload endpoint doesn't exist yet on the partner backend so the
 * pill is decorative — tapping it surfaces a snackbar via the parent
 * VM's "coming soon" hook (caller wires that).
 *
 * Email is rendered locked + read-only. The backend allows email
 * updates but changing it has knock-on effects (auth, login, audit)
 * and the customer flow already treats it the same way; mirror here
 * to keep the contract consistent.
 *
 * Phone uses CleansiaPhoneInput so format-as-you-type matches the
 * region inferred from a leading "+", falling back to device locale.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PersonalSectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    onboarding: Boolean = false,
    viewModel: PersonalSectionViewModel = hiltViewModel(),
    chainViewModel: cz.cleansia.partner.features.orders.OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val chainState by chainViewModel.state.collectAsStateWithLifecycle()

    LaunchedEffect(uiState.isSaved) {
        if (uiState.isSaved) onSaved()
    }

    SectionScaffold(
        title = stringResource(R.string.personal),
        isLoading = uiState.isLoading,
        onNavigateBack = onNavigateBack,
        headerSlot = if (onboarding) {
            {
                cz.cleansia.partner.features.profile.OnboardingChainHeader(
                    currentSection = cz.cleansia.partner.features.orders.ProfileSection.Personal,
                    state = chainState,
                )
            }
        } else null,
        form = {
            AvatarPreview(
                initials = initialsOf(uiState.firstName, uiState.lastName),
            )
            Spacer(Modifier.height(Spacing.L))

            FormSectionCard(title = stringResource(R.string.profile_section_personal)) {
                CleansiaTextField(
                    value = uiState.firstName,
                    onValueChange = viewModel::onFirstNameChange,
                    label = stringResource(R.string.first_name),
                    errorText = uiState.firstNameError,
                    enabled = !uiState.isSaving,
                    transparentContainer = true,
                )
                Spacer(Modifier.height(Spacing.XS))
                CleansiaTextField(
                    value = uiState.lastName,
                    onValueChange = viewModel::onLastNameChange,
                    label = stringResource(R.string.last_name),
                    errorText = uiState.lastNameError,
                    enabled = !uiState.isSaving,
                    transparentContainer = true,
                )
                Spacer(Modifier.height(Spacing.XS))
                BirthDateField(
                    value = uiState.birthDate,
                    onValueChange = viewModel::onBirthDateChange,
                    enabled = !uiState.isSaving,
                )
            }
            Spacer(Modifier.height(Spacing.M))

            FormSectionCard(title = stringResource(R.string.profile_section_contact)) {
                CleansiaPhoneInput(
                    value = uiState.phone,
                    onValueChange = viewModel::onPhoneChange,
                    label = stringResource(R.string.phone),
                    enabled = !uiState.isSaving,
                    transparentContainer = true,
                )
                Spacer(Modifier.height(Spacing.XS))
                ReadOnlyEmailField(value = uiState.email)
            }

            Spacer(Modifier.height(Spacing.L))

            CleansiaPrimaryButton(
                text = stringResource(
                    if (onboarding) R.string.save_and_continue else R.string.save,
                ),
                onClick = { viewModel.save() },
                loading = uiState.isSaving,
                enabled = uiState.firstName.isNotBlank() && uiState.lastName.isNotBlank() && !uiState.isSaving,
            )
        },
    )
}

/**
 * Centered initials avatar + camera-pill overlay. The pill has no
 * onClick yet (no backend endpoint), mirrors the customer's
 * EditProfileScreen which also leaves it as a visual placeholder
 * with a TODO. Initials update live as the name fields change.
 */
@Composable
private fun AvatarPreview(initials: String) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = Spacing.M),
        contentAlignment = Alignment.Center,
    ) {
        Box {
            Box(
                modifier = Modifier
                    .size(104.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f))
                    .border(3.dp, MaterialTheme.colorScheme.surface, CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = initials,
                    style = MaterialTheme.typography.displaySmall.copy(
                        fontWeight = FontWeight.Bold,
                        fontSize = 36.sp,
                    ),
                    color = MaterialTheme.colorScheme.primary,
                )
            }
            Box(
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .size(34.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary)
                    .border(3.dp, MaterialTheme.colorScheme.background, CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    Icons.Outlined.CameraAlt,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onPrimary,
                    modifier = Modifier.size(16.dp),
                )
            }
        }
    }
}

/**
 * Email displayed locked. Backend allows updates but the partner
 * shouldn't change their own login from the profile; the admin owns
 * that path. Matches the customer profile's read-only treatment.
 */
@Composable
private fun ReadOnlyEmailField(value: String) {
    CleansiaTextField(
        value = value,
        onValueChange = {},
        label = stringResource(R.string.email),
        helper = stringResource(R.string.email_readonly_helper),
        enabled = false,
        transparentContainer = true,
    )
}

private fun initialsOf(firstName: String, lastName: String): String {
    val first = firstName.firstOrNull()?.uppercaseChar()
    val last = lastName.firstOrNull()?.uppercaseChar()
    return listOfNotNull(first, last).joinToString("").ifBlank { "?" }
}

private val isoDateFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd")
private val displayDateFormatter = DateTimeFormatter.ofPattern("d MMM yyyy")

/**
 * Tappable Material 3 date field. Shows the selected birth date in
 * locale-friendly form (e.g. "15 Mar 1985") while persisting the
 * backend's required `yyyy-MM-dd` string. Future dates are blocked
 * by clamping `selectableDates` to today.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun BirthDateField(
    value: String,
    onValueChange: (String) -> Unit,
    enabled: Boolean,
) {
    var showDialog by remember { mutableStateOf(false) }

    val parsed = remember(value) {
        runCatching { LocalDate.parse(value, isoDateFormatter) }.getOrNull()
    }
    val displayText = parsed?.format(displayDateFormatter).orEmpty()

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .border(
                1.dp,
                MaterialTheme.colorScheme.outline,
                RoundedCornerShape(12.dp),
            )
            .clickable(enabled = enabled) { showDialog = true }
            .padding(horizontal = 16.dp, vertical = 10.dp),
    ) {
        Column {
            Text(
                text = stringResource(R.string.birth_date),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = displayText.ifBlank { stringResource(R.string.birth_date_placeholder) },
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Medium),
                    color = if (displayText.isBlank())
                        MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                    else MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                Icon(
                    imageVector = Icons.Outlined.CalendarMonth,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
        }
    }

    if (showDialog) {
        val initialMillis = parsed
            ?.atStartOfDay(ZoneId.of("UTC"))
            ?.toInstant()
            ?.toEpochMilli()
            ?: LocalDate.now().minusYears(25)
                .atStartOfDay(ZoneId.of("UTC"))
                .toInstant()
                .toEpochMilli()
        val datePickerState = rememberDatePickerState(
            initialSelectedDateMillis = initialMillis,
            selectableDates = object : SelectableDates {
                override fun isSelectableDate(utcTimeMillis: Long): Boolean {
                    val today = LocalDate.now()
                        .atStartOfDay(ZoneId.of("UTC"))
                        .toInstant()
                        .toEpochMilli()
                    return utcTimeMillis <= today
                }
            },
        )

        DatePickerDialog(
            onDismissRequest = { showDialog = false },
            confirmButton = {
                TextButton(onClick = {
                    val millis = datePickerState.selectedDateMillis
                    if (millis != null) {
                        val picked = Instant.ofEpochMilli(millis)
                            .atZone(ZoneId.of("UTC"))
                            .toLocalDate()
                        onValueChange(picked.format(isoDateFormatter))
                    }
                    showDialog = false
                }) { Text(stringResource(R.string.confirm)) }
            },
            dismissButton = {
                TextButton(onClick = { showDialog = false }) {
                    Text(stringResource(R.string.cancel))
                }
            },
        ) {
            DatePicker(state = datePickerState)
        }
    }
}
