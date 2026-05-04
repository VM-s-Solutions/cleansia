package cz.cleansia.customer.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.CameraAlt
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.Poppins
import cz.cleansia.customer.ui.theme.Sky600

/**
 * Wolt/Bolt-style edit profile:
 *  - Avatar preview at top with camera-pill overlay
 *  - Two grouped sections (PERSONAL / CONTACT) with bordered fields
 *  - Inline labels above each field (consistent whether empty or not)
 *  - Sticky "Save changes" CTA pinned to the bottom
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EditProfileScreen(
    user: CurrentUser? = null,
    saving: Boolean = false,
    onBack: () -> Unit = {},
    onSave: (firstName: String, lastName: String, phone: String, birthDate: String) -> Unit = { _, _, _, _ -> },
) {
    // Seed the form once per user identity. Using `user?.id` as the remember key
    // means we re-prime if the cache refreshes under us (new data from server),
    // but not on every recomposition — the user can still edit freely.
    var firstName by remember(user?.id) { mutableStateOf(user?.firstName.orEmpty()) }
    var lastName by remember(user?.id) { mutableStateOf(user?.lastName.orEmpty()) }
    var phone by remember(user?.id) { mutableStateOf(user?.phoneNumber.orEmpty()) }
    var birthDate by remember(user?.id) { mutableStateOf(user?.birthDate.orEmpty()) }
    val email = user?.email.orEmpty()

    val canSave = firstName.isNotBlank() && lastName.isNotBlank() && !saving

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .imePadding(),
    ) {
        TopAppBar(
            title = {
                Text(
                    stringResource(R.string.profile_edit_title),
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
            colors = TopAppBarDefaults.topAppBarColors(
                containerColor = MaterialTheme.colorScheme.background,
            ),
        )

        Column(
            modifier = Modifier
                .fillMaxSize()
                .weight(1f)
                .verticalScroll(rememberScrollState()),
        ) {
            // Avatar preview + change-photo pill
            AvatarPreview(initials = "${firstName.firstOrNull() ?: ""}${lastName.firstOrNull() ?: ""}")

            Spacer(Modifier.height(24.dp))

            FormSection(title = stringResource(R.string.profile_section_personal)) {
                LabeledField(
                    label = stringResource(R.string.profile_edit_first_name),
                    value = firstName,
                    onValueChange = { firstName = it },
                    capitalization = KeyboardCapitalization.Words,
                )
                FieldDivider()
                LabeledField(
                    label = stringResource(R.string.profile_edit_last_name),
                    value = lastName,
                    onValueChange = { lastName = it },
                    capitalization = KeyboardCapitalization.Words,
                )
                FieldDivider()
                DateField(
                    label = stringResource(R.string.profile_edit_birthdate),
                    value = birthDate,
                    onValueChange = { birthDate = it },
                    placeholder = "DD.MM.YYYY",
                )
            }

            Spacer(Modifier.height(16.dp))

            FormSection(title = stringResource(R.string.profile_section_contact)) {
                LabeledField(
                    label = stringResource(R.string.profile_edit_email),
                    value = email,
                    onValueChange = {},
                    enabled = false,
                    keyboardType = KeyboardType.Email,
                    helper = stringResource(R.string.profile_edit_email_readonly),
                )
                FieldDivider()
                LabeledField(
                    label = stringResource(R.string.profile_edit_phone),
                    value = phone,
                    onValueChange = { phone = it },
                    placeholder = "+420 000 000 000",
                    keyboardType = KeyboardType.Phone,
                )
            }

            Spacer(Modifier.height(32.dp))
        }

        // Sticky bottom save button
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.background)
                .navigationBarsPadding()
                .padding(horizontal = 20.dp, vertical = 12.dp),
        ) {
            CleansiaPrimaryButton(
                text = stringResource(R.string.profile_edit_save),
                onClick = { onSave(firstName, lastName, phone, birthDate) },
                enabled = canSave,
            )
        }
    }
}

@Composable
private fun AvatarPreview(initials: String) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 24.dp, bottom = 8.dp),
        contentAlignment = Alignment.Center,
    ) {
        Box {
            // Avatar
            Box(
                modifier = Modifier
                    .size(104.dp)
                    .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f), CircleShape)
                    .border(3.dp, MaterialTheme.colorScheme.surface, CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    initials.uppercase(),
                    style = MaterialTheme.typography.displaySmall.copy(
                        fontFamily = Poppins,
                        fontWeight = FontWeight.Bold,
                    ),
                    color = Sky600,
                )
            }
            // Camera pill — bottom-right overlay
            Box(
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .size(34.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary)
                    .border(3.dp, MaterialTheme.colorScheme.background, CircleShape)
                    .clickable { /* TODO: launch photo picker */ },
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

