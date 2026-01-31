package cz.cleansia.partner.features.profile.components

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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountBalance
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.ContactPhone
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Person
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.RelationshipType
import cz.cleansia.partner.features.profile.viewmodels.ProfileEditFormState
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.CleansiaButtonStyle
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable
fun EditProfileForm(
    formState: ProfileEditFormState,
    isSaving: Boolean,
    onFirstNameChange: (String) -> Unit,
    onLastNameChange: (String) -> Unit,
    onPhoneNumberChange: (String) -> Unit,
    onDateOfBirthChange: (String) -> Unit,
    onNationalityChange: (String) -> Unit = {},
    onNationalIdChange: (String) -> Unit = {},
    onPassportIdChange: (String) -> Unit = {},
    onTaxIdChange: (String) -> Unit = {},
    onStreetChange: (String) -> Unit,
    onCityChange: (String) -> Unit,
    onZipCodeChange: (String) -> Unit,
    onCountryChange: (String) -> Unit,
    onIbanChange: (String) -> Unit,
    onEmergencyContactNameChange: (String) -> Unit = {},
    onEmergencyContactRelationshipChange: (String) -> Unit = {},
    onEmergencyContactPhoneChange: (String) -> Unit = {},
    onEmergencyContactEmailChange: (String) -> Unit = {},
    onSave: () -> Unit,
    onCancel: () -> Unit,
    countries: List<Country> = emptyList(),
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Personal Information Section
        FormSection(
            title = stringResource(R.string.personal_info),
            icon = Icons.Default.Person
        ) {
            OutlinedTextField(
                value = formState.firstName,
                onValueChange = onFirstNameChange,
                label = { Text(stringResource(R.string.first_name)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.lastName,
                onValueChange = onLastNameChange,
                label = { Text(stringResource(R.string.last_name)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.phoneNumber,
                onValueChange = onPhoneNumberChange,
                label = { Text(stringResource(R.string.phone_number)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            DatePickerField(
                label = stringResource(R.string.date_of_birth),
                value = formState.dateOfBirth,
                onValueChange = onDateOfBirthChange
            )
        }

        // Additional Personal Info Section (Nationality, IDs)
        FormSection(
            title = stringResource(R.string.identification_info),
            icon = Icons.Default.Badge
        ) {
            OutlinedTextField(
                value = formState.nationality,
                onValueChange = onNationalityChange,
                label = { Text(stringResource(R.string.nationality)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.nationalId,
                onValueChange = onNationalIdChange,
                label = { Text(stringResource(R.string.national_id)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.passportId,
                onValueChange = onPassportIdChange,
                label = { Text(stringResource(R.string.passport_id)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.taxId,
                onValueChange = onTaxIdChange,
                label = { Text(stringResource(R.string.tax_id)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
        }

        // Address Section
        FormSection(
            title = stringResource(R.string.address_info),
            icon = Icons.Default.LocationOn
        ) {
            OutlinedTextField(
                value = formState.street,
                onValueChange = onStreetChange,
                label = { Text(stringResource(R.string.street)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.city,
                onValueChange = onCityChange,
                label = { Text(stringResource(R.string.city)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.zipCode,
                onValueChange = onZipCodeChange,
                label = { Text(stringResource(R.string.zip_code)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            if (countries.isNotEmpty()) {
                CountryDropdown(
                    selectedCountry = formState.country,
                    countries = countries,
                    onCountrySelected = onCountryChange
                )
            } else {
                OutlinedTextField(
                    value = formState.country,
                    onValueChange = onCountryChange,
                    label = { Text(stringResource(R.string.country)) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }
        }

        // Bank Details Section
        FormSection(
            title = stringResource(R.string.bank_details),
            icon = Icons.Default.AccountBalance
        ) {
            OutlinedTextField(
                value = formState.iban,
                onValueChange = onIbanChange,
                label = { Text(stringResource(R.string.iban)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
        }

        // Emergency Contact Section
        FormSection(
            title = stringResource(R.string.emergency_contact),
            icon = Icons.Default.ContactPhone
        ) {
            OutlinedTextField(
                value = formState.emergencyContactName,
                onValueChange = onEmergencyContactNameChange,
                label = { Text(stringResource(R.string.contact_name)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            RelationshipDropdown(
                selectedRelationship = formState.emergencyContactRelationship,
                onRelationshipSelected = onEmergencyContactRelationshipChange
            )

            OutlinedTextField(
                value = formState.emergencyContactPhone,
                onValueChange = onEmergencyContactPhoneChange,
                label = { Text(stringResource(R.string.contact_phone)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )

            OutlinedTextField(
                value = formState.emergencyContactEmail,
                onValueChange = onEmergencyContactEmailChange,
                label = { Text(stringResource(R.string.contact_email)) },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
        }

        Spacer(modifier = Modifier.height(8.dp))

        // Action Buttons
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            CleansiaButton(
                text = stringResource(R.string.cancel),
                onClick = onCancel,
                style = CleansiaButtonStyle.OUTLINED,
                modifier = Modifier.weight(1f)
            )
            CleansiaButton(
                text = stringResource(R.string.save),
                onClick = onSave,
                isLoading = isSaving,
                modifier = Modifier.weight(1f)
            )
        }

        Spacer(modifier = Modifier.height(80.dp))
    }
}

@Composable
private fun FormSection(
    title: String,
    icon: ImageVector,
    content: @Composable () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp)
                )
                Spacer(modifier = Modifier.size(8.dp))
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }
            content()
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DatePickerField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit
) {
    var showDatePicker by remember { mutableStateOf(false) }
    val datePickerState = rememberDatePickerState()

    // Display localized date format, but keep API format in the value
    val displayValue = if (value.isNotEmpty()) DateTimeUtils.formatDate(value) else ""

    OutlinedTextField(
        value = displayValue,
        onValueChange = { },
        label = { Text(label) },
        placeholder = { Text(stringResource(R.string.select_date)) },
        readOnly = true,
        modifier = Modifier
            .fillMaxWidth()
            .clickable { showDatePicker = true },
        trailingIcon = {
            Row {
                if (value.isNotEmpty()) {
                    IconButton(onClick = { onValueChange("") }) {
                        Icon(
                            imageVector = Icons.Default.Clear,
                            contentDescription = stringResource(R.string.clear_all),
                            modifier = Modifier.size(20.dp)
                        )
                    }
                }
                IconButton(onClick = { showDatePicker = true }) {
                    Icon(
                        imageVector = Icons.Default.CalendarToday,
                        contentDescription = stringResource(R.string.select_date),
                        modifier = Modifier.size(20.dp)
                    )
                }
            }
        }
    )

    if (showDatePicker) {
        DatePickerDialog(
            onDismissRequest = { showDatePicker = false },
            confirmButton = {
                TextButton(
                    onClick = {
                        datePickerState.selectedDateMillis?.let { millis ->
                            val dateFormat = SimpleDateFormat("yyyy-MM-dd", Locale.getDefault())
                            onValueChange(dateFormat.format(Date(millis)))
                        }
                        showDatePicker = false
                    }
                ) {
                    Text(stringResource(R.string.confirm))
                }
            },
            dismissButton = {
                TextButton(onClick = { showDatePicker = false }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        ) {
            DatePicker(state = datePickerState)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CountryDropdown(
    selectedCountry: String,
    countries: List<Country>,
    onCountrySelected: (String) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it }
    ) {
        OutlinedTextField(
            value = selectedCountry,
            onValueChange = {},
            readOnly = true,
            label = { Text(stringResource(R.string.country)) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = Modifier
                .fillMaxWidth()
                .menuAnchor()
        )

        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            countries.forEach { country ->
                DropdownMenuItem(
                    text = { Text(country.name) },
                    onClick = {
                        onCountrySelected(country.name)
                        expanded = false
                    }
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun RelationshipDropdown(
    selectedRelationship: String,
    onRelationshipSelected: (String) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val relationships = RelationshipType.entries

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it }
    ) {
        OutlinedTextField(
            value = selectedRelationship,
            onValueChange = {},
            readOnly = true,
            label = { Text(stringResource(R.string.relationship)) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = Modifier
                .fillMaxWidth()
                .menuAnchor()
        )

        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            relationships.forEach { relationship ->
                DropdownMenuItem(
                    text = { Text(relationship.displayName) },
                    onClick = {
                        onRelationshipSelected(relationship.displayName)
                        expanded = false
                    }
                )
            }
        }
    }
}
