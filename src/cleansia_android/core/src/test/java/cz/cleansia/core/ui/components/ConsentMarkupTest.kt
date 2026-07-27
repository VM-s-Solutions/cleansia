package cz.cleansia.core.ui.components

import cz.cleansia.core.config.CleansiaWeb
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Only the placeholder rewrite is covered here — [ConsentMarkup.annotated]
 * delegates to `AnnotatedString.fromHtml`, which needs the Android framework
 * and cannot run in a JVM unit test.
 */
class ConsentMarkupTest {
    @Test
    fun `rewrites both placeholders to the real web pages`() {
        val html = ConsentMarkup.resolveTargets(
            """I agree to the <a href="cleansia://terms">Terms</a> """ +
                """and <a href="cleansia://privacy">Privacy</a>""",
        )

        assertEquals(
            """I agree to the <a href="${CleansiaWeb.TERMS_URL}">Terms</a> """ +
                """and <a href="${CleansiaWeb.PRIVACY_URL}">Privacy</a>""",
            html,
        )
    }

    @Test
    fun `leaves no placeholder scheme behind`() {
        for (link in ConsentLink.entries) {
            val html = ConsentMarkup.resolveTargets("""<a href="${link.placeholder}">x</a>""")
            assertFalse(html, html.contains("cleansia://"))
            assertTrue(html, html.contains(link.url))
        }
    }

    @Test
    fun `every target sits under the single-source origin`() {
        for (link in ConsentLink.entries) {
            assertTrue(link.url, link.url.startsWith("${CleansiaWeb.ORIGIN}/"))
        }
    }

    @Test
    fun `a sentence whose translation dropped the markup is returned intact`() {
        val plain = "Souhlasim s podminkami"

        assertEquals(plain, ConsentMarkup.resolveTargets(plain))
    }
}
