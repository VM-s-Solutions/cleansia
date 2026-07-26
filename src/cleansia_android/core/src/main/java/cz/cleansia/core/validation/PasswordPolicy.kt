package cz.cleansia.core.validation

/**
 * The one password policy, and it is the server's.
 *
 * This mirrors `ValidationExtensions.PasswordPattern`
 * (`^(?=.*[a-zA-Z])(?=.*\d).{8,}$`) in Cleansia.Core.AppServices, which is the
 * single source of truth every auth endpoint composes, and the iOS twin
 * `CleansiaCore/Sources/CleansiaCore/Validation/PasswordPolicy.swift`.
 * It must not drift from either: a client that demands MORE than the server
 * blocks a password the API would happily accept, with no error text to
 * explain the refusal — which is exactly what four screens did while they
 * gated at twelve characters.
 *
 * There is deliberately no uppercase, lowercase or special-character rule.
 * The server has none.
 *
 * Kept free of android.* imports so it runs under plain-JVM unit tests, the
 * same reason [EmailValidator] lives in this package.
 */
object PasswordPolicy {

    /** Minimum accepted password length, matching the server's `{8,}`. */
    const val MIN_LENGTH = 8

    /** True when [password] is at least [MIN_LENGTH] characters. */
    fun hasMinLength(password: String): Boolean = password.length >= MIN_LENGTH

    /** True when [password] contains at least one letter. */
    fun hasLetter(password: String): Boolean = password.any { it.isLetter() }

    /** True when [password] contains at least one digit. */
    fun hasNumber(password: String): Boolean = password.any { it.isDigit() }

    /** True when [password] satisfies every rule the server enforces. */
    fun isValid(password: String): Boolean =
        hasMinLength(password) && hasLetter(password) && hasNumber(password)

    /** True when [confirmation] repeats a non-empty [password]. */
    fun passwordsMatch(password: String, confirmation: String): Boolean =
        password.isNotEmpty() && password == confirmation
}
