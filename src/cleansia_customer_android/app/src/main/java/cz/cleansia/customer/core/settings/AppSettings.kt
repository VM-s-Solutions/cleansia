package cz.cleansia.customer.core.settings

/**
 * Persisted user preferences. Owner: [AppSettingsRepository].
 * Centralised so the theme + locale picker can be tested / observed in isolation.
 */
enum class ThemePreference {
    System,
    Light,
    Dark,
}

/**
 * Supported app locales. Matches the `locales_config.xml` entries.
 * `System` means "follow device locale, fall back to English".
 */
enum class LanguagePreference(val tag: String?) {
    System(null),
    English("en"),
    Czech("cs"),
    Slovak("sk"),
    Ukrainian("uk"),
    Russian("ru"),
}

data class AppSettings(
    val theme: ThemePreference = ThemePreference.System,
    val language: LanguagePreference = LanguagePreference.System,
)
