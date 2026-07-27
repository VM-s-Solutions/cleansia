package cz.cleansia.core.settings

import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat

/**
 * The one place either app applies a per-app language to the running process.
 *
 * ## Why this exists at all
 * `AppCompatDelegate.setApplicationLocales` is only half of a persistent
 * preference, and which half you get depends on the API level:
 *
 *  - **API 33+** the call is forwarded to the framework's per-app locale
 *    service, which stores it. The choice survives a cold start for free.
 *  - **API 26–32** (our `minSdk` floor is 26) AppCompat has no framework
 *    service to forward to. It keeps the locales in a *process-scoped static*
 *    and, unless the app registers `AppLocalesMetadataHolderService` with
 *    `android:autoStoreLocales="true"` in its manifest, writes them nowhere.
 *    Neither app registers that service.
 *
 * So on API 26–32 a cleaner who picked Czech saw Czech until the process died
 * and English afterwards — while still receiving Czech email, because the
 * *email* language is read from DataStore, which did persist. The two halves
 * disagreed.
 *
 * We fix that from the DataStore side rather than by adding the manifest
 * service, deliberately: `AppSettingsRepository` is already the durable record
 * of the user's choice (it is what `emailLanguageTag()` reads), and
 * `autoStoreLocales` would introduce a second, AppCompat-owned store that can
 * drift from it. One source of truth, re-applied on every cold start via
 * [applyIfChanged].
 *
 * Requires an `AppCompatActivity` host — both apps' `MainActivity` is one.
 */
object AppLocale {

    /**
     * Applies [tag] now. A null tag means "follow the device locale".
     *
     * On API < 33 this recreates the visible Activity so already-composed
     * `stringResource` lookups re-resolve; call it *after* persisting the
     * choice, or the recreate races the write.
     */
    fun apply(tag: String?) {
        AppCompatDelegate.setApplicationLocales(localeList(tag))
    }

    /**
     * Applies [tag] only if the delegate is not already on it.
     *
     * Called from `MainActivity.onCreate` to restore the persisted choice.
     * The guard is what stops that from looping: on API 33+ the framework has
     * already restored the locale so the first check matches and nothing
     * happens, and on API 26–32 the single [apply] triggers one recreate whose
     * `onCreate` then finds a match and stops.
     */
    fun applyIfChanged(tag: String?) {
        val desired = localeList(tag)
        // Compared as tag strings rather than by LocaleListCompat equality: the
        // string form is the documented, stable surface, and a mismatch here is
        // self-correcting anyway — worst case we re-apply the value that is
        // already in force, which is idempotent and still terminates.
        if (AppCompatDelegate.getApplicationLocales().toLanguageTags() == desired.toLanguageTags()) {
            return
        }
        AppCompatDelegate.setApplicationLocales(desired)
    }

    private fun localeList(tag: String?): LocaleListCompat =
        if (tag == null) {
            LocaleListCompat.getEmptyLocaleList()
        } else {
            LocaleListCompat.forLanguageTags(tag)
        }
}
