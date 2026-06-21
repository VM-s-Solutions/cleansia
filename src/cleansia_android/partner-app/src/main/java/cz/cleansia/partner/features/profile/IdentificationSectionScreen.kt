package cz.cleansia.partner.features.profile

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeEntityType

/**
 * "Identification & business" section. Collects the gating fields the
 * registration lock needs:
 *  - Nationality + passport (the person)
 *  - Entity type (segmented control), business country (picker),
 *    registration number / IČO, optional VAT, legal entity name when
 *    entity type = Legal entity
 *
 * Business country defaults to the cleaner's address country so the
 * typical OSVČ-registered-where-I-live case is zero-tap.
 */
@Composable
fun IdentificationSectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    onboarding: Boolean = false,
    viewModel: IdentificationSectionViewModel = hiltViewModel(),
    chainViewModel: cz.cleansia.partner.features.orders.OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val chainState by chainViewModel.state.collectAsStateWithLifecycle()

    LaunchedEffect(uiState.isSaved) { if (uiState.isSaved) onSaved() }

    val countryOptions = uiState.countries.map { country ->
        country.id.orEmpty() to (country.name ?: country.isoCode ?: country.id.orEmpty())
    }

    SectionScaffold(
        title = stringResource(R.string.identification_title),
        isLoading = uiState.isLoading,
        onNavigateBack = onNavigateBack,
        headerSlot = if (!onboarding) null else ({
            cz.cleansia.partner.features.profile.OnboardingChainHeader(
                currentSection = cz.cleansia.partner.features.orders.ProfileSection.Identification,
                state = chainState,
            )
        }),
    ) {
        FormSectionCard(title = stringResource(R.string.identification_header_person)) {
            PickerDropdown(
                selectedId = uiState.nationalityId,
                options = countryOptions,
                onSelected = viewModel::onNationalitySelected,
                label = stringResource(R.string.nationality),
                enabled = !uiState.isSaving,
                searchable = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = uiState.passportId,
                onValueChange = viewModel::onPassportChange,
                label = stringResource(R.string.passport_id),
                enabled = !uiState.isSaving,
                transparentContainer = true,
            )
        }

        Spacer(Modifier.height(Spacing.M))

        FormSectionCard(title = stringResource(R.string.identification_header_business)) {
            EntityTypeSelector(
                selected = uiState.entityType,
                onSelect = viewModel::onEntityTypeSelected,
                enabled = !uiState.isSaving,
            )
            Spacer(Modifier.height(Spacing.S))

            PickerDropdown(
                selectedId = uiState.businessCountryId,
                options = countryOptions,
                onSelected = viewModel::onBusinessCountrySelected,
                label = stringResource(R.string.business_country),
                enabled = !uiState.isSaving,
                searchable = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = uiState.registrationNumber,
                onValueChange = viewModel::onRegistrationNumberChange,
                label = stringResource(R.string.registration_number_label),
                helper = stringResource(R.string.registration_number_helper),
                enabled = !uiState.isSaving,
                transparentContainer = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = uiState.vatNumber,
                onValueChange = viewModel::onVatNumberChange,
                label = stringResource(R.string.vat_number_label),
                helper = stringResource(R.string.vat_number_helper),
                enabled = !uiState.isSaving,
                transparentContainer = true,
            )

            // Legal entity name surfaces only for s.r.o.-style cleaners.
            // Animated visibility keeps the field out of the layout when
            // not applicable so the form stays tight for OSVČ/natural-
            // person (the common case).
            AnimatedVisibility(visible = uiState.entityType == EmployeeEntityType._2) {
                Box {
                    Spacer(Modifier.height(Spacing.XS))
                    CleansiaTextField(
                        value = uiState.legalEntityName,
                        onValueChange = viewModel::onLegalEntityNameChange,
                        label = stringResource(R.string.legal_entity_name_label),
                        enabled = !uiState.isSaving,
                        transparentContainer = true,
                    )
                }
            }
        }

        Spacer(Modifier.height(Spacing.L))

        CleansiaPrimaryButton(
            text = stringResource(
                if (onboarding) R.string.save_and_continue else R.string.save,
            ),
            onClick = { viewModel.save() },
            loading = uiState.isSaving,
            enabled = !uiState.isSaving,
        )
    }
}

@Composable
private fun EntityTypeSelector(
    selected: EmployeeEntityType,
    onSelect: (EmployeeEntityType) -> Unit,
    enabled: Boolean,
) {
    // Two-button segmented control. Wider than chips so the cleaner can
    // see both labels in full without truncation across locales.
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .height(48.dp),
        shape = RoundedCornerShape(50),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
    ) {
        Row(
            modifier = Modifier
                .fillMaxSize()
                .padding(4.dp),
            horizontalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            EntityTypeChip(
                label = stringResource(R.string.entity_type_natural_person),
                selected = selected == EmployeeEntityType._1,
                onClick = { onSelect(EmployeeEntityType._1) },
                enabled = enabled,
                modifier = Modifier.weight(1f),
            )
            EntityTypeChip(
                label = stringResource(R.string.entity_type_legal_entity),
                selected = selected == EmployeeEntityType._2,
                onClick = { onSelect(EmployeeEntityType._2) },
                enabled = enabled,
                modifier = Modifier.weight(1f),
            )
        }
    }
}

@Composable
private fun EntityTypeChip(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
    enabled: Boolean,
    modifier: Modifier = Modifier,
) {
    Surface(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier.fillMaxSize(),
        shape = RoundedCornerShape(50),
        color = if (selected) MaterialTheme.colorScheme.surface else androidx.compose.ui.graphics.Color.Transparent,
        shadowElevation = if (selected) 2.dp else 0.dp,
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                color = if (selected) MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.onSurfaceVariant,
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Medium,
            )
        }
    }
}
