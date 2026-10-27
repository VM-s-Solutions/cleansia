package cz.cleansia.customer.features.auth

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [EmailVerifyFormTest] proves the gate; this proves the screen and the route use it. Neither has a test
 * seam, so the source is read. Evidence here is Kotlin source read off disk, which Gradle does not track as
 * an input — this file only means something under `testDebugUnitTest --rerun-tasks`.
 */
class EmailVerifyCallSiteTest {

    private val moduleDir: File = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .first { File(it, "src/main/java/cz/cleansia/customer").isDirectory }

    private fun source(path: String) = File(moduleDir, "src/main/java/cz/cleansia/customer/$path").readText()

    @Test
    fun `the code submits on its own only when no password is asked, and Verify waits for the form`() {
        val screen = source("features/auth/EmailVerifyScreen.kt")
        assertTrue(
            "the code alone must not submit a form that asks for a password",
            screen.contains("if (code.length == CODE_LENGTH && !asksForPassword) onVerify(code, null)"),
        )
        assertTrue(
            "the Verify button must follow the form's gate",
            screen.contains("enabled = EmailVerifyForm.isSubmittable(code, asksForPassword, password, confirmPassword)"),
        )
    }

    @Test
    fun `the route asks the view model whether to ask for a password and hands it the one chosen`() {
        val host = source("navigation/CleansiaNavHost.kt")
        assertTrue("the route must ask the view model", host.contains("vm.asksForPassword(args.email)"))
        assertTrue(
            "the route must hand the chosen password to the confirmation",
            host.contains("onVerify = { code, password -> vm.confirmEmail(args.email, code, password) }"),
        )
    }
}
