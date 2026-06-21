package cz.cleansia.core.validation

import java.util.regex.Pattern

/**
 * Pure-Kotlin email-format check, framework-free so ViewModels that validate
 * an email are unit-testable on plain JVM. Previously the ViewModels called
 * android.util.Patterns.EMAIL_ADDRESS, which is null under a non-instrumented
 * unit test (no Android runtime), so every such test threw NPE.
 *
 * The pattern mirrors android.util.Patterns.EMAIL_ADDRESS (the AOSP source) so
 * on-device behaviour is unchanged: the same local-part / domain / TLD shapes
 * accepted there are accepted here.
 */
object EmailValidator {

    private val EMAIL_ADDRESS: Pattern = Pattern.compile(
        "[a-zA-Z0-9\\+\\.\\_\\%\\-\\+]{1,256}" +
            "\\@" +
            "[a-zA-Z0-9][a-zA-Z0-9\\-]{0,64}" +
            "(" +
            "\\." +
            "[a-zA-Z0-9][a-zA-Z0-9\\-]{0,25}" +
            ")+"
    )

    /** True when [email] is a syntactically valid email address. */
    fun isValid(email: String): Boolean = EMAIL_ADDRESS.matcher(email).matches()
}
