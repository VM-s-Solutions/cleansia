package cz.cleansia.partner.features.onboarding.screens

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.features.onboarding.components.steps.AddressStep
import cz.cleansia.partner.features.onboarding.components.steps.BankStep
import cz.cleansia.partner.features.onboarding.components.steps.PersonalStep
import cz.cleansia.partner.features.onboarding.components.steps.ScheduleStep
import cz.cleansia.partner.features.onboarding.components.steps.StepIndicatorRow
import cz.cleansia.partner.features.onboarding.viewmodels.CompletionStep
import cz.cleansia.partner.features.onboarding.viewmodels.ProfileCompletionViewModel
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import androidx.compose.foundation.layout.Arrangement

@Composable
fun ProfileCompletionScreen(
    onComplete: () -> Unit,
    viewModel: ProfileCompletionViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    Scaffold { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
        ) {
            // Header
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 24.dp, vertical = 16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(
                    text = stringResource(R.string.complete_your_profile),
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onBackground
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = stringResource(R.string.profile_completion_subtitle),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center
                )
            }

            // Step indicators
            StepIndicatorRow(
                currentStep = uiState.currentStep,
                completedSteps = uiState.completedSteps,
                modifier = Modifier.padding(horizontal = 24.dp)
            )

            // Progress bar
            val progress = (uiState.currentStep.index + 1).toFloat() / CompletionStep.entries.size
            LinearProgressIndicator(
                progress = { progress },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 24.dp, vertical = 12.dp)
                    .clip(RoundedCornerShape(4.dp)),
                trackColor = MaterialTheme.colorScheme.surfaceVariant
            )

            // Animated step content
            AnimatedContent(
                targetState = uiState.currentStep,
                transitionSpec = {
                    if (targetState.index > initialState.index) {
                        slideInHorizontally { it } togetherWith slideOutHorizontally { -it }
                    } else {
                        slideInHorizontally { -it } togetherWith slideOutHorizontally { it }
                    }
                },
                modifier = Modifier.weight(1f),
                label = "stepContent"
            ) { step ->
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .imePadding()
                        .padding(24.dp)
                ) {
                    when (step) {
                        CompletionStep.PERSONAL -> PersonalStep(
                            firstName = uiState.firstName,
                            lastName = uiState.lastName,
                            phoneNumber = uiState.phoneNumber,
                            dateOfBirth = uiState.dateOfBirth,
                            nationalityId = uiState.nationalityId,
                            passportId = uiState.passportId,
                            taxId = uiState.taxId,
                            countries = uiState.countries,
                            languageCode = uiState.currentLanguage,
                            onFirstNameChange = viewModel::updateFirstName,
                            onLastNameChange = viewModel::updateLastName,
                            onPhoneNumberChange = viewModel::updatePhoneNumber,
                            onDateOfBirthChange = viewModel::updateDateOfBirth,
                            onNationalitySelected = viewModel::updateNationalityId,
                            onPassportIdChange = viewModel::updatePassportId,
                            onTaxIdChange = viewModel::updateTaxId
                        )
                        CompletionStep.ADDRESS -> AddressStep(
                            street = uiState.street,
                            city = uiState.city,
                            zipCode = uiState.zipCode,
                            countryId = uiState.countryId,
                            countries = uiState.countries,
                            languageCode = uiState.currentLanguage,
                            onStreetChange = viewModel::updateStreet,
                            onCityChange = viewModel::updateCity,
                            onZipCodeChange = viewModel::updateZipCode,
                            onCountrySelected = viewModel::updateCountryId
                        )
                        CompletionStep.BANK -> BankStep(
                            iban = uiState.iban,
                            emergencyContactName = uiState.emergencyContactName,
                            emergencyContactPhone = uiState.emergencyContactPhone,
                            onIbanChange = viewModel::updateIban,
                            onEmergencyContactNameChange = viewModel::updateEmergencyContactName,
                            onEmergencyContactPhoneChange = viewModel::updateEmergencyContactPhone
                        )
                        CompletionStep.AVAILABILITY -> ScheduleStep(
                            availability = uiState.availability,
                            onAvailabilityChange = viewModel::updateAvailability
                        )
                    }
                }
            }

            // Bottom navigation buttons
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .imePadding()
                    .padding(24.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                if (uiState.currentStep != CompletionStep.PERSONAL) {
                    OutlinedButton(
                        onClick = { viewModel.previousStep() },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text(stringResource(R.string.profile_prev_step))
                    }
                } else {
                    TextButton(
                        onClick = {
                            viewModel.skipForNow()
                            onComplete()
                        }
                    ) {
                        Text(stringResource(R.string.profile_skip_for_now))
                    }
                }

                if (uiState.currentStep == CompletionStep.AVAILABILITY) {
                    Button(
                        onClick = {
                            if (uiState.canFinish) {
                                viewModel.finishProfile()
                                onComplete()
                            } else {
                                viewModel.skipForNow()
                                onComplete()
                            }
                        },
                        modifier = Modifier.weight(1f),
                        enabled = !uiState.isSaving
                    ) {
                        Text(stringResource(R.string.profile_finish))
                    }
                } else {
                    Button(
                        onClick = { viewModel.nextStep() },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text(stringResource(R.string.profile_next_step))
                    }
                }
            }
        }

        CleansiaSnackbarHost(hostState = snackbarHostState)
        }
    }
}
