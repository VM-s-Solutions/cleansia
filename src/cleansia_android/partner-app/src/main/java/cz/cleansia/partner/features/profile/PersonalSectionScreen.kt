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
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.ui.layout.ContentScale
import coil3.compose.AsyncImage
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaTextLink
import androidx.compose.foundation.layout.fillMaxSize
import cz.cleansia.core.ui.state.ActionState
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
 * Email is rendered locked + read-only. The backend has no email
 * field on any profile command — the login address is the session's
 * identity and only support can change it — so there is nothing to
 * send and nothing that could be saved.
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
    /**
     * Tapping a completed step dot in the onboarding header. Defaulted to a no-op because the same
     * screen is reachable from the profile menu, where there is no chain to jump around in.
     */
    onJumpToSection: (cz.cleansia.partner.features.orders.ProfileSection) -> Unit = {},
    viewModel: PersonalSectionViewModel = hiltViewModel(),
    chainViewModel: cz.cleansia.partner.features.orders.OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val saveState by viewModel.saveState.collectAsStateWithLifecycle()
    val avatarDraft by viewModel.avatarDraft.collectAsStateWithLifecycle()
    val avatarStateValue by viewModel.avatarState.collectAsStateWithLifecycle()
    val avatarBusy = avatarStateValue is ActionState.Submitting
    val chainState by chainViewModel.state.collectAsStateWithLifecycle()
    val saving = saveState is cz.cleansia.core.ui.state.ActionState.Submitting
    val form = (uiState as? PersonalSectionUiState.Loaded)?.form ?: PersonalForm()

    LaunchedEffect(viewModel) { viewModel.saved.collect { onSaved() } }

    SectionScaffold(
        title = stringResource(R.string.personal),
        isLoading = uiState is PersonalSectionUiState.Loading,
        isError = uiState is PersonalSectionUiState.Error,
        onRetry = viewModel::retry,
        onNavigateBack = onNavigateBack,
        headerSlot = if (onboarding) {
            {
                cz.cleansia.partner.features.profile.OnboardingChainHeader(
                    currentSection = cz.cleansia.partner.features.orders.ProfileSection.Personal,
                    state = chainState,
                    onSelect = onJumpToSection,
                )
            }
        } else null,
        form = {
            AvatarPreview(
                initials = initialsOf(form.firstName, form.lastName),
                savedPhotoUrl = form.profilePhotoUrl,
                draft = avatarDraft,
                busy = avatarBusy,
                onPick = viewModel::pickAvatar,
                onRemove = viewModel::removeAvatar,
            )
            Spacer(Modifier.height(Spacing.L))

            FormSectionCard(title = stringResource(R.string.profile_section_personal)) {
                CleansiaTextField(
                    value = form.firstName,
                    onValueChange = viewModel::onFirstNameChange,
                    label = stringResource(R.string.first_name),
                    errorText = form.firstNameError,
                    enabled = !saving,
                    transparentContainer = true,
                )
                Spacer(Modifier.height(Spacing.XS))
                CleansiaTextField(
                    value = form.lastName,
                    onValueChange = viewModel::onLastNameChange,
                    label = stringResource(R.string.last_name),
                    errorText = form.lastNameError,
                    enabled = !saving,
                    transparentContainer = true,
                )
                Spacer(Modifier.height(Spacing.XS))
                BirthDateField(
                    value = form.birthDate,
                    onValueChange = viewModel::onBirthDateChange,
                    enabled = !saving,
                    errorText = form.birthDateError,
                )
            }
            Spacer(Modifier.height(Spacing.M))

            FormSectionCard(title = stringResource(R.string.profile_section_contact)) {
                CleansiaPhoneInput(
                    value = form.phone,
                    onValueChange = viewModel::onPhoneChange,
                    label = stringResource(R.string.phone),
                    enabled = !saving,
                    transparentContainer = true,
                )
                Spacer(Modifier.height(Spacing.XS))
                ReadOnlyEmailField(value = form.email)
            }

            Spacer(Modifier.height(Spacing.L))

            CleansiaPrimaryButton(
                text = stringResource(
                    if (onboarding) R.string.save_and_continue else R.string.save,
                ),
                onClick = { viewModel.save() },
                loading = saving,
                enabled = form.firstName.isNotBlank() && form.lastName.isNotBlank() && !saving,
            )
        },
    )
}

/**
 * The cleaner's profile photo, with initials as the fallback.
 *
 * **The affordance is back, and now it works.** It was removed on 2026-08-14 on the sound reasoning
 * that the app rendered no photo anywhere, so a badge offering to change one had nothing to change.
 * The reason it rendered none was a comment claiming the partner backend had no photo endpoint — which
 * was already false: `UpdateCurrentUser_Command` carries `photo` and `removePhoto`, and
 * `MyProfileDto` returns `profilePhoto`, exactly as the customer contract does.
 *
 * The pick is staged, not uploaded — it goes with the rest of the form on Save, so backing out of the
 * screen leaves the stored photo alone.
 */
