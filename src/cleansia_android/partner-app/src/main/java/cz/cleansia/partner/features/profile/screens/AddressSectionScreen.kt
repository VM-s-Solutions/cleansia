package cz.cleansia.partner.features.profile.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.outlined.HelpOutline
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.ExpandLess
import androidx.compose.material.icons.outlined.ExpandMore
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.viewmodels.ProfileSection
import cz.cleansia.partner.features.profile.components.OnboardingChainHeader
import cz.cleansia.partner.features.profile.components.SectionScaffold
import cz.cleansia.partner.features.profile.viewmodels.AddressSectionViewModel
import cz.cleansia.partner.features.profile.viewmodels.ServiceAreaStatus

/**
 * Address section v2 — the cleaner picks their home address on a
 * full-screen Mapbox map ([AddressPickerScreen]) and this screen
 * displays the result in a tappable summary card. State (US/CA only)
 * stays inline as an optional text field.
 *
 * [onLaunchPicker] navigates to the picker route. [pickerResult] is
 * the `GeocodedAddress` returned via SavedStateHandle when the picker
 * pops — the NavHost wrapper reads and consumes it then forwards via
 * this callback. Decoupled so the NavHost owns the
 * SavedStateHandle plumbing, screen stays composable-pure.
 */
@Composable
fun AddressSectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    onLaunchPicker: () -> Unit,
    pickerResult: GeocodedAddress?,
    onPickerResultConsumed: () -> Unit,
    onboarding: Boolean = false,
    viewModel: AddressSectionViewModel = hiltViewModel(),
    chainViewModel: cz.cleansia.partner.features.orders.viewmodels.OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()
    val chainState by chainViewModel.state.collectAsState()

    LaunchedEffect(uiState.isSaved) {
        if (uiState.isSaved) onSaved()
    }

    // Picker → section handoff. NavHost stashes the pick into our
    // SavedStateHandle and surfaces it here as [pickerResult]; we
    // forward it into VM state and tell NavHost to clear the slot so
    // re-entering the section without re-picking doesn't replay the
    // same pin.
    LaunchedEffect(pickerResult) {
        val pick = pickerResult ?: return@LaunchedEffect
        viewModel.applyPick(pick)
        onPickerResultConsumed()
    }

    SectionScaffold(
        title = stringResource(R.string.address),
        isLoading = uiState.isLoading,
        onNavigateBack = onNavigateBack,
        headerSlot = if (onboarding) {
            {
                OnboardingChainHeader(
                    currentSection = ProfileSection.Address,
                    state = chainState,
                )
            }
        } else null,
    ) {
        AddressSummaryCard(
            line1 = uiState.summaryLine1,
            line2 = uiState.summaryLine2,
            enabled = !uiState.isSaving,
            onClick = onLaunchPicker,
        )

        if (uiState.pickedAddress != null) {
            Spacer(Modifier.height(Spacing.S))
            ServiceAreaRow(status = uiState.serviceAreaStatus)
        }

        Spacer(Modifier.height(Spacing.M))

        WhyWeNeedThisCard()

        Spacer(Modifier.height(Spacing.L))

        CleansiaPrimaryButton(
            text = stringResource(
                if (onboarding) R.string.save_and_continue else R.string.save,
            ),
            onClick = { viewModel.save() },
            loading = uiState.isSaving,
            enabled = !uiState.isSaving && uiState.pickedAddress != null,
        )
    }
}

/**
 * Tappable card that opens the map picker. When nothing is picked yet
 * shows a "Pick on map" placeholder; once picked, shows two lines
 * (street, then zip · city · country) with a chevron to indicate the
 * card is still tappable for re-picking.
 */
