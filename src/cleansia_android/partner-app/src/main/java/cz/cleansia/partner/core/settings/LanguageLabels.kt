package cz.cleansia.partner.core.settings

/**
 * How each language is written in the picker.
 *
 * **The five real languages carry their NATIVE names, never translated ones** — someone stuck in a
 * language they cannot read has to be able to find their own. That is why these are hard strings rather
 * than resources: a translated "Czech" would defeat the purpose. System is the exception, because it is
 * not a language.
 */
object LanguageLabels {

    /** Display order for pickers: System first, then the five languages. */
    val ordered: List<LanguagePreference> = listOf(
        LanguagePreference.System,
        LanguagePreference.English,
        LanguagePreference.Czech,
        LanguagePreference.Slovak,
        LanguagePreference.Ukrainian,
        LanguagePreference.Russian,
    )

    fun nativeName(preference: LanguagePreference): String? = when (preference) {
        LanguagePreference.System -> null
        LanguagePreference.English -> "English"
        LanguagePreference.Czech -> "Čeština"
        LanguagePreference.Slovak -> "Slovenčina"
        LanguagePreference.Ukrainian -> "Українська"
        LanguagePreference.Russian -> "Русский"
    }
}