@Composable
private fun AvatarPreview(
    initials: String,
    savedPhotoUrl: String?,
    draft: PartnerAvatarDraft,
    busy: Boolean,
    onPick: (Uri) -> Unit,
    onRemove: () -> Unit,
) {
    var pickerUnavailable by remember { mutableStateOf(false) }
    // The system photo picker grants per-item access to exactly what was chosen, so it needs no
    // storage permission on any API level this app supports and there is no denial branch to write.
    val pickImage = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.PickVisualMedia(),
        onResult = { uri -> uri?.let(onPick) },
    )
    val launchPicker = {
        // A device with neither picker nor document provider throws rather than returning empty, and
        // an uncaught throw here takes the screen down. Same guard the customer app carries.
        runCatching {
            pickImage.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly))
        }.onFailure { pickerUnavailable = true }
        Unit
    }

    if (pickerUnavailable) {
        CleansiaDialog(
            onDismiss = { pickerUnavailable = false },
            title = stringResource(R.string.profile_avatar_picker_unavailable_title),
            confirmLabel = stringResource(android.R.string.ok),
            onConfirm = { pickerUnavailable = false },
            message = stringResource(R.string.profile_avatar_picker_unavailable_message),
        )
    }

    AvatarPreviewContent(
        initials = initials,
        // What the cleaner will see after saving: the pick if there is one, nothing if they removed
        // it, the stored photo otherwise.
        shownPhoto = when (draft) {
            is PartnerAvatarDraft.Picked -> draft.previewUri
            PartnerAvatarDraft.Removed -> null
            PartnerAvatarDraft.Unchanged -> savedPhotoUrl
        },
        busy = busy,
        canRemove = draft is PartnerAvatarDraft.Picked ||
            (draft is PartnerAvatarDraft.Unchanged && savedPhotoUrl != null),
        onPick = launchPicker,
        onRemove = onRemove,
    )
}

@Composable
private fun AvatarPreviewContent(
    initials: String,
    shownPhoto: Any?,
    busy: Boolean,
    canRemove: Boolean,
    onPick: () -> Unit,
    onRemove: () -> Unit,
) {
    // A Column, not a Box. Both children in one Box meant the links were aligned to the BOTTOM of a
    // box whose height is the 104.dp avatar — so they were drawn across the face, and wider than the
    // circle at that. Stacking puts them under it, which is also where the customer app has them.
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = Spacing.M),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Box(
            modifier = Modifier
                .size(104.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f))
                .border(3.dp, MaterialTheme.colorScheme.surface, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            if (shownPhoto != null) {
                AsyncImage(
                    model = shownPhoto,
                    contentDescription = stringResource(R.string.profile_avatar_content_description),
                    contentScale = ContentScale.Crop,
                    modifier = Modifier.fillMaxSize().clip(CircleShape),
                )
            } else {
                Text(
                    text = initials,
                    style = MaterialTheme.typography.displaySmall.copy(
                        fontWeight = FontWeight.Bold,
                        fontSize = 36.sp,
                    ),
                    color = MaterialTheme.colorScheme.primary,
                )
            }
            if (busy) {
                CircularProgressIndicator(modifier = Modifier.size(28.dp))
            }
        }

        Spacer(Modifier.height(Spacing.XS))

        Row(
            horizontalArrangement = Arrangement.spacedBy(Spacing.S),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            CleansiaTextLink(
                text = stringResource(R.string.profile_avatar_change),
                onClick = onPick,
            )
            if (canRemove) {
                CleansiaTextLink(
                    text = stringResource(R.string.profile_avatar_remove),
                    onClick = onRemove,
                )
            }
        }
    }
}

/**
 * Email displayed locked. The profile commands carry no email at all;
 * changing a login address is a support-only path. Matches the
 * customer profile's read-only treatment.
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
    errorText: String? = null,
) {
    var showDialog by remember { mutableStateOf(false) }

    val parsed = remember(value) {
        runCatching { LocalDate.parse(value, isoDateFormatter) }.getOrNull()
    }
    val displayText = parsed?.format(displayDateFormatter).orEmpty()
    val isError = errorText != null

    Column {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(12.dp))
                .border(
                    1.dp,
                    if (isError) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.outline,
                    RoundedCornerShape(12.dp),
                )
                .clickable(enabled = enabled) { showDialog = true }
                .padding(horizontal = 16.dp, vertical = 10.dp),
        ) {
            Column {
                Text(
                    text = stringResource(R.string.birth_date),
                    style = MaterialTheme.typography.labelSmall,
                    color = if (isError) MaterialTheme.colorScheme.error
                    else MaterialTheme.colorScheme.onSurfaceVariant,
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
        if (errorText != null) {
            Text(
                text = errorText,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(start = 16.dp, top = 4.dp),
            )
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
