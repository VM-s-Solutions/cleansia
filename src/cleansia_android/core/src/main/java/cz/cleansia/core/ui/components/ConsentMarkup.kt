package cz.cleansia.core.ui.components

import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextLinkStyles
import androidx.compose.ui.text.fromHtml
import cz.cleansia.core.config.CleansiaWeb

/**
 * The link targets a consent sentence may carry. Translators write the
 * placeholder, never the real address — a URL baked into a translation is one
 * that ten `strings.xml` files would each have to be re-translated to change.
 *
 * Mirrors `ConsentLink` on iOS, including the `cleansia://` placeholder scheme,
 * so the same translated copy can be lifted between platforms unchanged.
 */
enum class ConsentLink(val placeholder: String, val url: String) {
    TERMS("cleansia://terms", CleansiaWeb.TERMS_URL),
    PRIVACY("cleansia://privacy", CleansiaWeb.PRIVACY_URL),
}

/**
 * Turns a localized consent sentence carrying `<a href="cleansia://…">` markup
 * into an [AnnotatedString] whose links point at the real web pages.
 *
 * ## The markup has to survive AAPT, and nothing warns you when it doesn't
 *
 * A `<a href>` written bare in `strings.xml` is compiled into a *style span*,
 * not into text. `stringResource()` calls `Resources.getString()`, which drops
 * every span — so the sentence arrives here as plain prose, [annotated] finds
 * no anchors, and the row renders correct-looking copy with zero tappable
 * links. There is no compile error, no lint (Android CI runs none) and no
 * runtime warning. The markup MUST therefore be `<![CDATA[…]]>`-wrapped (or
 * entity-escaped) in every locale file; `ConsentCatalogTest` is the only thing
 * standing between a bare `<a>` and a silent ship.
 *
 * A sentence whose markup a translation broke or dropped still renders in full,
 * as plain text — the consent copy is legally load-bearing, so losing a link is
 * strictly better than losing the sentence.
 */
object ConsentMarkup {
    /**
     * Rewrites every [ConsentLink] placeholder to its real https address.
     * Split out from [annotated] because it is the only half that can be
     * exercised without the Android framework.
     */
    fun resolveTargets(html: String): String =
        ConsentLink.entries.fold(html) { acc, link -> acc.replace(link.placeholder, link.url) }

    fun annotated(html: String, linkStyles: TextLinkStyles): AnnotatedString =
        AnnotatedString.fromHtml(resolveTargets(html), linkStyles = linkStyles)
}