@Composable
private fun AddressSummaryCard(
    line1: String?,
    line2: String?,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .clickable(enabled = enabled, onClick = onClick),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        tonalElevation = 1.dp,
    ) {
        Row(
            modifier = Modifier.padding(Spacing.M),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .clip(RoundedCornerShape(50))
                    .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.10f)),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = Icons.Outlined.Place,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
            }
            Spacer(Modifier.width(Spacing.S))
            Column(modifier = Modifier.weight(1f)) {
                if (line1 != null) {
                    Text(
                        text = line1,
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 1,
                    )
                    if (line2 != null) {
                        Text(
                            text = line2,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1,
                        )
                    }
                } else {
                    Text(
                        text = stringResource(R.string.address_pick_on_map),
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    Text(
                        text = stringResource(R.string.address_pick_on_map_helper),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}

/**
 * Inline service-area indicator. Three render states + a loading
 * skeleton:
 *  - Unknown → soft pulsing row "Checking service area…"
 *  - InServicedCity → green ✓ "We service jobs in {city}"
 *  - OutsideServicedCity → amber info "You can still take jobs in
 *    nearby serviced cities" (city restriction applies to customer
 *    orders, not cleaner addresses)
 *  - CountryNotServiced → red ! "We don't service this country yet"
 *    (save will be rejected by the backend's CountryNotServiced rule)
 */
@Composable
private fun ServiceAreaRow(status: ServiceAreaStatus) {
    val (icon, tint, text) = when (status) {
        ServiceAreaStatus.Unknown -> Triple(
            Icons.Outlined.Info,
            MaterialTheme.colorScheme.onSurfaceVariant,
            stringResource(R.string.address_service_area_checking),
        )
        is ServiceAreaStatus.InServicedCity -> Triple(
            Icons.Outlined.CheckCircle,
            MaterialTheme.colorScheme.primary,
            stringResource(R.string.address_service_area_in_serviced_city, status.cityName),
        )
        ServiceAreaStatus.OutsideServicedCity -> Triple(
            Icons.Outlined.Info,
            MaterialTheme.colorScheme.tertiary,
            stringResource(R.string.address_service_area_outside_serviced_city),
        )
        ServiceAreaStatus.CountryNotServiced -> Triple(
            Icons.Outlined.ErrorOutline,
            MaterialTheme.colorScheme.error,
            stringResource(R.string.address_service_area_country_not_serviced),
        )
    }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(tint.copy(alpha = 0.08f))
            .padding(horizontal = Spacing.S, vertical = Spacing.XS),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = tint,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(8.dp))
        Text(
            text = text,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/**
 * Collapsed-by-default "Why we need this" explainer. Builds trust at
 * onboarding ("why do you want my home address") without inventing form
 * fields. Three concrete reasons rendered as bullet rows so the cleaner
 * sees exactly what their address powers.
 */
@Composable
private fun WhyWeNeedThisCard() {
    var expanded by remember { mutableStateOf(false) }
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .clickable { expanded = !expanded },
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.HelpOutline,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
                Spacer(Modifier.width(Spacing.S))
                Text(
                    text = stringResource(R.string.address_why_title),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                Icon(
                    imageVector = if (expanded) Icons.Outlined.ExpandLess
                    else Icons.Outlined.ExpandMore,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
            AnimatedVisibility(visible = expanded) {
                Column(
                    modifier = Modifier.padding(top = Spacing.S),
                    verticalArrangement = Arrangement.spacedBy(Spacing.XS),
                ) {
                    WhyRow(text = stringResource(R.string.address_why_reason_jobs))
                    WhyRow(text = stringResource(R.string.address_why_reason_distance_pay))
                    WhyRow(text = stringResource(R.string.address_why_reason_invoice))
                    Spacer(Modifier.height(Spacing.XXS))
                    Text(
                        text = stringResource(R.string.address_why_privacy),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }
}

@Composable
private fun WhyRow(text: String) {
    Row(verticalAlignment = Alignment.Top) {
        Text(
            text = "•",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.primary,
        )
        Spacer(Modifier.width(8.dp))
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}
