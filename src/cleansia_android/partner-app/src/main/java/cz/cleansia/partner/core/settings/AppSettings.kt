package cz.cleansia.partner.core.settings

enum class ThemePreference {
    System,
    Light,
    Dark,
}

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
