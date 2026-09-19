package cz.cleansia.partner.features.profile

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R

/**
 * "Identification & business" section. Collects the gating fields the
 * registration lock needs:
 *  - Nationality + passport (the person)
 *  - Business country (picker), registration number / IČO
 *
 * A cleaner contracts as a natural person, so there is no entity type to
 * pick. A row an operator onboarded as a company shows its stored legal
 * name locked, the way the personal section shows the email.
 *
 * Business country defaults to the cleaner's address country so the
 * typical OSVČ-registered-where-I-live case is zero-tap.
 */
@Composable
fun IdentificationSectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    onboarding: Boolean = false,
    /**
     * Tapping a completed step dot in the onboarding header. Defaulted to a no-op because the same
     * screen is reachable from the profile menu, where there is no chain to jump around in.
     */
    onJumpToSection: (cz.cleansia.partner.features.orders.ProfileSection) -> Unit = {},
    viewModel: IdentificationSectionViewModel = hiltViewModel(),
    chainViewModel: cz.cleansia.partner.features.orders.OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val saveState by viewModel.saveState.collectAsStateWithLifecycle()
    val chainState by chainViewModel.state.collectAsStateWithLifecycle()
    val fieldLabels by viewModel.fieldLabels.collectAsStateWithLifecycle()
    val saving = saveState is cz.cleansia.core.ui.state.ActionState.Submitting
    val form = (uiState as? IdentificationSectionUiState.Loaded)?.form ?: IdentificationForm()

    LaunchedEffect(viewModel) { viewModel.saved.collect { onSaved() } }

    val countryOptions = form.countries.map { country ->
        country.id.orEmpty() to country.localizedName()
    }

    SectionScaffold(
        title = stringResource(R.string.identification_title),
        isLoading = uiState is IdentificationSectionUiState.Loading,
        isError = uiState is IdentificationSectionUiState.Error,
        onRetry = viewModel::retry,
        onNavigateBack = onNavigateBack,
        headerSlot = if (!onboarding) null else ({
            cz.cleansia.partner.features.profile.OnboardingChainHeader(
                currentSection = cz.cleansia.partner.features.orders.ProfileSection.Identification,
                state = chainState,
                onSelect = onJumpToSection,
            )
        }),
    ) {
        FormSectionCard(title = stringResource(R.string.identification_header_person)) {
            PickerDropdown(
                selectedId = form.nationalityId,
                options = countryOptions,
                onSelected = viewModel::onNationalitySelected,
                label = stringResource(R.string.nationality),
                enabled = !saving,
                searchable = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = form.passportId,
                onValueChange = viewModel::onPassportChange,
                label = stringResource(R.string.passport_id),
                enabled = !saving,
                transparentContainer = true,
            )
        }

        Spacer(Modifier.height(Spacing.M))

        FormSectionCard(title = stringResource(R.string.identification_header_business)) {
            PickerDropdown(
                selectedId = form.businessCountryId,
                options = countryOptions,
                onSelected = viewModel::onBusinessCountrySelected,
                label = stringResource(R.string.business_country),
                enabled = !saving,
                searchable = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            // The country's own word for these when it has one, our neutral wording when it does
            // not. "Registration number" is correct everywhere and precise nowhere, which is exactly
            // what a fallback should be — flattening every country to it would have cost CZ and SK
            // the term their own registries use.
            CleansiaTextField(
                value = form.registrationNumber,
                onValueChange = viewModel::onRegistrationNumberChange,
                label = fieldLabels?.registrationNumberLabel
                    ?: stringResource(R.string.registration_number_label),
                helper = stringResource(R.string.registration_number_helper),
                enabled = !saving,
                transparentContainer = true,
            )

            form.storedLegalEntityName?.let { storedLegalEntityName ->
                Spacer(Modifier.height(Spacing.XS))
                CleansiaTextField(
                    value = storedLegalEntityName,
                    onValueChange = {},
                    label = stringResource(R.string.legal_entity_name_label),
                    enabled = false,
                    transparentContainer = true,
                )
            }
        }

        Spacer(Modifier.height(Spacing.L))

        SectionSaveRow(
            primaryText = stringResource(
                if (onboarding) R.string.onboarding_next else R.string.save,
            ),
            onBack = onboardingBackFor(cz.cleansia.partner.features.orders.ProfileSection.Identification, onboarding, onJumpToSection),
            onSave = { viewModel.save() },
            saving = saving,
            enabled = !saving,
        )
    }
}
