package cz.cleansia.partner.features.onboarding.components.steps

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountBalance
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.ContactPhone
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.features.profile.components.AvailabilityEditSection
import cz.cleansia.partner.features.profile.components.availability.QuickSetupPresets
import cz.cleansia.partner.ui.components.CountryDropdown
import cz.cleansia.partner.ui.components.CleansiaTextField
import cz.cleansia.partner.ui.components.PhoneVisualTransformation
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun PersonalStep(
    firstName: String,
    lastName: String,
    phoneNumber: String,
    dateOfBirth: String,
    nationalityId: String,
    passportId: String,
    taxId: String,
    countries: List<Country>,
    languageCode: String,
    onFirstNameChange: (String) -> Unit,
    onLastNameChange: (String) -> Unit,
    onPhoneNumberChange: (String) -> Unit,
    onDateOfBirthChange: (String) -> Unit,
    onNationalitySelected: (String) -> Unit,
    onPassportIdChange: (String) -> Unit,
    onTaxIdChange: (String) -> Unit
) {
    SectionHeader(
        icon = Icons.Default.Person,
        title = stringResource(R.string.personal_details)
    )
    Spacer(modifier = Modifier.height(16.dp))

    CleansiaTextField(
        value = firstName,
        onValueChange = onFirstNameChange,
        label = stringResource(R.string.first_name),
        leadingIcon = Icons.Default.Person,
        imeAction = ImeAction.Next
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = lastName,
        onValueChange = onLastNameChange,
        label = stringResource(R.string.last_name),
        leadingIcon = Icons.Default.Person,
        imeAction = ImeAction.Next
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = phoneNumber,
        onValueChange = onPhoneNumberChange,
        label = stringResource(R.string.phone_number),
        keyboardType = KeyboardType.Phone,
        imeAction = ImeAction.Next,
        visualTransformation = PhoneVisualTransformation()
    )
    Spacer(modifier = Modifier.height(12.dp))

    // Date of birth with date picker
    var showDatePicker by remember { mutableStateOf(false) }
    val initialDateMillis = remember(dateOfBirth) {
        try {
            val parsed = java.time.LocalDate.parse(dateOfBirth)
            parsed.atStartOfDay(java.time.ZoneOffset.UTC).toInstant().toEpochMilli()
        } catch (e: Exception) { null }
    }
    val datePickerState = rememberDatePickerState(
        initialSelectedDateMillis = initialDateMillis
    )
    LaunchedEffect(initialDateMillis) {
        initialDateMillis?.let { datePickerState.selectedDateMillis = it }
    }

    TextField(
        value = if (dateOfBirth.isNotBlank())
            DateTimeUtils.formatDate(dateOfBirth)
        else "",
        onValueChange = {},
        label = { Text(stringResource(R.string.date_of_birth)) },
        modifier = Modifier.fillMaxWidth(),
        singleLine = true,
        readOnly = true,
        shape = RoundedCornerShape(12.dp),
        colors = TextFieldDefaults.colors(
            focusedContainerColor = MaterialTheme.colorScheme.surfaceVariant,
            unfocusedContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.7f),
            focusedIndicatorColor = Color.Transparent,
            unfocusedIndicatorColor = Color.Transparent
        ),
        trailingIcon = {
            IconButton(onClick = { showDatePicker = true }) {
                Icon(
                    imageVector = Icons.Default.CalendarToday,
                    contentDescription = stringResource(R.string.select_date),
                    modifier = Modifier.size(20.dp)
                )
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
                            val dateFormat = SimpleDateFormat("yyyy-MM-dd", Locale.US)
                            dateFormat.timeZone = java.util.TimeZone.getTimeZone("UTC")
                            onDateOfBirthChange(dateFormat.format(Date(millis)))
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

    // Identification section
    Spacer(modifier = Modifier.height(24.dp))
    SectionHeader(
        icon = Icons.Default.Badge,
        title = stringResource(R.string.identification_info)
    )
    Spacer(modifier = Modifier.height(12.dp))

    CountryDropdown(
        label = stringResource(R.string.nationality),
        selectedCountryId = nationalityId,
        countries = countries,
        languageCode = languageCode,
        onCountrySelected = onNationalitySelected
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = passportId,
        onValueChange = onPassportIdChange,
        label = stringResource(R.string.passport_id),
        imeAction = ImeAction.Next
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = taxId,
        onValueChange = onTaxIdChange,
        label = stringResource(R.string.tax_id),
        imeAction = ImeAction.Done
    )
}

@Composable
internal fun AddressStep(
    street: String,
    city: String,
    zipCode: String,
    countryId: String,
    countries: List<Country>,
    languageCode: String,
    onStreetChange: (String) -> Unit,
    onCityChange: (String) -> Unit,
    onZipCodeChange: (String) -> Unit,
    onCountrySelected: (String) -> Unit
) {
    SectionHeader(
        icon = Icons.Default.LocationOn,
        title = stringResource(R.string.address_details)
    )
    Spacer(modifier = Modifier.height(16.dp))

    CleansiaTextField(
        value = street,
        onValueChange = onStreetChange,
        label = stringResource(R.string.street),
        leadingIcon = Icons.Default.LocationOn,
        imeAction = ImeAction.Next
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = city,
        onValueChange = onCityChange,
        label = stringResource(R.string.city),
        imeAction = ImeAction.Next
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = zipCode,
        onValueChange = onZipCodeChange,
        label = stringResource(R.string.zip_code),
        keyboardType = KeyboardType.Number,
        imeAction = ImeAction.Done
    )
    Spacer(modifier = Modifier.height(12.dp))

    CountryDropdown(
        label = stringResource(R.string.country),
        selectedCountryId = countryId,
        countries = countries,
        languageCode = languageCode,
        onCountrySelected = onCountrySelected
    )
}

@Composable
internal fun BankStep(
    iban: String,
    emergencyContactName: String,
    emergencyContactPhone: String,
    onIbanChange: (String) -> Unit,
    onEmergencyContactNameChange: (String) -> Unit,
    onEmergencyContactPhoneChange: (String) -> Unit
) {
    SectionHeader(
        icon = Icons.Default.AccountBalance,
        title = stringResource(R.string.banking_details)
    )
    Spacer(modifier = Modifier.height(16.dp))

    CleansiaTextField(
        value = iban,
        onValueChange = onIbanChange,
        label = stringResource(R.string.iban),
        leadingIcon = Icons.Default.AccountBalance,
        imeAction = ImeAction.Done
    )

    // Emergency contact section
    Spacer(modifier = Modifier.height(24.dp))
    SectionHeader(
        icon = Icons.Default.ContactPhone,
        title = stringResource(R.string.emergency_contact),
        subtitle = stringResource(R.string.emergency_contact_desc)
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = emergencyContactName,
        onValueChange = onEmergencyContactNameChange,
        label = stringResource(R.string.contact_name),
        leadingIcon = Icons.Default.Person,
        imeAction = ImeAction.Next
    )
    Spacer(modifier = Modifier.height(12.dp))

    CleansiaTextField(
        value = emergencyContactPhone,
        onValueChange = onEmergencyContactPhoneChange,
        label = stringResource(R.string.contact_phone),
        keyboardType = KeyboardType.Phone,
        imeAction = ImeAction.Done,
        visualTransformation = PhoneVisualTransformation()
    )
}

@Composable
internal fun ScheduleStep(
    availability: List<DayAvailability>,
    onAvailabilityChange: (List<DayAvailability>) -> Unit
) {
    SectionHeader(
        icon = Icons.Default.Schedule,
        title = stringResource(R.string.schedule_setup),
        subtitle = stringResource(R.string.schedule_setup_desc)
    )
    Spacer(modifier = Modifier.height(16.dp))

    QuickSetupPresets(
        onPresetSelected = onAvailabilityChange
    )

    Spacer(modifier = Modifier.height(16.dp))

    AvailabilityEditSection(
        availability = availability,
        onAvailabilityChange = onAvailabilityChange
    )
}
