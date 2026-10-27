package cz.cleansia.partner.data.auth

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * `RegistrationPasswordTest` proves a wipe forgets the held password; this proves the app's session wipe set
 * holds it. A Hilt multibinding has no seam in a unit test, so the module's source is read — evidence Gradle
 * does not track as an input, so this file only means something under `testDebugUnitTest --rerun-tasks`.
 */
class RegistrationPasswordWipeTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).firstOrNull { File(it, "src/main/java/cz/cleansia/partner").isDirectory }
        ?: error("partner-app module not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every session wipe forgets the password held for the confirmation`() {
        val module = File(moduleDir, "src/main/java/cz/cleansia/partner/data/auth/AuthModule.kt").readText()

        assertTrue(
            "RegistrationPassword must be bound into the session wipe set",
            Regex("""@Binds\s+@IntoSet\s+abstract fun \w+\(impl: RegistrationPassword\): SessionScopedCache""")
                .containsMatchIn(module),
        )
    }
}
