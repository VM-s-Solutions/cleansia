package cz.cleansia.customer.features.profile

import cz.cleansia.core.ui.components.sanitizePhoneInput
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The customer's two phone fields kept whatever was typed or pasted, and the view model only trims, so a pasted
 * "+420 777 111 222" was saved with its spaces where the iOS customer app sends "+420777111222". Edit profile runs
 * every edit through the shared phone sanitiser, and the onboarding draws the shared phone field, which does the same
 * to every edit itself. Only edits: a number already stored is shown as it is.
 */
class ProfilePhoneSanitisingTest {

    private val featureDir: File = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/java/cz/cleansia/customer/features/profile") }
        .firstOrNull { it.isDirectory }
        ?: error("customer profile feature dir not found from ${File(".").absolutePath}")

    private fun source(name: String): String = File(featureDir, name).also { assertTrue("$name not found", it.isFile) }.readText()

    @Test
    fun `a pasted formatted number is kept as its plus and digits`() {
        assertEquals("+420777111222", sanitizePhoneInput("+420 777 111 222"))
        assertEquals("+420777111222", sanitizePhoneInput("+420-777-111-222"))
    }

    @Test
    fun `both phone fields sanitise every edit and seed with the stored number as it is`() {
        assertTrue(
            "EditProfileScreen.kt must pass each phone edit through sanitizePhoneInput",
            source("EditProfileScreen.kt").contains("onValueChange = { phone = sanitizePhoneInput(it) }"),
        )
        assertTrue(
            "ProfileOnboardingScreen.kt must take its phone from the shared field, which sanitises every edit",
            Regex("""CleansiaPhoneInput\(\s*value = phone,\s*onValueChange = \{ phone = it \},\s*label = [^\n]+,\s*countryRegion = "CZ",?\s*\)""")
                .containsMatchIn(source("ProfileOnboardingScreen.kt")),
        )
        listOf("EditProfileScreen.kt", "ProfileOnboardingScreen.kt").forEach { name ->
            assertTrue("$name must not rewrite the stored number on load", source(name).contains("mutableStateOf(user?.phoneNumber.orEmpty())"))
        }
    }
}
