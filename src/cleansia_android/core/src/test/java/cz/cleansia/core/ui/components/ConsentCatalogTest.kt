package cz.cleansia.core.ui.components

import cz.cleansia.core.config.CleansiaWeb
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

/**
 * The legal sentences — the two consent ticks and the booking wizard's contract line —
 * are the only strings in either app whose *markup* is load-bearing, and every way of breaking
 * that markup is silent:
 *
 *  - drop the `<![CDATA[…]]>` wrapper and AAPT compiles the `<a>` into a style
 *    span that `stringResource()` throws away — correct-looking copy, zero
 *    tappable links, no build or lint failure (Android CI runs no lint);
 *  - drop a locale row and that locale falls back to English, which also
 *    compiles and ships;
 *  - spell the domain out and a `.cz` → `.eu` move needs ten re-translations
 *    instead of one edit to [CleansiaWeb].
 *
 * So pin all three, in every locale of both apps. Mirrors
 * `ConsentCatalogTests.swift`, which does the same job for the iOS catalogs
 * from :core's test target for the same reason: the invariant belongs to the
 * shared component, not to either app.
 */
class ConsentCatalogTest {
    private data class LegalSentence(val app: String, val key: String, val links: List<ConsentLink>)

    private companion object {
        val CONSENT_KEYS = listOf(
            LegalSentence("customer-app", "register_terms_and_conditions", listOf(ConsentLink.TERMS, ConsentLink.PRIVACY)),
            LegalSentence("partner-app", "accept_terms", listOf(ConsentLink.TERMS, ConsentLink.PRIVACY)),
            LegalSentence("customer-app", "booking_contract_notice", listOf(ConsentLink.TERMS)),
        )

        // The default `values` directory is the `en` source of truth.
        val LOCALE_DIRS = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

        val CDATA = Regex("""<!\[CDATA\[(.*?)]]>""", RegexOption.DOT_MATCHES_ALL)
    }

    /**
     * Unit tests run with the module directory as their working directory;
     * walk up to the Gradle root rather than hard-coding `..`, so the test
     * survives the module being moved.
     */
    private fun androidRoot(): File {
        var dir: File? = File("").absoluteFile
        while (dir != null && !File(dir, "settings.gradle.kts").isFile) {
            dir = dir.parentFile
        }
        assertNotNull("could not locate the cleansia_android Gradle root", dir)
        return dir!!
    }

    /** The raw `<string>` body, markup and CDATA wrapper included. */
    private fun rawValue(app: String, localeDir: String, key: String): String {
        val file = File(androidRoot(), "$app/src/main/res/$localeDir/strings.xml")
        assertTrue("$file does not exist", file.isFile)
        val match = Regex("""<string name="$key">(.*?)</string>""", RegexOption.DOT_MATCHES_ALL)
            .find(file.readText())
        assertNotNull("$app/$localeDir is missing the `$key` row", match)
        return match!!.groupValues[1]
    }

    /** What `stringResource()` will actually hand the component. */
    private fun resolvedValue(raw: String): String =
        CDATA.replace(raw) { it.groupValues[1] }
            .replace("&lt;", "<")
            .replace("&gt;", ">")
            .replace("&quot;", "\"")
            .replace("&amp;", "&")

    @Test
    fun `every legal sentence links the pages it names in every locale`() {
        for ((app, key, links) in CONSENT_KEYS) {
            for (localeDir in LOCALE_DIRS) {
                val value = resolvedValue(rawValue(app, localeDir, key))
                for (link in links) {
                    assertTrue(
                        "$app/$localeDir `$key` is missing the ${link.placeholder} link: $value",
                        value.contains("""<a href="${link.placeholder}">"""),
                    )
                }
            }
        }
    }

    @Test
    fun `every link target is named by at least one pinned sentence`() {
        val pinned = CONSENT_KEYS.flatMap { it.links }.toSet()
        for (link in ConsentLink.entries) {
            assertTrue("${link.placeholder} is a target no pinned sentence carries", link in pinned)
        }
    }

