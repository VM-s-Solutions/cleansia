package cz.cleansia.customer.features.auth

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The password manager offers a strong password, and fills a saved one back, only on a field that
 * declares what it holds; nothing else on the screen tells it. There is no Compose harness for these
 * screens, so the declarations are pinned as source.
 */
class AutofillHintsCallSiteTest {

    @Test
    fun `Sign up declares the new account's username and both new-password fields`() {
        val fields = textFields("auth/SignUpScreen.kt")

        assertEquals(listOf("NewUsername"), fields.filter { "KeyboardType.Email" in it }.map(::hint))
        assertEquals(listOf("NewPassword", "NewPassword"), passwordHints(fields))
    }

    @Test
    fun `Sign in declares the username and the password it fills back`() {
        val fields = textFields("auth/SignInScreen.kt")

        assertEquals(listOf("Username"), fields.filter { "KeyboardType.Email" in it }.map(::hint))
        assertEquals(listOf("Password"), passwordHints(fields))
    }

    @Test
    fun `the reset step and Security declare both of their new-password fields`() {
        assertEquals(listOf("NewPassword", "NewPassword"), passwordHints(textFields("auth/ForgotPasswordScreen.kt")))
        assertEquals(listOf("NewPassword", "NewPassword"), passwordHints(textFields("profile/SecurityScreen.kt")))
    }

    private fun passwordHints(fields: List<String>) = fields.filter { "isPassword = true" in it }.map(::hint)

    private fun hint(field: String): String? =
        Regex("""contentType = ContentType\.(\w+)""").find(field)?.groupValues?.get(1)

    private fun textFields(screen: String): List<String> {
        val source = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer/features/$screen") }
            .first { it.isFile }
            .readText()
        return Regex("""CleansiaTextField\(""").findAll(source).map { call ->
            var depth = 0
            val close = (call.range.last until source.length).first { i ->
                when (source[i]) {
                    '(' -> depth++
                    ')' -> depth--
                }
                depth == 0
            }
            source.substring(call.range.first, close + 1)
        }.toList()
    }
}
