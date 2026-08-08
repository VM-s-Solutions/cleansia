package cz.cleansia.customer.features.auth

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Both auth screens reach one Google flow, and only the ViewModel entry point they call decides
 * whether that flow may provision an account. Every ViewModel test in this package is green with
 * the signup route wired to `signInWithGoogle` — which is exactly how it was wired — and green
 * again with the button handing over a literal instead of the checkbox. Neither is visible from
 * inside the ViewModel, and a Compose call site has no test harness in this module, so the two
 * argument lists are pinned here as source text, each scoped to its own call.
 */
class SignUpSocialWiringTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("customer-app module not found from working dir ${File(".").absolutePath}")

    private fun source(path: String): String {
        val file = File(moduleDir, "src/main/java/cz/cleansia/customer/$path")
        assertTrue("${file.absolutePath} not found", file.isFile)
        return file.readText()
    }

    private val navHost: String by lazy { source("navigation/CleansiaNavHost.kt") }

    private val signUpScreen: String by lazy { source("features/auth/SignUpScreen.kt") }

    /** The argument list of `call(` … `)`, brace/paren matched so a sibling route can't leak in. */
    private fun argumentsOf(source: String, call: String): String {
        val start = source.indexOf(call)
        assertTrue("`$call` no longer appears — this parser is stale", start >= 0)
        var depth = 0
        val open = source.indexOf('(', start)
        for (index in open until source.length) {
            when (source[index]) {
                '(' -> depth++
                ')' -> {
                    depth--
                    if (depth == 0) return source.substring(open, index + 1)
                }
            }
        }
        error("unbalanced parentheses after `$call`")
    }

    private val signUpRoute: String by lazy { argumentsOf(navHost, "SignUpScreen(") }

    private val signInRoute: String by lazy { argumentsOf(navHost, "SignInScreen(") }

    @Test
    fun `the signup route reaches the consent-asserting entry point`() {
        assertTrue(
            "the signup route no longer calls signUpWithGoogle: $signUpRoute",
            signUpRoute.contains("signUpWithGoogle"),
        )
        assertTrue(
            "the signup route reaches the sign-in entry point, which asserts no consent: $signUpRoute",
            !signUpRoute.contains("signInWithGoogle"),
        )
    }

    @Test
    fun `the sign-in route reaches only the entry point that asserts nothing`() {
        assertTrue(
            "the sign-in route no longer calls signInWithGoogle: $signInRoute",
            signInRoute.contains("signInWithGoogle"),
        )
        assertTrue(
            "the sign-in route can provision an account: $signInRoute",
            !signInRoute.contains("signUpWithGoogle"),
        )
    }

    @Test
    fun `the google button hands over the checkbox state, not a literal`() {
        val checkbox = argumentsOf(signUpScreen, "CleansiaConsentCheckbox(")
        val tick = Regex("checked\\s*=\\s*(\\w+)").find(checkbox)?.groupValues?.get(1)
        assertTrue("no `checked =` on the consent checkbox: $checkbox", tick != null)

        val button = argumentsOf(signUpScreen, "CleansiaOutlinedButton(")
        assertTrue(
            "the Google button passes something other than the checkbox state `$tick`: $button",
            button.contains("onGoogleSignIn($tick)"),
        )
    }
}
