package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [CleansiaBankAccountInputPasteTest] proves the rule and what [lastReported] answers, and cannot see when a segment
 * builds one. This pins that wiring, scoped to the one file because the component has no test harness.
 */
class CleansiaBankAccountInputBindingTest {

    private val source: String = sequenceOf(File("."), File("core"), File("src/cleansia_android/core"))
        .map { File(it, "src/main/java/cz/cleansia/core/ui/components/CleansiaBankAccountInput.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("CleansiaBankAccountInput.kt not found from ${File(".").absolutePath}")

    /**
     * BasicTextField shows the composed value again at the composition after an edit the caller kept out. A reporter
     * kept across that measures the next edit against text the box no longer shows.
     */
    @Test
    fun `a segment builds its reporter again at the composition after every edit`() {
        assertTrue(
            "the count must be state, so an edit that leaves the value as it was still recomposes the segment",
            source.contains("var edits by remember { mutableIntStateOf(0) }"),
        )
        assertTrue(
            "the reporter must start from the composed value whenever the value or the count changes",
            source.contains("val replaced = remember(value, edits) { lastReported(value) }"),
        )
        assertTrue(
            "every edit must count, the ones that leave the value as it was among them",
            Regex("""onValueChange\(replaced\(raw\), raw\)\s*edits\+\+""").containsMatchIn(source),
        )
    }
}
