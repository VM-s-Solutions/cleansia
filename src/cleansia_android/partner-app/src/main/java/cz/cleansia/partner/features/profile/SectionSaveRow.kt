package cz.cleansia.partner.features.profile

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R

/**
 * The save row every profile section ends with — and, inside the onboarding chain, the Back control
 * beside it.
 *
 * Four callers, which is why it is extracted rather than inlined a fourth time.
 *
 * [primaryText] is passed in rather than derived from an `onboarding` flag: Bank is in the chain but
 * still says "Save" (it is the last step — there is nothing to continue to), so a flag here would
 * have to lie at one of the four call sites.
 *
 * [onBack] is non-null only inside the chain AND only when a previous step exists. The profile menu
 * reaches these same screens for a maintenance edit, where there is nothing to go back to and the
 * toolbar arrow is already the way out.
 */
@Composable
fun SectionSaveRow(
    primaryText: String,
    onSave: () -> Unit,
    saving: Boolean,
    enabled: Boolean,
    modifier: Modifier = Modifier,
    onBack: (() -> Unit)? = null,
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        if (onBack != null) {
            CleansiaOutlinedButton(
                text = stringResource(R.string.back),
                onClick = onBack,
                enabled = !saving,
                modifier = Modifier.weight(1f),
            )
        }
        CleansiaPrimaryButton(
            text = primaryText,
            onClick = onSave,
            loading = saving,
            enabled = enabled,
            modifier = Modifier.weight(1f),
        )
    }
}

/**
 * Back is offered only inside the chain, and only when a previous step exists.
 *
 * `ProfileSection.Personal` is index 0, so it returns null and step one shows a single full-width
 * primary — pointing Back at the registration lock there would duplicate the toolbar arrow. Reuses
 * the same jump the step dots added, so the two ways back behave identically.
 */
@Composable
fun onboardingBackFor(
    section: cz.cleansia.partner.features.orders.ProfileSection,
    onboarding: Boolean,
    onJumpToSection: (cz.cleansia.partner.features.orders.ProfileSection) -> Unit,
): (() -> Unit)? {
    if (!onboarding) return null
    val previous = cz.cleansia.partner.features.orders.ProfileSection.values()
        .getOrNull(section.ordinal - 1) ?: return null
    return { onJumpToSection(previous) }
}
