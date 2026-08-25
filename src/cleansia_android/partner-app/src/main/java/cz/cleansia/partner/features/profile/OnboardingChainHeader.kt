package cz.cleansia.partner.features.profile

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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.OnboardingChainState
import cz.cleansia.partner.features.orders.ProfileSection

/**
 * Banner shown at the top of every profile-section screen when the
 * cleaner reached it via the registration lock's "Complete profile" CTA.
 * Tells them:
 *  - they're in a multi-step flow (Step N of 4),
 *  - which section they're on right now (highlighted dot),
 *  - which sections are already done (checkmark dots) vs. still pending
 *    (numbered dots).
 *
 * Hidden when [state] is loading or when this section isn't part of an
 * active onboarding chain — both cases are handled by the caller (the
 * header is only rendered when `route.onboarding == true`).
 */
@Composable
fun OnboardingChainHeader(
    currentSection: ProfileSection,
    state: OnboardingChainState,
    onSelect: (ProfileSection) -> Unit,
) {
    val sections = ProfileSection.values().toList()
    val currentIndex = sections.indexOf(currentSection)
    val stepNumber = currentIndex + 1

    Surface(
        modifier = Modifier.fillMaxWidth(),
        // 24, matching iOS's CornerRadius.large token. The two drew this card at different radii.
        shape = RoundedCornerShape(24.dp),
        color = MaterialTheme.colorScheme.primary.copy(alpha = 0.08f),
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    text = stringResource(
                        R.string.onboarding_step_progress,
                        stepNumber,
                        state.totalSteps,
                    ),
                    // The token is already Bold (Type.kt) — overriding it to SemiBold here made this
                    // line lighter than the same slot everywhere else, and lighter than iOS.
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary,
                )
                Text(
                    text = stringResource(R.string.onboarding_header_subtitle),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(Spacing.XS))
            LinearProgressIndicator(
                progress = {
                    if (state.totalSteps == 0) 0f
                    else state.completedSteps.toFloat() / state.totalSteps
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(6.dp)
                    .clip(RoundedCornerShape(50)),
                color = MaterialTheme.colorScheme.primary,
                trackColor = MaterialTheme.colorScheme.surfaceVariant,
                drawStopIndicator = {},
            )

            Spacer(Modifier.height(Spacing.M))

            // Dot row — each section is a circle with the step number or
            // a checkmark when complete. The current section gets a
            // filled primary fill so the cleaner sees where they are.
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                sections.forEachIndexed { index, section ->
                    val isDone = state.completionByCategory[section] == true
                    val isCurrent = section == currentSection
                    SectionDot(
                        index = index + 1,
                        labelRes = labelResFor(section),
                        isDone = isDone,
                        isCurrent = isCurrent,
                        // Reachable = already finished, or already walked past. Not "any step":
                        // jumping forward into a section the chain has not filled yet leaves a gap
                        // the chain then has to re-find.
                        isReachable = isDone || index < currentIndex,
                        onTap = { onSelect(section) },
                    )
                }
            }
        }
    }
}

@Composable
private fun SectionDot(
    index: Int,
    labelRes: Int,
    isDone: Boolean,
    isCurrent: Boolean,
    isReachable: Boolean,
    onTap: () -> Unit,
) {
    val dotColor = when {
        isCurrent -> MaterialTheme.colorScheme.primary
        isDone -> MaterialTheme.colorScheme.primary.copy(alpha = 0.6f)
        else -> MaterialTheme.colorScheme.surfaceVariant
    }
    val labelColor = when {
        isCurrent -> MaterialTheme.colorScheme.primary
        isDone -> MaterialTheme.colorScheme.onSurfaceVariant
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }
    Column(
        // The click goes on the whole dot+label column, not the 32dp circle: 32dp is under the 48dp
        // minimum touch target, and the label is the part people aim at.
        modifier = Modifier
            .clickable(enabled = isReachable, role = Role.Button, onClick = onTap)
            .padding(horizontal = Spacing.XXS),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Box(
            modifier = Modifier
                .size(32.dp)
                .clip(CircleShape)
                .background(dotColor),
            contentAlignment = Alignment.Center,
        ) {
            if (isDone) {
                Icon(
                    imageVector = Icons.Outlined.Check,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onPrimary,
                    modifier = Modifier.size(18.dp),
                )
            } else {
                Text(
                    text = index.toString(),
                    style = MaterialTheme.typography.labelLarge,
                    color = if (isCurrent) MaterialTheme.colorScheme.onPrimary
                    else MaterialTheme.colorScheme.onSurface,
                )
            }
        }
        Spacer(Modifier.height(4.dp))
        Text(
            text = stringResource(labelRes),
            style = MaterialTheme.typography.labelSmall,
            color = labelColor,
        )
    }
}

private fun labelResFor(section: ProfileSection): Int = when (section) {
    ProfileSection.Personal -> R.string.onboarding_step_personal
    ProfileSection.Address -> R.string.onboarding_step_address
    ProfileSection.Identification -> R.string.onboarding_step_identification
    ProfileSection.Bank -> R.string.onboarding_step_bank
}
