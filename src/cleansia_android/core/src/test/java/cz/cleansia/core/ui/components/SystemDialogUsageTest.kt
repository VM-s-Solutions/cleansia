package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every confirm in both apps is the system's Material 3 AlertDialog, as every confirm on iOS is the
 * system alert. The branded CleansiaDialog drew its own window, looked like nothing else on the phone
 * and stayed up, greyed, while its request ran; it is deleted. A screen that draws a dialog window of
 * its own again fails here rather than on a device.
 */
class SystemDialogUsageTest {

    private val androidRoot: File = sequenceOf(File(".."), File("."), File("src/cleansia_android"))
        .map { it.absoluteFile.normalize() }
        .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
        ?: error("cleansia_android not found from ${File(".").absolutePath}")

    private val sources: List<File> by lazy {
        listOf("core", "customer-app", "partner-app").flatMap { module ->
            File(androidRoot, "$module/src/main/java").walkTopDown()
                .filter { it.isFile && it.extension == "kt" }
                .toList()
        }
    }

    @Test
    fun `the sources are still found`() {
        assertTrue("no Kotlin sources under $androidRoot", sources.size > 100)
    }

    @Test
    fun `no screen draws a dialog window of its own`() {
        val custom = sources
            .filter { it.readText().contains(Regex("""import androidx\.compose\.ui\.window\.Dialog\b""")) }
            .map { it.name }
        assertEquals(emptyList<String>(), custom)
    }
}
