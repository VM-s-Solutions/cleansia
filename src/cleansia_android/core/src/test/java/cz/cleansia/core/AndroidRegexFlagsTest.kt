package cz.cleansia.core

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Android compiles every regex with ICU, which refuses two flags the JVM accepts: the Unicode character
 * class flag (inline it throws PatternSyntaxException, as a constant IllegalArgumentException) and
 * CANON_EQ (IllegalArgumentException). These unit tests run on the JVM, so only a device shows it, and a
 * regex held in a static fails its whole class.
 */
class AndroidRegexFlagsTest {

    @Test
    fun `no source compiles a regex with a flag Android refuses`() {
        val offenders = sources.flatMap { (where, text) ->
            text.lineSequence().mapIndexedNotNull { index, line ->
                "$where:${index + 1}".takeIf { REFUSED.containsMatchIn(line) }
            }.toList()
        }

        assertEquals(emptyList<String>(), offenders)
    }

    @Test
    fun `the sources of every module are still found`() {
        MODULES.forEach { module ->
            assertTrue(module, sources.count { it.first.startsWith("$module/") } > 50)
        }
    }

    private val androidRoot: File = sequenceOf(File(".."), File("."), File("src/cleansia_android"))
        .map { it.absoluteFile.normalize() }
        .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
        ?: error("cleansia_android not found from ${File(".").absolutePath}")

    private val sources: List<Pair<String, String>> by lazy {
        MODULES.flatMap { module ->
            val root = File(androidRoot, "$module/src/main/java")
            root.walkTopDown().filter { it.isFile && it.extension == "kt" }
                .map { "$module/${it.relativeTo(root).invariantSeparatorsPath}" to it.readText().replace("\r\n", "\n") }
                .toList()
        }
    }

    private companion object {
        val MODULES = listOf("core", "customer-app", "partner-app")
        val REFUSED = Regex("""\(\?[a-zA-Z-]*U|UNICODE_CHARACTER_CLASS|CANON_EQ""")
    }
}
