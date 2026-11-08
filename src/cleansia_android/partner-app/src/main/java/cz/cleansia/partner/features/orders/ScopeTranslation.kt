package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Translation

fun resolveTranslatedName(
    translations: Map<String, Translation>?,
    lang: String?,
    fallback: String?,
): String? {
    if (lang.isNullOrBlank() || translations.isNullOrEmpty()) return fallback
    // A present translation with a null OR blank name degrades to the fallback, so the seam is
    // self-sufficient rather than relying on call-site isNotBlank guards.
    return translations[lang]?.name?.takeIf { it.isNotBlank() } ?: fallback
}
