package cz.cleansia.partner.features.profile

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.BorderStroke
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
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.OnboardingChainState
import cz.cleansia.partner.features.orders.ProfileSection

private val DOT = 30.dp
private val DOT_TARGET = 36.dp
private val PILL_HEIGHT = 40.dp
private val PILL_DISC = 26.dp

/**
 * The onboarding stepper.
 *
 * **The current step is a capsule, not a dot.** It grows out of the rail carrying its own icon and
 * its own name, and every other step shrinks to a compact disc. That is the whole idea: the name
 * belongs to the step it describes instead of floating on a line of its own underneath the rail,
 * where it named nothing in particular. The separate title line this used to end with is gone — the
 * pill holds it now, and the card is about 50dp shorter for it.
 *
 * Three channels carry state, one bit each, so no single failure of colour perception loses the
 * whole picture:
 *
 * - **shape** says where you are — a capsule is the current step, a disc is any other;
 * - **fill** says whether a step is finished — `primaryContainer` behind a checkmark once it is,
 *   nothing behind an icon while it is not;
 * - **ring** says whether you may go there — `primary` on a step you can jump to, `outline` on one
 *   you cannot.
 *
 * **The row fits because the pill is content-sized and the connectors absorb the slack.** At 320dp
 * the card gives 256dp of content: three 36dp dots and a pill of at most 120dp leave about 9dp for
 * each connector. 120 is the real ceiling and not an estimate — the longest step name in any of the
 * five shipped locales is eight characters (`Особисте`, `Identity`, `Identita`, `Личность`), which
 * is 62dp of labelLarge plus 58dp of disc, gaps and insets.
 *
 * **No green, and no elevation.** Reference designs for this pattern are drawn on white; the success
 * token measures 2.92:1 on this app's dark surface and a shadow is invisible against it. Progress is
 * carried by the connector tinting `primary` behind you — which is why the separate progress bar this
 * used to sit above is gone. It said the same thing twice.
 *
 * Built to the same numbers as the iOS twin: 30 dot, 36 target, 40 pill, 26 pill disc, 2 connector.
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
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
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
                    if (section == currentSection) {
                        StepPill(
                            icon = iconFor(section),
                            label = stringResource(labelResFor(section)),
                        )
                    } else {
                        StepDot(
                            icon = iconFor(section),
                            isDone = isDone(section),
                            isReachable = isReachable(index),
                            label = stringResource(labelResFor(section)),
                            onTap = { onSelect(section) },
                        )
                    }
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
        }
    }
}

/**
 * The current step. Content-sized on purpose: it is the one element allowed to claim whatever width
 * its label needs, because the connectors either side give that width up.
 */
@Composable
private fun StepPill(icon: ImageVector, label: String) {
    val stateDescription = stringResource(R.string.onboarding_step_state_current)

    Row(
        modifier = Modifier
            .height(PILL_HEIGHT)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primary)
            .padding(start = Spacing.XS, end = Spacing.M)
            .semantics(mergeDescendants = true) {
                contentDescription = "$label, $stateDescription"
            },
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(PILL_DISC)
                .clip(CircleShape)
                // A wash of the pill's own ink, not a second palette colour — it has to read as an
                // inset in the capsule rather than a separate badge sitting on top of it.
                .background(MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.22f)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimary,
                modifier = Modifier.size(15.dp),
            )
        }
        Spacer(Modifier.width(Spacing.XS))
        // No maxLines and no autosize anywhere in this composable: the pill is sized by its text, so
        // there is nothing for the text to be squeezed into.
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.onPrimary,
        )
    }
}

/**
 * Any step that is not the current one. 30dp of disc inside a 36dp target — smaller than the 48 this
 * used to draw, because the pill has to fit on the same row at 320dp and something had to give.
 */
@Composable
private fun StepDot(
    icon: ImageVector,
    isDone: Boolean,
    isReachable: Boolean,
    label: String,
    onTap: () -> Unit,
) {
    val interactionSource = remember { MutableInteractionSource() }
    val pressed by interactionSource.collectIsPressedAsState()
    val scale by animateFloatAsState(if (pressed) 0.94f else 1f, label = "dotPress")

    val fill = if (isDone) MaterialTheme.colorScheme.primaryContainer else Color.Transparent
    val ring = if (isReachable) {
        MaterialTheme.colorScheme.primary
    } else {
        MaterialTheme.colorScheme.outline
    }
    val glyph = if (isDone) {
        MaterialTheme.colorScheme.onPrimaryContainer
    } else {
        MaterialTheme.colorScheme.onSurfaceVariant
    }
    val stateDescription = stringResource(
        if (isDone) R.string.onboarding_step_state_done else R.string.onboarding_step_state_upcoming,
    )

    Box(
        modifier = Modifier
            .size(DOT_TARGET)
            .scale(scale)
            .clickable(
                interactionSource = interactionSource,
                indication = ripple(bounded = false),
                enabled = isReachable,
                role = Role.Button,
                onClick = onTap,
            ),
        contentAlignment = Alignment.Center,
    ) {
        Box(
            modifier = Modifier
                .size(DOT)
                .clip(CircleShape)
                .background(fill)
                .then(if (isDone) Modifier else Modifier.border(1.5.dp, ring, CircleShape)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = if (isDone) Icons.Outlined.Check else icon,
                contentDescription = "$label, $stateDescription",
                tint = glyph,
                modifier = Modifier.size(16.dp),
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