@Composable
private fun FormSection(title: String, content: @Composable () -> Unit) {
    Column(modifier = Modifier.padding(horizontal = 20.dp)) {
        Text(
            title.uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(
                fontWeight = FontWeight.SemiBold,
                letterSpacing = 0.8.sp,
            ),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(start = 4.dp, bottom = 8.dp),
        )
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(16.dp))
                .background(MaterialTheme.colorScheme.surface),
        ) {
            content()
        }
    }
}

@Composable
private fun FieldDivider() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = 16.dp)
            .height(1.dp)
            .background(MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)),
    )
}

@Composable
private fun LabeledField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    placeholder: String = "",
    keyboardType: KeyboardType = KeyboardType.Text,
    capitalization: KeyboardCapitalization = KeyboardCapitalization.None,
    enabled: Boolean = true,
    helper: String? = null,
) {
    Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp)) {
        Text(
            label,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(4.dp))
        Box {
            if (value.isEmpty() && placeholder.isNotEmpty()) {
                Text(
                    placeholder,
                    style = LocalTextStyle.current.copy(color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)),
                )
            }
            BasicTextField(
                value = value,
                onValueChange = onValueChange,
                enabled = enabled,
                singleLine = true,
                textStyle = LocalTextStyle.current.copy(
                    color = if (enabled) MaterialTheme.colorScheme.onSurface
                    else MaterialTheme.colorScheme.onSurfaceVariant,
                    fontWeight = FontWeight.Medium,
                ),
                cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
                keyboardOptions = KeyboardOptions(
                    keyboardType = keyboardType,
                    capitalization = capitalization,
                    imeAction = ImeAction.Next,
                ),
                modifier = Modifier.fillMaxWidth(),
            )
        }
        if (helper != null) {
            Spacer(Modifier.height(4.dp))
            Text(
                helper,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/**
 * Tap-only date picker row. Shows the value (or placeholder) like a regular
 * labeled field, but opens a Material3 DatePickerDialog on tap — prevents the
 * user from typing garbage that needs validation.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DateField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    placeholder: String = "",
) {
    var showPicker by remember { mutableStateOf(false) }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { showPicker = true }
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                label,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = value.ifEmpty { placeholder },
                style = LocalTextStyle.current.copy(
                    color = if (value.isEmpty()) MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                    else MaterialTheme.colorScheme.onSurface,
                    fontWeight = FontWeight.Medium,
                ),
            )
        }
        Icon(
            Icons.Outlined.CalendarToday,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(18.dp),
        )
    }

    if (showPicker) {
        val datePickerState = rememberDatePickerState()
        DatePickerDialog(
            onDismissRequest = { showPicker = false },
            confirmButton = {
                TextButton(onClick = {
                    datePickerState.selectedDateMillis?.let { millis ->
                        onValueChange(formatDateDMY(millis))
                    }
                    showPicker = false
                }) { Text(stringResource(R.string.common_save)) }
            },
            dismissButton = {
                TextButton(onClick = { showPicker = false }) {
                    Text(stringResource(R.string.common_cancel))
                }
            },
        ) {
            DatePicker(state = datePickerState)
        }
    }
}

/** Formats millis into "DD.MM.YYYY" — Czech convention, matches the placeholder. */
private fun formatDateDMY(millis: Long): String {
    val cal = java.util.Calendar.getInstance(java.util.TimeZone.getTimeZone("UTC"))
    cal.timeInMillis = millis
    val d = cal.get(java.util.Calendar.DAY_OF_MONTH)
    val m = cal.get(java.util.Calendar.MONTH) + 1
    val y = cal.get(java.util.Calendar.YEAR)
    return "%02d.%02d.%04d".format(d, m, y)
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun EditProfilePreview() {
    CleansiaTheme { EditProfileScreen() }
}
