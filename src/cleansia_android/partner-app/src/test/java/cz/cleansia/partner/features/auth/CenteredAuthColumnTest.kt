package cz.cleansia.partner.features.auth

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The four partner auth forms centre when they fit and scroll when they do not. There is no Compose
 * harness in this module, so the shape is pinned as source: the column is at least as tall as its
 * viewport and centres inside it, and every form is built on it rather than on a top-anchored scroll.
 */
class CenteredAuthColumnTest {

    private val authDir: File = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/auth") }
        .first { it.isDirectory }

    private fun source(name: String) = File(authDir, name).readText()

    @Test
    fun `the column is at least as tall as its viewport and centres its content`() {
        val flat = source("CenteredAuthColumn.kt").replace(Regex("\\s+"), " ")
        assertTrue("the column no longer scrolls", flat.contains(".verticalScroll(rememberScrollState())"))
        assertTrue("the column no longer fills the viewport before centring", flat.contains(".heightIn(min = maxHeight)"))
        assertTrue("the column no longer centres", flat.contains("verticalArrangement = Arrangement.Center"))
    }

    @Test
    fun `every auth form is centred, none is top-anchored`() {
        listOf("LoginScreen.kt", "RegisterScreen.kt", "ForgotPasswordScreen.kt", "ConfirmEmailScreen.kt").forEach { name ->
            val text = source(name)
            assertEquals("$name does not build its form on CenteredAuthColumn", 1, Regex("CenteredAuthColumn\\(").findAll(text).count())
            assertTrue("$name scrolls a top-anchored column of its own", !text.contains("verticalScroll("))
            assertTrue("$name still pads its top by a fixed 64dp", !text.contains("top = 64.dp"))
        }
    }
}