    @Test
    fun `consent markup is CDATA-wrapped so AAPT cannot strip it`() {
        for ((app, key) in CONSENT_KEYS) {
            for (localeDir in LOCALE_DIRS) {
                val raw = rawValue(app, localeDir, key)
                val outsideCdata = CDATA.replace(raw, "")
                assertFalse(
                    "$app/$localeDir `$key` has <a> markup outside a CDATA section — AAPT will " +
                        "compile it to a span and stringResource() will drop it, leaving the " +
                        "sentence with no tappable links and nothing failing: $raw",
                    outsideCdata.contains("<a "),
                )
            }
        }
    }

    @Test
    fun `consent sentences carry no literal domain`() {
        for ((app, key) in CONSENT_KEYS) {
            for (localeDir in LOCALE_DIRS) {
                val value = resolvedValue(rawValue(app, localeDir, key))
                assertFalse(
                    "$app/$localeDir `$key` spells the domain out — translators carry " +
                        "cleansia:// placeholders only: $value",
                    value.contains(CleansiaWeb.DOMAIN),
                )
            }
        }
    }

    @Test
    fun `the toggle content description exists in every core locale`() {
        for (localeDir in LOCALE_DIRS) {
            val file = File(androidRoot(), "core/src/main/res/$localeDir/strings.xml")
            assertTrue("$file does not exist", file.isFile)
            assertTrue(
                "core/$localeDir is missing `core_consent_toggle` — it would render English",
                file.readText().contains("""<string name="core_consent_toggle">"""),
            )
        }
    }

    /**
     * Each tick names its own consent to TalkBack. The description used to default to the terms, so the
     * card-saving and early-performance ticks were announced as the terms. A new call fails here until
     * it is pinned.
     */
    @Test
    fun `every consent tick is described as its own consent`() {
        val expected = listOf(
            "customer-app CardSavingConsent.kt booking_save_card_label booking_save_card_label",
            "customer-app ConfirmStep.kt consent_early_performance_draft_2026_09_29 booking_early_performance_toggle",
            "customer-app ConfirmStep.kt register_terms_and_conditions core_consent_toggle",
            "customer-app CreateRecurringScreen.kt consent_early_performance_draft_2026_09_29 booking_early_performance_toggle",
            "customer-app CreateRecurringScreen.kt register_terms_and_conditions core_consent_toggle",
            "customer-app OrderDetailScreen.kt register_terms_and_conditions core_consent_toggle",
            "customer-app SignUpScreen.kt register_terms_and_conditions core_consent_toggle",
            "partner-app RegisterScreen.kt accept_terms core_consent_toggle",
        )
        val html = Regex("""\bhtml\s*=\s*stringResource\((?:[\w.]+\.)?R\.string\.(\w+)\)""")
        val described = Regex("""\btoggleContentDescription\s*=\s*stringResource\((?:[\w.]+\.)?R\.string\.(\w+)\)""")
        val actual = listOf("customer-app", "partner-app").flatMap { app ->
            File(androidRoot(), "$app/src/main/java").walkTopDown().filter { it.extension == "kt" }.flatMap { file ->
                val source = file.readText()
                Regex("""\bCleansiaConsentCheckbox\(""").findAll(source).map { call ->
                    val args = argumentsAt(source, call.range.last)
                    "$app ${file.name} ${html.find(args)?.groupValues?.get(1)} ${described.find(args)?.groupValues?.get(1)}"
                }
            }.toList()
        }.sorted()
        assertTrue("consent ticks and their descriptions drifted:\n${actual.joinToString("\n")}", actual == expected)

        for (key in setOf("booking_save_card_label", "booking_early_performance_toggle")) {
            for (localeDir in LOCALE_DIRS) rawValue("customer-app", localeDir, key)
        }
    }

    /** The argument list opening at [open], parenthesis-matched so a sibling call cannot leak in. */
    private fun argumentsAt(source: String, open: Int): String {
        var depth = 0
        for (index in open until source.length) {
            when (source[index]) {
                '(' -> depth++
                ')' -> if (--depth == 0) return source.substring(open, index + 1)
            }
        }
        error("unbalanced parentheses after index $open")
    }
}
