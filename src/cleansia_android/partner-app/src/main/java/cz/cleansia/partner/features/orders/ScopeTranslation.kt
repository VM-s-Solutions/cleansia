package cz.cleansia.partner.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalConfiguration
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

@Composable
fun localizedScopeName(
    translations: Map<String, Translation>?,
    fallback: String?,
): String? = resolveTranslatedName(
    translations = translations,
    lang = LocalConfiguration.current.locales.get(0)?.language,
    fallback = fallback,
)
