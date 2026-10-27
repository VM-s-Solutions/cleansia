package cz.cleansia.customer.features.auth

import cz.cleansia.core.validation.PasswordPolicy

/**
 * The Verify gate for [EmailVerifyScreen]: a complete code and, when the screen asks for the password the
 * confirmation sets, one that keeps the server's rules typed twice the same.
 */
object EmailVerifyForm {

    const val CODE_LENGTH = 6

    fun isSubmittable(code: String, asksForPassword: Boolean, password: String, confirmPassword: String): Boolean =
        code.length == CODE_LENGTH &&
            (!asksForPassword || PasswordPolicy.isValid(password) && PasswordPolicy.passwordsMatch(password, confirmPassword))
}
