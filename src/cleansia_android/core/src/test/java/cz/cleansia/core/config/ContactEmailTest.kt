package cz.cleansia.core.config

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Both apps mail one support address, held once in [CleansiaWeb] as iOS holds `CleansiaWeb.contactEmail`. A
 * screen that keeps its own copy is one more place a domain move has to find.
 */
class ContactEmailTest {

    private val androidRoot: File = sequenceOf(File(".."), File("."), File("src/cleansia_android"))
        .map { it.absoluteFile.normalize() }
        .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
        ?: error("cleansia_android not found from ${File(".").absolutePath}")

    private val sources: List<File> by lazy {
        listOf("core", "customer-app", "partner-app").flatMap { module ->
            File(androidRoot, "$module/src/main").walkTopDown()
                .filter { it.isFile && it.extension in setOf("kt", "xml") }
                .toList()
        }
    }

    private fun File.relativePath(): String = relativeTo(androidRoot).invariantSeparatorsPath

    @Test
    fun `the support address is support at the platform's domain`() {
        assertEquals("support@${CleansiaWeb.DOMAIN}", CleansiaWeb.CONTACT_EMAIL)
    }

    @Test
    fun `no source but CleansiaWeb spells the support address out`() {
        val names = sources.map { it.name }.toSet()
        listOf("HelpSupportScreen.kt", "DashboardScreen.kt", "RegistrationLockScreen.kt").forEach { screen ->
            assertTrue("the walk must reach $screen, which mails support", screen in names)
        }
        val copies = sources
            .filter { it.relativePath() != DEFINITION && it.readText().contains("support@") }
            .map { it.relativePath() }
        assertEquals(emptyList<String>(), copies)
    }

    private companion object {
        const val DEFINITION = "core/src/main/java/cz/cleansia/core/config/CleansiaWeb.kt"
    }
}
