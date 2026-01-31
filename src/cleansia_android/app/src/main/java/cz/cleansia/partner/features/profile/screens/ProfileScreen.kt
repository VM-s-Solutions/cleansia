package cz.cleansia.partner.features.profile.screens

import androidx.compose.foundation.background
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountBalance
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.ContactPhone
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material.icons.filled.Verified
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
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
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.DocumentStatus
import cz.cleansia.partner.domain.models.profile.DocumentType
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.features.profile.components.AvailabilityEditSection
import cz.cleansia.partner.features.profile.components.AvailabilityViewSection
import cz.cleansia.partner.features.profile.components.DocumentManagementSection
import cz.cleansia.partner.features.profile.components.TermsConsentSection
import cz.cleansia.partner.features.profile.viewmodels.ProfileEditFormState
import cz.cleansia.partner.features.profile.viewmodels.ProfileSection
import cz.cleansia.partner.features.profile.viewmodels.ProfileViewModel
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.core.utils.DateTimeUtils

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

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { _ ->
        Box(
            modifier = Modifier
                .fillMaxSize()
        ) {
            when {
                uiState.isLoading -> {
                    LoadingIndicator(
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
                            editingSections = uiState.editingSections,
                            editFormState = uiState.editFormState,
                            savingSection = uiState.savingSection,
                            onStartEditing = { viewModel.startEditingSection(it) },
                            onCancelEditing = { viewModel.cancelEditingSection(it) },
                            onSaveSection = { viewModel.saveSection(it) },
                            onFirstNameChange = { viewModel.updateFirstName(it) },
                            onLastNameChange = { viewModel.updateLastName(it) },
                            onPhoneNumberChange = { viewModel.updatePhoneNumber(it) },
                            onDateOfBirthChange = { viewModel.updateDateOfBirth(it) },
                            onNationalityChange = { viewModel.updateNationality(it) },
                            onNationalIdChange = { viewModel.updateNationalId(it) },
                            onPassportIdChange = { viewModel.updatePassportId(it) },
                            onTaxIdChange = { viewModel.updateTaxId(it) },
                            onStreetChange = { viewModel.updateStreet(it) },
                            onCityChange = { viewModel.updateCity(it) },
                            onZipCodeChange = { viewModel.updateZipCode(it) },
                            onCountryChange = { viewModel.updateCountry(it) },
                            onIbanChange = { viewModel.updateIban(it) },
                            onEmergencyContactNameChange = { viewModel.updateEmergencyContactName(it) },
                            onEmergencyContactRelationshipChange = { viewModel.updateEmergencyContactRelationship(it) },
                            onEmergencyContactPhoneChange = { viewModel.updateEmergencyContactPhone(it) },
                            onEmergencyContactEmailChange = { viewModel.updateEmergencyContactEmail(it) },
                            documents = uiState.documents,
                            availability = uiState.availability,
                            isSavingAvailability = uiState.isSavingAvailability,
                            onAvailabilityChange = { viewModel.updateAvailability(it) },
                            onSaveAvailability = { viewModel.saveAvailability() },
                            isUploadingDocument = uiState.isUploadingDocument,
                            isDeletingDocument = uiState.isDeletingDocument,
                            onUploadDocument = { data, fileName -> viewModel.uploadDocument(data, fileName) },
                            onDeleteDocument = { viewModel.deleteDocument(it) }
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
        }
    }
}

@Composable
private fun ProfileContent(
    listState: LazyListState,
    profile: EmployeeProfile,
    editingSections: Set<ProfileSection>,
    editFormState: ProfileEditFormState,
    savingSection: ProfileSection?,
    onStartEditing: (ProfileSection) -> Unit,
    onCancelEditing: (ProfileSection) -> Unit,
    onSaveSection: (ProfileSection) -> Unit,
    onFirstNameChange: (String) -> Unit,
    onLastNameChange: (String) -> Unit,
    onPhoneNumberChange: (String) -> Unit,
    onDateOfBirthChange: (String) -> Unit,
    onNationalityChange: (String) -> Unit,
    onNationalIdChange: (String) -> Unit,
    onPassportIdChange: (String) -> Unit,
    onTaxIdChange: (String) -> Unit,
    onStreetChange: (String) -> Unit,
    onCityChange: (String) -> Unit,
    onZipCodeChange: (String) -> Unit,
    onCountryChange: (String) -> Unit,
    onIbanChange: (String) -> Unit,
    onEmergencyContactNameChange: (String) -> Unit,
    onEmergencyContactRelationshipChange: (String) -> Unit,
    onEmergencyContactPhoneChange: (String) -> Unit,
    onEmergencyContactEmailChange: (String) -> Unit,
    documents: List<EmployeeDocument>,
    availability: List<DayAvailability>,
    isSavingAvailability: Boolean,
    onAvailabilityChange: (List<DayAvailability>) -> Unit,
    onSaveAvailability: () -> Unit,
    isUploadingDocument: Boolean,
    isDeletingDocument: Boolean,
    onUploadDocument: (ByteArray, String) -> Unit,
    onDeleteDocument: (String) -> Unit
) {
    LazyColumn(
        state = listState,
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 60.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
        modifier = Modifier
            .fillMaxSize()
            .statusBarsPadding()
    ) {
        // Profile Header
        item {
            ProfileHeaderCard(profile = profile)
        }

        // Personal Info
        item {
            val isEditing = editingSections.contains(ProfileSection.PERSONAL)
            ProfileSection(
                title = stringResource(R.string.personal_info),
                icon = Icons.Default.Person,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.PERSONAL,
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
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.lastName,
                        onValueChange = onLastNameChange,
                        label = { Text(stringResource(R.string.last_name)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.phoneNumber,
                        onValueChange = onPhoneNumberChange,
                        label = { Text(stringResource(R.string.phone_number)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.dateOfBirth,
                        onValueChange = onDateOfBirthChange,
                        label = { Text(stringResource(R.string.date_of_birth)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                } else {
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
            ProfileSection(
                title = stringResource(R.string.identification_info),
                icon = Icons.Default.Badge,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.IDENTIFICATION,
                onEditClick = { onStartEditing(ProfileSection.IDENTIFICATION) },
                onSave = { onSaveSection(ProfileSection.IDENTIFICATION) },
                onCancel = { onCancelEditing(ProfileSection.IDENTIFICATION) }
            ) {
                if (isEditing) {
                    TextField(
                        value = editFormState.nationality,
                        onValueChange = onNationalityChange,
                        label = { Text(stringResource(R.string.nationality)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.nationalId,
                        onValueChange = onNationalIdChange,
                        label = { Text(stringResource(R.string.national_id)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.passportId,
                        onValueChange = onPassportIdChange,
                        label = { Text(stringResource(R.string.passport_id)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.taxId,
                        onValueChange = onTaxIdChange,
                        label = { Text(stringResource(R.string.tax_id)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                } else {
                    if (!profile.nationality.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.nationality), value = profile.nationality!!)
                    }
                    if (!profile.nationalId.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.national_id), value = profile.nationalId!!)
                    }
                    if (!profile.passportId.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.passport_id), value = profile.passportId!!)
                    }
                    if (!profile.taxId.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.tax_id), value = profile.taxId!!)
                    }
                    if (profile.nationality.isNullOrBlank() && profile.nationalId.isNullOrBlank() &&
                        profile.passportId.isNullOrBlank() && profile.taxId.isNullOrBlank()) {
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
            ProfileSection(
                title = stringResource(R.string.address_info),
                icon = Icons.Default.LocationOn,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.ADDRESS,
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
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.city,
                        onValueChange = onCityChange,
                        label = { Text(stringResource(R.string.city)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.zipCode,
                        onValueChange = onZipCodeChange,
                        label = { Text(stringResource(R.string.zip_code)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.country,
                        onValueChange = onCountryChange,
                        label = { Text(stringResource(R.string.country)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                } else {
                    if (profile.fullAddress.isNotBlank()) {
                        Text(
                            text = profile.fullAddress,
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
            ProfileSection(
                title = stringResource(R.string.bank_details),
                icon = Icons.Default.AccountBalance,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.BANK,
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
                        colors = profileTextFieldColors()
                    )
                } else {
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
            ProfileSection(
                title = stringResource(R.string.emergency_contact),
                icon = Icons.Default.ContactPhone,
                isEditing = isEditing,
                isSaving = savingSection == ProfileSection.EMERGENCY,
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
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.emergencyContactRelationship,
                        onValueChange = onEmergencyContactRelationshipChange,
                        label = { Text(stringResource(R.string.relationship)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.emergencyContactPhone,
                        onValueChange = onEmergencyContactPhoneChange,
                        label = { Text(stringResource(R.string.contact_phone)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                    TextField(
                        value = editFormState.emergencyContactEmail,
                        onValueChange = onEmergencyContactEmailChange,
                        label = { Text(stringResource(R.string.contact_email)) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = profileTextFieldColors()
                    )
                } else {
                    if (!profile.emergencyContactName.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.contact_name), value = profile.emergencyContactName!!)
                    }
                    if (!profile.emergencyContactRelationship.isNullOrBlank()) {
                        InfoRow(label = stringResource(R.string.relationship), value = profile.emergencyContactRelationship!!)
                    }
                    if (!profile.emergencyContactPhone.isNullOrBlank()) {
                        InfoRow(icon = Icons.Default.Phone, label = stringResource(R.string.contact_phone), value = profile.emergencyContactPhone!!)
                    }
                    if (!profile.emergencyContactEmail.isNullOrBlank()) {
                        InfoRow(icon = Icons.Default.Email, label = stringResource(R.string.contact_email), value = profile.emergencyContactEmail!!)
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

        // Availability Section
        item {
            var isEditingAvailability by remember { mutableStateOf(false) }

            if (isEditingAvailability) {
                Column {
                    AvailabilityEditSection(
                        availability = availability,
                        onAvailabilityChange = onAvailabilityChange
                    )
                    Spacer(modifier = Modifier.height(12.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        OutlinedButton(
                            onClick = { isEditingAvailability = false },
                            modifier = Modifier.weight(1f)
                        ) {
                            Text(stringResource(R.string.cancel))
                        }
                        Button(
                            onClick = {
                                onSaveAvailability()
                                isEditingAvailability = false
                            },
                            enabled = !isSavingAvailability,
                            modifier = Modifier.weight(1f)
                        ) {
                            Text(stringResource(R.string.save))
                        }
                    }
                }
            } else {
                Box {
                    AvailabilityViewSection(
                        availability = availability
                    )
                    IconButton(
                        onClick = { isEditingAvailability = true },
                        modifier = Modifier
                            .align(Alignment.TopEnd)
                            .padding(4.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Edit,
                            contentDescription = stringResource(R.string.edit_profile),
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }
            }
        }

        // Documents with management
        item {
            DocumentManagementSection(
                documents = documents,
                isUploading = isUploadingDocument,
                isDeleting = isDeletingDocument,
                onUploadDocument = onUploadDocument,
                onDeleteDocument = onDeleteDocument
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

@Composable
private fun ProfileHeaderCard(profile: EmployeeProfile) {
    val isDarkTheme = LocalDarkTheme.current
    val gradientColors = if (isDarkTheme) {
        listOf(Color(0xFF1E293B), Color(0xFF0F172A))
    } else {
        listOf(Color(0xFFE0F2FE), Color(0xFFF0F9FF))
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .background(Brush.linearGradient(colors = gradientColors))
                .padding(24.dp)
        ) {
            Column(
                modifier = Modifier.fillMaxWidth(),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Avatar
                Box(
                    modifier = Modifier
                        .size(80.dp)
                        .clip(CircleShape)
                        .background(MaterialTheme.colorScheme.primary),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = profile.fullName.take(2).uppercase(),
                        style = MaterialTheme.typography.headlineMedium,
                        color = MaterialTheme.colorScheme.onPrimary,
                        fontWeight = FontWeight.Bold
                    )
                }

                Spacer(modifier = Modifier.height(16.dp))

                // Name
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.Center
                ) {
                    Text(
                        text = profile.fullName,
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    if (profile.isVerified) {
                        Spacer(modifier = Modifier.width(8.dp))
                        Icon(
                            imageVector = Icons.Default.Verified,
                            contentDescription = "Verified",
                            tint = CleansiaColors.success,
                            modifier = Modifier.size(20.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(4.dp))

                // Email
                Text(
                    text = profile.email,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                // Status badge
                if (!profile.isActive) {
                    Spacer(modifier = Modifier.height(12.dp))
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(12.dp))
                            .background(MaterialTheme.colorScheme.errorContainer)
                            .padding(horizontal = 12.dp, vertical = 4.dp)
                    ) {
                        Text(
                            text = stringResource(R.string.inactive_account),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onErrorContainer
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun ProfileSection(
    title: String,
    icon: ImageVector,
    isEditing: Boolean = false,
    isSaving: Boolean = false,
    onEditClick: (() -> Unit)? = null,
    onSave: (() -> Unit)? = null,
    onCancel: (() -> Unit)? = null,
    content: @Composable () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(if (isEditing) 12.dp else 0.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = icon,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f)
                )
                if (!isEditing && onEditClick != null) {
                    IconButton(
                        onClick = onEditClick,
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Edit,
                            contentDescription = stringResource(R.string.edit_profile),
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }
            }
            Spacer(modifier = Modifier.height(4.dp))
            content()

            // Save/Cancel buttons when editing
            if (isEditing && onSave != null && onCancel != null) {
                Spacer(modifier = Modifier.height(12.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    OutlinedButton(
                        onClick = onCancel,
                        modifier = Modifier.weight(1f)
                    ) {
                        Text(stringResource(R.string.cancel))
                    }
                    Button(
                        onClick = onSave,
                        enabled = !isSaving,
                        modifier = Modifier.weight(1f)
                    ) {
                        Text(if (isSaving) stringResource(R.string.saving) else stringResource(R.string.save))
                    }
                }
            }
        }
    }
}

@Composable
private fun InfoRow(
    icon: ImageVector? = null,
    label: String,
    value: String
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 5.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.weight(1f, fill = false)
        ) {
            if (icon != null) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(16.dp)
                )
                Spacer(modifier = Modifier.width(8.dp))
            }
            Text(
                text = label,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

@Composable
private fun DocumentItem(document: EmployeeDocument) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant)
            .padding(12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = getDocumentTypeName(document.documentType),
                style = MaterialTheme.typography.bodyLarge,
                fontWeight = FontWeight.Medium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            if (!document.fileName.isNullOrBlank()) {
                Text(
                    text = document.fileName!!,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                )
            }
        }
        DocumentStatusBadge(status = document.documentStatus)
    }
}

@Composable
private fun DocumentStatusBadge(status: DocumentStatus) {
    val (backgroundColor, textColor) = when (status) {
        DocumentStatus.PENDING -> CleansiaColors.warningContainer to CleansiaColors.onWarningContainer
        DocumentStatus.APPROVED -> CleansiaColors.successContainer to CleansiaColors.onSuccessContainer
        DocumentStatus.REJECTED -> MaterialTheme.colorScheme.errorContainer to MaterialTheme.colorScheme.onErrorContainer
        DocumentStatus.EXPIRED -> MaterialTheme.colorScheme.secondaryContainer to MaterialTheme.colorScheme.onSecondaryContainer
    }

    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(16.dp))
            .background(backgroundColor)
            .padding(horizontal = 10.dp, vertical = 4.dp)
    ) {
        Text(
            text = status.name.lowercase().replaceFirstChar { it.uppercase() },
            style = MaterialTheme.typography.labelSmall,
            color = textColor,
            maxLines = 1,
            softWrap = false
        )
    }
}

@Composable
private fun getDocumentTypeName(type: DocumentType): String {
    return when (type) {
        DocumentType.ID_CARD -> stringResource(R.string.doc_id_card)
        DocumentType.PASSPORT -> stringResource(R.string.doc_passport)
        DocumentType.DRIVING_LICENSE -> stringResource(R.string.doc_driving_license)
        DocumentType.WORK_PERMIT -> stringResource(R.string.doc_work_permit)
        DocumentType.RESIDENCE_PERMIT -> stringResource(R.string.doc_residence_permit)
        DocumentType.TAX_DOCUMENT -> stringResource(R.string.doc_tax_document)
        DocumentType.OTHER -> stringResource(R.string.doc_other)
    }
}

@Composable
private fun profileTextFieldColors() = TextFieldDefaults.colors(
    focusedContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
    unfocusedContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f),
    focusedIndicatorColor = Color.Transparent,
    unfocusedIndicatorColor = Color.Transparent
)
