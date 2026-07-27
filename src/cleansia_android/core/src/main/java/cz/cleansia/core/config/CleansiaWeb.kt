package cz.cleansia.core.config

/**
 * The public web app both Android apps link out to. Mirrors
 * `CleansiaCore/Sources/CleansiaCore/Config/CleansiaWeb.swift` on iOS: a move
 * off `.cz` (a `.eu` domain is under consideration) must be a one-line edit
 * here — never a grep across two apps and ten `strings.xml` files. That is why
 * the consent sentences carry [ConsentLink] placeholders instead of real
 * addresses, and why `ConsentCatalogTest` fails any translation that spells
 * [DOMAIN] out.
 */
object CleansiaWeb {
    const val DOMAIN = "cleansia.cz"

    const val ORIGIN = "https://$DOMAIN"

    /** Routed by the customer web app (`app.routes.ts`). */
    const val TERMS_URL = "$ORIGIN/terms"

    const val PRIVACY_URL = "$ORIGIN/privacy"
}
