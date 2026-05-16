package cz.cleansia.partner.features.profile.screens

import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountBalance
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.ContactPhone
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.TextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.DocumentType
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.features.profile.components.UnifiedAvailabilitySection
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.features.profile.components.DocumentManagementSection
import cz.cleansia.partner.features.profile.components.TermsConsentSection
import cz.cleansia.partner.features.profile.components.sections.ProfileHeaderCard
import cz.cleansia.partner.features.profile.components.sections.ProfileSectionCard
import cz.cleansia.partner.features.profile.components.sections.InfoRow
import cz.cleansia.partner.features.profile.components.sections.MissingFieldsIndicator
import cz.cleansia.partner.features.profile.components.sections.profileTextFieldColors
import cz.cleansia.partner.features.profile.components.sections.validationErrorText
import cz.cleansia.partner.features.profile.viewmodels.ProfileEditFormState
import cz.cleansia.partner.features.profile.viewmodels.ProfileSection
import cz.cleansia.partner.features.profile.viewmodels.ProfileViewModel
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.ui.components.CountryDropdown
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.ui.components.CleansiaPhoneTextField
import cz.cleansia.partner.ui.components.IbanVisualTransformation
import cz.cleansia.partner.features.profile.components.ProfileSkeleton
import cz.cleansia.partner.ui.components.ScrollToTopFab
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProfileScreen(
    onNavigateBack: (() -> Unit)? = null,
    onLogout: () -> Unit,
    viewModel: ProfileViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    var showLogoutDialog by remember { mutableStateOf(false) }
    val profileListState = rememberLazyListState()
    val isScrolled by remember {
        derivedStateOf { profileListState.firstVisibleItemIndex > 0 || profileListState.firstVisibleItemScrollOffset > 0 }
    }
    val headerBgColor = if (isScrolled) MaterialTheme.colorScheme.surface else Color.Transparent

    // Handle logout success
    LaunchedEffect(uiState.logoutSuccess) {
        if (uiState.logoutSuccess) {
            onLogout()
        }
    }

    // Show error in snackbar
    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Show save success
    LaunchedEffect(uiState.saveSuccess) {
        if (uiState.saveSuccess) {
            snackbarHostState.showSnackbar("Profile saved successfully")
            viewModel.clearSaveSuccess()
        }
    }

    // Show upload success
    LaunchedEffect(uiState.uploadSuccess) {
        if (uiState.uploadSuccess) {
            snackbarHostState.showSnackbar("Document uploaded successfully")
            viewModel.clearUploadSuccess()
        }
    }

    // Show photo upload success
    LaunchedEffect(uiState.photoUploadSuccess) {
        if (uiState.photoUploadSuccess) {
            snackbarHostState.showSnackbar("Profile photo updated")
            viewModel.clearPhotoUploadSuccess()
        }
    }

    // Show delete success
    LaunchedEffect(uiState.deleteSuccess) {
        if (uiState.deleteSuccess) {
            snackbarHostState.showSnackbar("Document deleted successfully")
            viewModel.clearDeleteSuccess()
        }
    }

    // Show availability save success
    LaunchedEffect(uiState.availabilitySaveSuccess) {
        if (uiState.availabilitySaveSuccess) {
            snackbarHostState.showSnackbar("Availability saved successfully")
            viewModel.clearAvailabilitySaveSuccess()
        }
    }

    // Logout confirmation dialog
    if (showLogoutDialog) {
        AlertDialog(
            onDismissRequest = { showLogoutDialog = false },
            title = { Text(stringResource(R.string.logout)) },
            text = { Text("Are you sure you want to logout?") },
            confirmButton = {
                TextButton(
                    onClick = {
                        showLogoutDialog = false
                        viewModel.logout()
                    }
                ) {
                    Text(stringResource(R.string.confirm))
                }
            },
            dismissButton = {
                TextButton(onClick = { showLogoutDialog = false }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        )
    }

    Scaffold { _ ->
        Box(
            modifier = Modifier
                .fillMaxSize()
        ) {
            when {
                uiState.isLoading -> {
                    ProfileSkeleton(
                        modifier = Modifier.fillMaxSize()
                    )
                }
                uiState.error != null && uiState.profile == null -> {
                    ErrorView(
                        message = uiState.error ?: "Unknown error",
                        onRetry = { viewModel.loadProfile() },
                        modifier = Modifier.fillMaxSize()
                    )
                }
                uiState.profile != null -> {
                    PullToRefreshBox(
                        isRefreshing = uiState.isRefreshing,
                        onRefresh = { viewModel.refresh() },
                        modifier = Modifier.fillMaxSize()
                    ) {
                        ProfileContent(
                            listState = profileListState,
                            profile = uiState.profile!!,
                            countries = uiState.countries,
                            currentLanguage = viewModel.currentLanguage.collectAsState().value,
                            profilePhotoUri = uiState.profilePhotoUri,
                            onPhotoSelected = { uri -> viewModel.setProfilePhotoUri(uri) },
                            editingSections = uiState.editingSections,
                            editFormState = uiState.editFormState,
                            savingSection = uiState.savingSection,
                            validationErrors = uiState.validationErrors,
                            onStartEditing = { viewModel.startEditingSection(it) },
                            onCancelEditing = { viewModel.cancelEditingSection(it) },
                            onSaveSection = { viewModel.saveSection(it) },
                            onFirstNameChange = { viewModel.updateFirstName(it) },
                            onLastNameChange = { viewModel.updateLastName(it) },
                            onEmailChange = { viewModel.updateEmail(it) },
                            onPhoneNumberChange = { viewModel.updatePhoneNumber(it) },
                            onDateOfBirthChange = { viewModel.updateDateOfBirth(it) },
                            onSelectNationality = { viewModel.selectNationality(it) },
                            onPassportIdChange = { viewModel.updatePassportId(it) },
                            onTaxIdChange = { viewModel.updateTaxId(it) },
                            onStreetChange = { viewModel.updateStreet(it) },
                            onCityChange = { viewModel.updateCity(it) },
                            onZipCodeChange = { viewModel.updateZipCode(it) },
                            onStateChange = { viewModel.updateState(it) },
                            onSelectCountry = { viewModel.selectCountry(it) },
                            onIbanChange = { viewModel.updateIban(it) },
                            onEmergencyContactNameChange = { viewModel.updateEmergencyContactName(it) },
                            onEmergencyContactPhoneChange = { viewModel.updateEmergencyContactPhone(it) },
                            documents = uiState.documents,
                            availability = uiState.availability,
                            dateOverrides = uiState.dateOverrides,
                            isSavingAvailability = uiState.isSavingAvailability,
                            onAvailabilityChange = { viewModel.updateAvailability(it) },
                            onSaveAvailability = { viewModel.saveAvailability() },
                            onAddDateOverride = { viewModel.addDateOverride(it) },
                            onRemoveDateOverride = { viewModel.removeDateOverride(it) },
                            isUploadingDocument = uiState.isUploadingDocument,
                            isDeletingDocument = uiState.isDeletingDocument,
                            onUploadDocument = { data, fileName, docType -> viewModel.uploadDocument(data, fileName, docType) },
                            onDeleteDocument = { viewModel.deleteDocument(it) },
                            onDownloadDocument = { id, name -> viewModel.downloadDocument(id, name) }
                        )
                    }
                }
            }

            if (onNavigateBack != null) {
                val headerShape = RoundedCornerShape(bottomStart = 20.dp, bottomEnd = 20.dp)
                GlassBackButton(
                    onNavigateBack = onNavigateBack,
                    title = stringResource(R.string.profile),
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(headerShape)
                        .background(headerBgColor)
                )
            }

            ScrollToTopFab(
                listState = profileListState,
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(end = 16.dp, bottom = 24.dp)
            )

            CleansiaSnackbarHost(hostState = snackbarHostState)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ProfileContent(
    listState: LazyListState,
    profile: EmployeeProfile,
    countries: List<Country>,
    currentLanguage: String,
    profilePhotoUri: Uri?,
    onPhotoSelected: (Uri?) -> Unit,
    editingSections: Set<ProfileSection>,
    editFormState: ProfileEditFormState,
    savingSection: ProfileSection?,
    validationErrors: Map<String, String>,
    onStartEditing: (ProfileSection) -> Unit,
    onCancelEditing: (ProfileSection) -> Unit,
    onSaveSection: (ProfileSection) -> Unit,
    onFirstNameChange: (String) -> Unit,
    onLastNameChange: (String) -> Unit,
    onEmailChange: (String) -> Unit,
    onPhoneNumberChange: (String) -> Unit,
    onDateOfBirthChange: (String) -> Unit,
    onSelectNationality: (String) -> Unit,
    onPassportIdChange: (String) -> Unit,
    onTaxIdChange: (String) -> Unit,
    onStreetChange: (String) -> Unit,
    onCityChange: (String) -> Unit,
    onZipCodeChange: (String) -> Unit,
    onStateChange: (String) -> Unit,
    onSelectCountry: (String) -> Unit,
    onIbanChange: (String) -> Unit,
    onEmergencyContactNameChange: (String) -> Unit,
    onEmergencyContactPhoneChange: (String) -> Unit,
    documents: List<EmployeeDocument>,
    availability: List<DayAvailability>,
    dateOverrides: List<DateOverride>,
    isSavingAvailability: Boolean,
    onAvailabilityChange: (List<DayAvailability>) -> Unit,
    onSaveAvailability: () -> Unit,
    onAddDateOverride: (DateOverride) -> Unit,
    onRemoveDateOverride: (String) -> Unit,
    isUploadingDocument: Boolean,
    isDeletingDocument: Boolean,
    onUploadDocument: (ByteArray, String, DocumentType) -> Unit,
    onDeleteDocument: (String) -> Unit,
    onDownloadDocument: (String, String) -> Unit
) {
    LazyColumn(
        state = listState,
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
        modifier = Modifier
            .fillMaxSize()
            .statusBarsPadding()
    ) {
        // Profile Header
        item {
            ProfileHeaderCard(
                profile = profile,
                profilePhotoUri = profilePhotoUri,
                onPhotoSelected = onPhotoSelected
            )
        }

        // Personal Info
        item {
            val isEditing = editingSections.contains(ProfileSection.PERSONAL)
            ProfileSectionCard(
                title = stringResource(R.string.personal_info),
                icon = Icons.Default.Person,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.PERSONAL,
                hasMissingFields = profile.missingPersonalFields.isNotEmpty(),
                onEditClick = { onStartEditing(ProfileSection.PERSONAL) },
                onSave = { onSaveSection(ProfileSection.PERSONAL) },
                onCancel = { onCancelEditing(ProfileSection.PERSONAL) }
            ) {
                if (isEditing) {
                    TextField(
                        value = editFormState.firstName,
                        onValueChange = onFirstNameChange,
                        label = { Text(stringResource(R.string.first_name)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("firstName"),
                        supportingText = validationErrors["firstName"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    TextField(
                        value = editFormState.lastName,
                        onValueChange = onLastNameChange,
                        label = { Text(stringResource(R.string.last_name)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("lastName"),
                        supportingText = validationErrors["lastName"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    TextField(
                        value = editFormState.email,
                        onValueChange = onEmailChange,
                        label = { Text(stringResource(R.string.email)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("email"),
                        supportingText = validationErrors["email"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    CleansiaPhoneTextField(
                        value = editFormState.phoneNumber,
                        onValueChange = onPhoneNumberChange,
                        label = stringResource(R.string.phone_number),
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("phoneNumber"),
                        supportingText = validationErrors["phoneNumber"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    // Date of birth with date picker
                    var showDatePicker by remember { mutableStateOf(false) }
                    val initialDateMillis = remember(editFormState.dateOfBirth) {
                        try {
                            val parsed = java.time.LocalDate.parse(editFormState.dateOfBirth)
                            parsed.atStartOfDay(java.time.ZoneOffset.UTC).toInstant().toEpochMilli()
                        } catch (e: Exception) { null }
                    }
                    val datePickerState = rememberDatePickerState(
                        initialSelectedDateMillis = initialDateMillis
                    )
                    // Sync date picker when form state changes
                    LaunchedEffect(initialDateMillis) {
                        initialDateMillis?.let { datePickerState.selectedDateMillis = it }
                    }

                    TextField(
                        value = if (editFormState.dateOfBirth.isNotBlank())
                            DateTimeUtils.formatDate(editFormState.dateOfBirth)
                        else "",
                        onValueChange = {},
                        label = { Text(stringResource(R.string.date_of_birth)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        readOnly = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("dateOfBirth"),
                        supportingText = validationErrors["dateOfBirth"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        },
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
                } else {
                    MissingFieldsIndicator(profile.missingPersonalFields)
                    InfoRow(
                        icon = Icons.Default.Email,
                        label = stringResource(R.string.email),
                        value = profile.email
                    )
                    if (!profile.phoneNumber.isNullOrBlank()) {
                        InfoRow(
                            icon = Icons.Default.Phone,
                            label = stringResource(R.string.phone),
                            value = profile.phoneNumber!!
                        )
                    }
                    if (!profile.dateOfBirth.isNullOrBlank()) {
                        InfoRow(
                            icon = Icons.Default.Person,
                            label = stringResource(R.string.date_of_birth),
                            value = DateTimeUtils.formatDate(profile.dateOfBirth!!)
                        )
                    }
                }
            }
        }

        // Identification Info
        item {
            val isEditing = editingSections.contains(ProfileSection.IDENTIFICATION)
            ProfileSectionCard(
                title = stringResource(R.string.identification_info),
                icon = Icons.Default.Badge,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.IDENTIFICATION,
                hasMissingFields = profile.missingIdentificationFields.isNotEmpty(),
                onEditClick = { onStartEditing(ProfileSection.IDENTIFICATION) },
                onSave = { onSaveSection(ProfileSection.IDENTIFICATION) },
                onCancel = { onCancelEditing(ProfileSection.IDENTIFICATION) }
            ) {
                if (isEditing) {
                    CountryDropdown(
                        label = stringResource(R.string.nationality),
                        selectedCountryId = editFormState.nationalityId,
                        countries = countries,
                        languageCode = currentLanguage,
                        onCountrySelected = onSelectNationality,
                        isError = validationErrors.containsKey("nationalityId"),
                        errorText = validationErrors["nationalityId"]?.let { validationErrorText(it) },
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.passportId,
                        onValueChange = onPassportIdChange,
                        label = { Text(stringResource(R.string.passport_id)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("passportId"),
                        supportingText = validationErrors["passportId"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    TextField(
                        value = editFormState.taxId,
                        onValueChange = onTaxIdChange,
                        label = { Text(stringResource(R.string.tax_id)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("taxId"),
                        supportingText = validationErrors["taxId"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                } else {
                    MissingFieldsIndicator(profile.missingIdentificationFields)
                    val nationalityName = countries.find { it.id == profile.nationalityId }?.getLocalizedName(currentLanguage)
                    if (!nationalityName.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.nationality), value = nationalityName)
                    }
                    if (!profile.passportId.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.passport_id), value = profile.passportId!!)
                    }
                    if (!profile.taxId.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.tax_id), value = profile.taxId!!)
                    }
                    if (nationalityName.isNullOrBlank() && profile.passportId.isNullOrBlank() && profile.taxId.isNullOrBlank()) {
                        Text(
                            text = stringResource(R.string.no_data),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }

        // Address
        item {
            val isEditing = editingSections.contains(ProfileSection.ADDRESS)
            ProfileSectionCard(
                title = stringResource(R.string.address_info),
                icon = Icons.Default.LocationOn,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.ADDRESS,
                hasMissingFields = profile.missingAddressFields.isNotEmpty(),
                onEditClick = { onStartEditing(ProfileSection.ADDRESS) },
                onSave = { onSaveSection(ProfileSection.ADDRESS) },
                onCancel = { onCancelEditing(ProfileSection.ADDRESS) }
            ) {
                if (isEditing) {
                    TextField(
                        value = editFormState.street,
                        onValueChange = onStreetChange,
                        label = { Text(stringResource(R.string.street)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("street"),
                        supportingText = validationErrors["street"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        TextField(
                            value = editFormState.city,
                            onValueChange = onCityChange,
                            label = { Text(stringResource(R.string.city)) },
                            modifier = Modifier.weight(1f),
                            singleLine = true,
                            shape = RoundedCornerShape(12.dp),
                            colors = profileTextFieldColors(),
                            isError = validationErrors.containsKey("city"),
                            supportingText = validationErrors["city"]?.let { key ->
                                { Text(validationErrorText(key)) }
                            }
                        )
                        TextField(
                            value = editFormState.zipCode,
                            onValueChange = onZipCodeChange,
                            label = { Text(stringResource(R.string.zip_code)) },
                            modifier = Modifier.weight(0.6f),
                            singleLine = true,
                            shape = RoundedCornerShape(12.dp),
                            colors = profileTextFieldColors(),
                            isError = validationErrors.containsKey("zipCode"),
                            supportingText = validationErrors["zipCode"]?.let { key ->
                                { Text(validationErrorText(key)) }
                            }
                        )
                    }
                    TextField(
                        value = editFormState.state,
                        onValueChange = onStateChange,
                        label = { Text(stringResource(R.string.state_region)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    CountryDropdown(
                        label = stringResource(R.string.country),
                        selectedCountryId = editFormState.countryId,
                        countries = countries,
                        languageCode = currentLanguage,
                        onCountrySelected = onSelectCountry,
                        isError = validationErrors.containsKey("countryId"),
                        errorText = validationErrors["countryId"]?.let { validationErrorText(it) },
                        colors = profileTextFieldColors()
                    )
                } else {
                    MissingFieldsIndicator(profile.missingAddressFields)
                    val countryName = countries.find { it.id == profile.countryId }?.getLocalizedName(currentLanguage)
                    val addressParts = listOfNotNull(profile.street, profile.city, profile.state, profile.zipCode, countryName)
                        .filter { it.isNotBlank() }
                    if (addressParts.isNotEmpty()) {
                        Text(
                            text = addressParts.joinToString(", "),
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    } else {
                        Text(
                            text = stringResource(R.string.no_data),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }

        // Bank Details
        item {
            val isEditing = editingSections.contains(ProfileSection.BANK)
            ProfileSectionCard(
                title = stringResource(R.string.bank_details),
                icon = Icons.Default.AccountBalance,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.BANK,
                hasMissingFields = profile.missingBankFields.isNotEmpty(),
                onEditClick = { onStartEditing(ProfileSection.BANK) },
                onSave = { onSaveSection(ProfileSection.BANK) },
                onCancel = { onCancelEditing(ProfileSection.BANK) }
            ) {
                if (isEditing) {
                    TextField(
                        value = editFormState.iban,
                        onValueChange = onIbanChange,
                        label = { Text(stringResource(R.string.iban)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        visualTransformation = IbanVisualTransformation(),
                        isError = validationErrors.containsKey("iban"),
                        supportingText = validationErrors["iban"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                } else {
                    MissingFieldsIndicator(profile.missingBankFields)
                    if (!profile.bankName.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.bank_name), value = profile.bankName!!)
                    }
                    if (!profile.accountNumber.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.account_number), value = profile.accountNumber!!)
                    }
                    if (!profile.iban.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.iban), value = profile.iban!!)
                    }
                    if (!profile.swiftCode.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.swift_code), value = profile.swiftCode!!)
                    }
                    if (!profile.hasBankDetails) {
                        Text(
                            text = stringResource(R.string.no_data),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }

        // Emergency Contact
        item {
            val isEditing = editingSections.contains(ProfileSection.EMERGENCY)
            ProfileSectionCard(
                title = stringResource(R.string.emergency_contact),
                icon = Icons.Default.ContactPhone,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.EMERGENCY,
                hasMissingFields = profile.missingEmergencyFields.isNotEmpty(),
                onEditClick = { onStartEditing(ProfileSection.EMERGENCY) },
                onSave = { onSaveSection(ProfileSection.EMERGENCY) },
                onCancel = { onCancelEditing(ProfileSection.EMERGENCY) }
            ) {
                if (isEditing) {
                    TextField(
                        value = editFormState.emergencyContactName,
                        onValueChange = onEmergencyContactNameChange,
                        label = { Text(stringResource(R.string.contact_name)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors(),
                        isError = validationErrors.containsKey("emergencyContactName"),
                        supportingText = validationErrors["emergencyContactName"]?.let { key ->
                            { Text(validationErrorText(key)) }
                        }
                    )
                    CleansiaPhoneTextField(
                        value = editFormState.emergencyContactPhone,
                        onValueChange = onEmergencyContactPhoneChange,
                        label = stringResource(R.string.contact_phone),
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                } else {
                    MissingFieldsIndicator(profile.missingEmergencyFields)
                    if (!profile.emergencyContactName.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.contact_name), value = profile.emergencyContactName!!)
                    }
                    if (!profile.emergencyContactPhone.isNullOrBlank()) {
                        InfoRow(icon = Icons.Default.Phone, label = stringResource(R.string.contact_phone), value = profile.emergencyContactPhone!!)
                    }
                    if (!profile.hasEmergencyContact) {
                        Text(
                            text = stringResource(R.string.no_data),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }

        // Unified Availability & Calendar Section
        item {
            var isEditingAvailability by remember { mutableStateOf(false) }

            UnifiedAvailabilitySection(
                availability = availability,
                dateOverrides = dateOverrides,
                isEditing = isEditingAvailability,
                isSaving = isSavingAvailability,
                onEditToggle = { isEditingAvailability = !isEditingAvailability },
                onSaveAvailability = {
                    onSaveAvailability()
                    isEditingAvailability = false
                },
                onAvailabilityChange = onAvailabilityChange,
                onAddDateOverride = onAddDateOverride,
                onRemoveDateOverride = onRemoveDateOverride
            )
        }

        // Documents with management
        item {
            DocumentManagementSection(
                documents = documents,
                isUploading = isUploadingDocument,
                isDeleting = isDeletingDocument,
                onUploadDocument = onUploadDocument,
                onDeleteDocument = onDeleteDocument,
                onDownloadDocument = onDownloadDocument
            )
        }

        // Terms & Consent Section
        item {
            TermsConsentSection(
                termsAccepted = profile.termsAccepted,
                termsAcceptedAt = profile.termsAcceptedAt
            )
        }

        // Bottom spacing
        item {
            Spacer(modifier = Modifier.height(16.dp))
        }
    }
}
