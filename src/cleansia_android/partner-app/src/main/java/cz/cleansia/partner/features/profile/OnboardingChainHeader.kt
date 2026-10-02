package cz.cleansia.partner.features.profile

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
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
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.OnboardingChainState
import cz.cleansia.partner.features.orders.ProfileSection

private val NODE = 32.dp
private val NODE_CURRENT = 36.dp
private val NODE_ROW = 36.dp

/** What a step node draws: the current step is emphasised, a finished one checked, the rest muted. */
internal enum class StepNodeState { Current, Done, Upcoming }

internal fun stepNodeState(isCurrent: Boolean, isDone: Boolean): StepNodeState = when {
    isCurrent -> StepNodeState.Current
    isDone -> StepNodeState.Done
    else -> StepNodeState.Upcoming
}

/**
 * The onboarding stepper.
 *
 * **Every step is named.** Four equal columns, each a node over its short name, so a cleaner can tell
 * at a glance what each step is and where they are: the current step is a filled `primary` node with
 * its icon and a bold `primary` name; a finished step keeps its name and swaps its icon for a check on
 * `primaryContainer`; a step not yet done is an outlined node with a muted name. The previous design
 * named only the current step, in a capsule, and drew the other three as unlabelled discs — three
 * identical check circles said nothing about which steps they were.
 *
 * Three channels still carry state, so no single failure of colour perception loses the picture:
 * **size and fill** say where you are (a larger filled node), **the check** says a step is finished,
 * and **the ring** says whether you may go there — `primary` on a step you can jump to, `outline` on
 * one you cannot. A reachable step stays tappable across its whole column, node and name (T-0607).
 *
 * **It fits because the columns share the width.** At 320dp the card gives 256dp of content, 64dp a
 * step; the longest name in the five shipped locales is eight characters (`Особисте`, `Identity`,
 * `Личность`), about 56dp of labelMedium. A larger font wraps a name onto a second line rather than
 * truncating it. The connector runs centre to centre behind the nodes, `primary` behind a finished step.
 *
 * TalkBack reads each column as one element: "Step 2 of 4, Address, current step".
 *
 * Built to the same numbers as the iOS twin: 32 node, 36 current node, 2 connector, 6 under the node.
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

    // The segment behind a finished step is the progress indicator. Two tones of primary, never
    // outlineVariant — slate700 on this card measures 1.51:1.
    val connectorDone = MaterialTheme.colorScheme.primary
    val connectorTodo = MaterialTheme.colorScheme.primary.copy(alpha = 0.24f)

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

            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
                sections.forEachIndexed { index, section ->
                    val nodeState = stepNodeState(isCurrent = section == currentSection, isDone = isDone(section))
                    StepNode(
                        icon = iconFor(section),
                        label = stringResource(labelResFor(section)),
                        position = stringResource(R.string.onboarding_step_progress, index + 1, sections.size),
                        state = nodeState,
                        isReachable = nodeState != StepNodeState.Current && isReachable(index),
                        leadingConnector = if (index == 0) null else {
                            if (isDone(sections[index - 1])) connectorDone else connectorTodo
                        },
                        trailingConnector = if (index == sections.lastIndex) null else {
                            if (isDone(section)) connectorDone else connectorTodo
                        },
                        onTap = { onSelect(section) },
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
    }
}

/**
 * One step: its node over its name, and the half of each neighbouring connector that falls in its
 * column, so adjacent halves meet between two nodes. The node's own fill hides the line behind it.
 */
@Composable
private fun StepNode(
    icon: ImageVector,
    label: String,
    position: String,
    state: StepNodeState,
    isReachable: Boolean,
    leadingConnector: Color?,
    trailingConnector: Color?,
    onTap: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val colors = MaterialTheme.colorScheme
    val (fill, glyph) = when (state) {
        StepNodeState.Current -> colors.primary to colors.onPrimary
        StepNodeState.Done -> colors.primaryContainer to colors.onPrimaryContainer
        StepNodeState.Upcoming -> colors.surface to colors.onSurfaceVariant
    }
    val ring = if (isReachable) colors.primary else colors.outline
    val stateDescription = stringResource(
        when (state) {
            StepNodeState.Current -> R.string.onboarding_step_state_current
            StepNodeState.Done -> R.string.onboarding_step_state_done
            StepNodeState.Upcoming -> R.string.onboarding_step_state_upcoming
        },
    )

    Column(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .clickable(enabled = isReachable, role = Role.Button, onClick = onTap)
            .semantics(mergeDescendants = true) { contentDescription = "$position, $label, $stateDescription" }
            .padding(bottom = Spacing.XXS),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Box(modifier = Modifier.fillMaxWidth().height(NODE_ROW)) {
            leadingConnector?.let {
                Box(Modifier.align(Alignment.CenterStart).fillMaxWidth(0.5f).height(2.dp).background(it))
            }
            trailingConnector?.let {
                Box(Modifier.align(Alignment.CenterEnd).fillMaxWidth(0.5f).height(2.dp).background(it))
            }
            Box(
                modifier = Modifier
                    .align(Alignment.Center)
                    .size(if (state == StepNodeState.Current) NODE_CURRENT else NODE)
                    .clip(CircleShape)
                    .background(fill)
                    .then(if (state == StepNodeState.Upcoming) Modifier.border(1.5.dp, ring, CircleShape) else Modifier),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = if (state == StepNodeState.Done) Icons.Outlined.Check else icon,
                    contentDescription = null,
                    tint = glyph,
                    modifier = Modifier.size(if (state == StepNodeState.Current) 18.dp else 16.dp),
                )
            }
        }
        Spacer(Modifier.height(6.dp))
        // Two lines at most and never cut: a name too long for its column at a large font wraps.
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium.copy(
                fontWeight = if (state == StepNodeState.Current) FontWeight.ExtraBold else FontWeight.SemiBold,
            ),
            color = when (state) {
                StepNodeState.Current -> colors.primary
                StepNodeState.Done -> colors.onSurface
                StepNodeState.Upcoming -> colors.onSurfaceVariant
            },
            textAlign = TextAlign.Center,
            maxLines = 2,
            modifier = Modifier.padding(horizontal = 2.dp),
        )
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
