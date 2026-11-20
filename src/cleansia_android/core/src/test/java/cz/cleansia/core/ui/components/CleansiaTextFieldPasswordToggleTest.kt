package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** TalkBack reads the password field's show/hide button in the user's language, as the web's eye button does. */
class CleansiaTextFieldPasswordToggleTest {

    @Test
    fun `the toggle is described by core strings`() {
        val source = File(coreDir, "src/main/java/cz/cleansia/core/ui/components/CleansiaTextField.kt").readText()
        assertFalse("the toggle carries an English literal", source.contains("\"Show password\"") || source.contains("\"Hide password\""))
        assertTrue(source.contains("R.string.core_password_show"))
        assertTrue(source.contains("R.string.core_password_hide"))
    }

    @Test
    fun `the toggle's descriptions exist in every core locale`() {
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { localeDir ->
            val strings = File(coreDir, "src/main/res/$localeDir/strings.xml").readText()
            listOf("core_password_show", "core_password_hide").forEach { key ->
                assertTrue("core/$localeDir is missing `$key`", strings.contains("""<string name="$key">"""))
            }
        }
    }

    private val coreDir: File = sequenceOf(File("."), File("core"), File("src/cleansia_android/core"))
        .firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("core module not found from working dir ${File(".").absolutePath}")
}
