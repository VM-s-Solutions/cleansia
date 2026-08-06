package cz.cleansia.customer.features.auth

import cz.cleansia.core.validation.PasswordPolicy

/**
 * The submit gate for [SignUpScreen]. Extracted from the composable so the terms tick can
 * be pinned as a hard blocker under a plain unit test: the Register button's `enabled`
 * flag is the only thing between an unticked form and an account created without the
 * consent its own sentence claims.
 */
object SignUpForm {

    fun isSubmittable(
        firstName: String,
        lastName: String,
        email: String,
        password: String,
        confirmPassword: String,
        acceptedTerms: Boolean,
    ): Boolean = firstName.isNotBlank() &&
        lastName.isNotBlank() &&
        email.isNotBlank() &&
        PasswordPolicy.isValid(password) &&
        PasswordPolicy.passwordsMatch(password, confirmPassword) &&
        acceptedTerms
}
