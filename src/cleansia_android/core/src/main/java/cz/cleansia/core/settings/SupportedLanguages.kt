package cz.cleansia.core.settings

/**
 * Resolves the language code the backend should render transactional emails in.
 *
 * Both apps let the user pick a language, and both default that picker to
 * "System" — which is stored as a *null* tag. Every call site that sends a
 * language to the API used to collapse that null straight to `"en"`, so a fresh
 * install on a Czech handset received its confirmation and password-reset
 * emails in English no matter what the phone was set to. This is the shared
 * resolver they now go through instead: an explicit choice wins, otherwise we
 * scan the device's ordered preferred-locale list, and only then fall back to
 * English.
 *
 * It is the Kotlin twin of `AppSettingsStore.languageTag` in
 * `CleansiaCore/Sources/CleansiaCore/Settings/AppSettingsStore.swift`, down to
 * the bare-code narrowing, and must not drift from it.
 *
 * ## Why the clamp to [SUPPORTED] is load-bearing
 * It is not defensive tidiness. `Register.Validator`
 * (`Cleansia.Core.AppServices/Features/Auth/Register.cs`) runs `LanguageValidator`,
 * which looks the code up with `ExistsWithCodeAsync` and fails the whole command
 * with `LanguageNotSupported`. Passing a raw device tag — `"de-DE"` off a German
 * phone, or `"pl"` — would turn a cosmetic email-language bug into a hard
 * registration failure for every user outside the five locales. Never hand an
 * unfiltered device tag to the API; always come through [resolve].
 *
 * Deliberately free of `android.*` so it runs under plain-JVM unit tests. The
 * part that actually needs a `Context` (reading the device locale list) lives in
 * each app's `AppSettingsRepository`.
 */
object SupportedLanguages {

    /**
     * The five languages the platform ships. Mirrors `res/xml/locales_config.xml`
     * in both apps and the `Language` seed rows the backend validates against.
     */
    val SUPPORTED = listOf("en", "cs", "sk", "uk", "ru")

    /** Used when neither the picker nor the device offers anything supported. */
    const val DEFAULT = "en"

    /**
     * Narrows a possibly region- or script-qualified tag to its bare language
     * code: `"cs-CZ"` → `"cs"`, `"sk_SK"` → `"sk"`, `"ru-Cyrl-RU"` → `"ru"`.
     *
     * [SUPPORTED] holds bare codes, and Android hands back qualified ones
     * (`Locale.toLanguageTag()` on a Czech phone gives `"cs-CZ"`), so matching
     * without this step would silently never hit. Returns null for a blank or
     * headless tag rather than an empty string, so such entries are skipped by
     * the scan instead of matching nothing noisily.
     */
    fun bareCode(tag: String): String? {
        val trimmed = tag.trim()
        if (trimmed.isEmpty()) return null
        return trimmed.split('-', '_').first().lowercase().ifEmpty { null }
    }

    /**
     * @param persistedTag the tag behind the user's explicit language choice,
     *   or null when the picker is on "System".
     * @param devicePreferred the device's preferred locales, **in order** — a
     *   user whose first language is German and second is Czech should get
     *   Czech, not English, which only works if the whole ordered list is passed.
     */
    fun resolve(persistedTag: String?, devicePreferred: List<String>): String =
        persistedTag?.takeIf { it in SUPPORTED }
            ?: devicePreferred.asSequence().mapNotNull(::bareCode).firstOrNull { it in SUPPORTED }
            ?: DEFAULT
}
