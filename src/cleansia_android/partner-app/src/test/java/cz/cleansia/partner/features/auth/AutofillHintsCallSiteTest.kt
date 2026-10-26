package cz.cleansia.partner.features.auth

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The password manager offers a strong password, and fills a saved one back, only on a field that
 * declares what it holds; nothing else on the screen tells it. There is no Compose harness in this
 * module, so the declarations are pinned as source.
 */
class AutofillHintsCallSiteTest {

    @Test
    fun `Register declares the new account's username and both new-password fields`() {
        val fields = textFields("RegisterScreen.kt")

        assertEquals(listOf("NewUsername"), fields.filter { "KeyboardType.Email" in it }.map(::hint))
        assertEquals(listOf("NewPassword", "NewPassword"), fields.filter { "isPassword = true" in it }.map(::hint))
    }

    @Test
    fun `Sign in declares the username and the password it fills back`() {
        val fields = textFields("LoginScreen.kt")

        assertEquals(listOf("Username"), fields.filter { "KeyboardType.Email" in it }.map(::hint))
        assertEquals(listOf("Password"), fields.filter { "isPassword = true" in it }.map(::hint))
    }

    private fun hint(field: String): String? =
        Regex("""contentType = ContentType\.(\w+)""").find(field)?.groupValues?.get(1)

    private fun textFields(screen: String): List<String> {
        val source = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
            .map { File(it, "src/main/java/cz/cleansia/partner/features/auth/$screen") }
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
