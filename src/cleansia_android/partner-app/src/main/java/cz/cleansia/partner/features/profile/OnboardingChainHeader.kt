package cz.cleansia.partner.features.profile

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AccountBalance
import androidx.compose.material.icons.outlined.Badge
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.ripple
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.OnboardingChainState
import cz.cleansia.partner.features.orders.ProfileSection

private val DISC = 40.dp
private val TARGET = 48.dp

/**
 * The onboarding stepper.
 *
 * **Rebuilt from the plain numbered dots it used to be.** Three things carry state now, one bit each,
 * so no single failure of colour perception loses the whole picture:
 *
 * - the **disc fill** says where you are — solid `primary` for the current step, a washed
 *   `primaryContainer` for one that is finished, nothing at all for one you have not reached;
 * - the **glyph** says whether it is finished — a checkmark once it is, otherwise an icon that names
 *   the step, because a bare ordinal tells a cleaner nothing about what is being asked of them;
 * - the **ring** says whether you may go there — `primary` on a step you can jump to, `outline` on
 *   one you cannot.
 *
 * **Only the current step is named.** Four Cyrillic labels do not fit across four medallions on a
 * 320dp screen: the cell budget is 64dp and "Идентификация" needs roughly twice that.
 *
 * **No green, and no elevation.** Reference designs for this pattern are drawn on white; the success
 * token measures 2.92:1 on this app's dark surface and a shadow is invisible against it. Progress is
 * carried by the connector tinting `primary` behind you — which is why the separate progress bar this
 * used to sit above is gone. It said the same thing twice.
 *
 * Built to the same numbers as the iOS twin: 40 disc, 48 target, 2 connector, 20 glyph, 1.5 ring.
 */
@Composable
fun OnboardingChainHeader(
    currentSection: ProfileSection,
    state: OnboardingChainState,
    onSelect: (ProfileSection) -> Unit,
) {
    val sections = ProfileSection.values().toList()
    val currentIndex = sections.indexOf(currentSection)

    fun isDone(section: ProfileSection) = state.completionByCategory[section] == true

    // Reachable = already finished, or already walked past. Not "any step": jumping forward into a
    // section the chain has not filled yet would leave a gap the chain then has to re-find.
    fun isReachable(index: Int) = isDone(sections[index]) || index < currentIndex

    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        border = androidx.compose.foundation.BorderStroke(
            1.dp,
            MaterialTheme.colorScheme.outlineVariant,
        ),
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = stringResource(
                        R.string.onboarding_step_progress,
                        currentIndex + 1,
                        state.totalSteps,
                    ),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.width(Spacing.S))
                // Truncates before the counter does: losing "Complete your profile" costs nothing,
                // losing "Step 3 of 4" costs the reader their place.
                Text(
                    text = stringResource(R.string.onboarding_header_subtitle),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f, fill = true),
                )
            }

            Spacer(Modifier.height(Spacing.M))

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                sections.forEachIndexed { index, section ->
                    StepMedallion(
                        icon = iconFor(section),
                        isDone = isDone(section),
                        isCurrent = section == currentSection,
                        isReachable = isReachable(index),
                        label = stringResource(labelResFor(section)),
                        onTap = { onSelect(section) },
                    )
                    if (index < sections.lastIndex) {
                        // The segment behind a finished step is the progress indicator. Two tones of
                        // primary, never outlineVariant — slate700 on this card measures 1.51:1.
                        Box(
                            modifier = Modifier
                                .weight(1f)
                                .height(2.dp)
                                .background(
                                    if (isDone(section)) {
                                        MaterialTheme.colorScheme.primary
                                    } else {
                                        MaterialTheme.colorScheme.primary.copy(alpha = 0.24f)
                                    },
                                ),
                        )
                    }
                }
            }

            Spacer(Modifier.height(Spacing.M))

            Text(
                text = stringResource(labelResFor(currentSection)),
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

@Composable
private fun StepMedallion(
    icon: ImageVector,
    isDone: Boolean,
    isCurrent: Boolean,
    isReachable: Boolean,
    label: String,
    onTap: () -> Unit,
) {
    val interactionSource = remember { MutableInteractionSource() }
    val pressed by interactionSource.collectIsPressedAsState()
    val scale by animateFloatAsState(if (pressed) 0.94f else 1f, label = "medallionPress")

    val fill = when {
        isCurrent -> MaterialTheme.colorScheme.primary
        isDone -> MaterialTheme.colorScheme.primaryContainer
        else -> Color.Transparent
    }
    val ring = if (isReachable) {
        MaterialTheme.colorScheme.primary
    } else {
        MaterialTheme.colorScheme.outline
    }
    val glyph = when {
        isCurrent -> MaterialTheme.colorScheme.onPrimary
        isDone -> MaterialTheme.colorScheme.onPrimaryContainer
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }
    val stateDescription = stringResource(
        when {
            isCurrent -> R.string.onboarding_step_state_current
            isDone -> R.string.onboarding_step_state_done
            else -> R.string.onboarding_step_state_upcoming
        },
    )

    Box(
        modifier = Modifier
            .size(TARGET)
            .scale(scale)
            .clickable(
                interactionSource = interactionSource,
                indication = ripple(bounded = false),
                enabled = isReachable && !isCurrent,
                role = Role.Button,
                onClick = onTap,
            ),
        contentAlignment = Alignment.Center,
    ) {
        // The halo is the only thing that makes the current step read as "lifted" — this theme has no
        // usable elevation, so depth has to come from tint.
        if (isCurrent) {
            Box(
                modifier = Modifier
                    .size(TARGET)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.22f)),
            )
        }
        Box(
            modifier = Modifier
                .size(DISC)
                .clip(CircleShape)
                .background(fill)
                .then(
                    if (isCurrent) Modifier else Modifier.border(1.5.dp, ring, CircleShape),
                ),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = if (isDone) Icons.Outlined.Check else icon,
                contentDescription = "$label, $stateDescription",
                tint = glyph,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}

/**
 * Named, not numbered. Each glyph pairs with an SF Symbol of the same shape on iOS, so the two
 * platforms read identically.
 */
private fun iconFor(section: ProfileSection): ImageVector = when (section) {
    ProfileSection.Personal -> Icons.Outlined.Person
    ProfileSection.Address -> Icons.Outlined.Place
    ProfileSection.Identification -> Icons.Outlined.Badge
    ProfileSection.Bank -> Icons.Outlined.AccountBalance
}

private fun labelResFor(section: ProfileSection): Int = when (section) {
    ProfileSection.Personal -> R.string.onboarding_step_personal
    ProfileSection.Address -> R.string.onboarding_step_address
    ProfileSection.Identification -> R.string.onboarding_step_identification
    ProfileSection.Bank -> R.string.onboarding_step_bank
}
