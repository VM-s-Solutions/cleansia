package cz.cleansia.partner.core.settings

/**
 * How each [LanguagePreference] is written in a picker row.
 *
 * The five real languages are labelled with their *native* names, never
 * translated ones: someone whose app is currently stuck in a language they
 * cannot read has to be able to find their own. That is why these are hard
 * strings and not `strings.xml` rows — a translated "Czech"/"Tschechisch"
 * would defeat the purpose.
 *
 * [LanguagePreference.System] is the exception. It has no native name because
 * it is not a language; it is labelled with the translated `language_system`
 * resource, which is why [nativeName] returns null for it and every call site
 * elvis-es into `stringResource(R.string.language_system)`.
 *
 * Consolidated here because the same six-way `when` had been copied into the
 * profile summary row and the picker screen, and the pre-auth onboarding
 * chooser would have made three.
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
