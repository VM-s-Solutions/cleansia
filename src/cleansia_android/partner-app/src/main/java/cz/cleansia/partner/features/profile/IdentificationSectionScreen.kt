package cz.cleansia.partner.features.profile

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectHorizontalDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
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
            EntityTypeSelector(
                selected = form.entityType,
                onSelect = viewModel::onEntityTypeSelected,
                enabled = !saving,
            )
            Spacer(Modifier.height(Spacing.S))

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
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = form.vatNumber,
                onValueChange = viewModel::onVatNumberChange,
                label = fieldLabels?.vatNumberLabel ?: stringResource(R.string.vat_number_label),
                helper = stringResource(R.string.vat_number_helper),
                enabled = !saving,
                transparentContainer = true,
            )

            // Legal entity name surfaces only for s.r.o.-style cleaners.
            // Animated visibility keeps the field out of the layout when
            // not applicable so the form stays tight for OSVČ/natural-
            // person (the common case).
            AnimatedVisibility(visible = form.entityType == EmployeeEntityType._2) {
                Box {
                    Spacer(Modifier.height(Spacing.XS))
                    CleansiaTextField(
                        value = form.legalEntityName,
                        onValueChange = viewModel::onLegalEntityNameChange,
                        label = stringResource(R.string.legal_entity_name_label),
                        enabled = !saving,
                        transparentContainer = true,
                    )
                }
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

/**
 * Self-employed / Legal entity, as one track with a selection thumb that slides between halves.
 *
 * The shape is the Cleansia Plus plan switcher the owner pointed at. The selection used to be a
 * per-chip colour swap on recomposition, so it JUMPED; it is now a single thumb that animates its
 * offset, on the same spring as the iOS twin.
 *
 * A drag flips the selection past a threshold rather than tracking the finger — the thumb is bound
 * to the selection, so following the finger could park it between the two halves. Both scaffolds
 * put this form inside a vertical scroll, which is also why this is not a HorizontalPager: a
 * paging container inside a vertical scroll fights the scroll on every diagonal drag, and with
 * only legalEntityName differing between the two types the panes would be near-identical anyway.
 */
@Composable
private fun EntityTypeSelector(
    selected: EmployeeEntityType,
    onSelect: (EmployeeEntityType) -> Unit,
    enabled: Boolean,
) {
    val isLegal = selected == EmployeeEntityType._2
    val labels = listOf(
        stringResource(R.string.entity_type_natural_person) to EmployeeEntityType._1,
        stringResource(R.string.entity_type_legal_entity) to EmployeeEntityType._2,
    )

    BoxWithConstraints(
        modifier = Modifier
            .fillMaxWidth()
            .height(48.dp)
            .clip(RoundedCornerShape(50))
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
            .pointerInput(enabled, isLegal) {
                if (!enabled) return@pointerInput
                detectHorizontalDragGestures { _, dragAmount ->
                    if (dragAmount < -DRAG_FLIP_THRESHOLD && !isLegal) {
                        onSelect(EmployeeEntityType._2)
                    } else if (dragAmount > DRAG_FLIP_THRESHOLD && isLegal) {
                        onSelect(EmployeeEntityType._1)
                    }
                }
            }
            .padding(4.dp),
    ) {
        val segment = (maxWidth - 8.dp) / 2
        val offset by animateDpAsState(
            targetValue = if (isLegal) segment else 0.dp,
            animationSpec = spring(dampingRatio = 0.86f, stiffness = 900f),
            label = "entityTypeThumb",
        )

        Box(
            modifier = Modifier
                .offset(x = offset)
                .width(segment)
                .fillMaxHeight()
                .clip(RoundedCornerShape(50))
                .background(MaterialTheme.colorScheme.primary),
        )

        Row(modifier = Modifier.fillMaxSize()) {
            labels.forEachIndexed { index, (label, type) ->
                val isSelected = (index == 1) == isLegal
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxHeight()
                        .clip(RoundedCornerShape(50))
                        .clickable(enabled = enabled) { onSelect(type) }
                        .semantics { this.selected = isSelected },
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = label,
                        style = MaterialTheme.typography.labelLarge,
                        color = if (isSelected) {
                            MaterialTheme.colorScheme.onPrimary
                        } else {
                            MaterialTheme.colorScheme.onSurfaceVariant
                        },
                        fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Medium,
                        maxLines = 1,
                    )
                }
            }
        }
    }
}

/** Enough travel that a vertical scroll that wanders sideways does not change the answer. */
private const val DRAG_FLIP_THRESHOLD = 12f
