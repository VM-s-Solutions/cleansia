package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The onboarding stepper names every step: the current one emphasised, a finished one checked and
 * still named, the rest muted, and TalkBack reads each as "Step n of 4, <name>, <state>". The state
 * rule is plain code; the drawing has no Compose harness here, so it is pinned as source.
 */
class OnboardingChainHeaderTest {

    @Test
    fun `the current step wins over done, and a step neither current nor done is upcoming`() {
        assertEquals(StepNodeState.Current, stepNodeState(isCurrent = true, isDone = true))
        assertEquals(StepNodeState.Current, stepNodeState(isCurrent = true, isDone = false))
        assertEquals(StepNodeState.Done, stepNodeState(isCurrent = false, isDone = true))
        assertEquals(StepNodeState.Upcoming, stepNodeState(isCurrent = false, isDone = false))
    }

    private val header: String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/profile/OnboardingChainHeader.kt") }
        .first { it.isFile }
        .readText()

    @Test
    fun `every step is drawn with its name, not only the current one`() {
        assertTrue("a capsule for the current step is back", !header.contains("StepPill("))
        val node = header.substringAfter("private fun StepNode(").substringBefore("private fun iconFor(")
        assertTrue("a node no longer draws its name", node.contains("text = label"))
        assertTrue("a finished step no longer shows its check", node.contains("if (state == StepNodeState.Done) Icons.Outlined.Check else icon"))
        assertTrue("a name can be cut off", !node.contains("TextOverflow.Ellipsis"))
    }

    @Test
    fun `TalkBack reads one element per step with its position, name and state`() {
        val node = header.substringAfter("private fun StepNode(").substringBefore("private fun iconFor(")
        assertTrue(
            "the step is no longer one merged element with its position, name and state",
            node.contains("semantics(mergeDescendants = true) { contentDescription = \"\$position, \$label, \$stateDescription\" }"),
        )
        assertTrue("the position is no longer \"Step n of 4\"", header.contains("position = stringResource(R.string.onboarding_step_progress, index + 1, sections.size)"))
    }

    @Test
    fun `a step the cleaner may go back to stays tappable`() {
        assertTrue(header.contains("isReachable = nodeState != StepNodeState.Current && isReachable(index)"))
        assertTrue(header.contains(".clickable(enabled = isReachable, role = Role.Button, onClick = onTap)"))
    }
}
