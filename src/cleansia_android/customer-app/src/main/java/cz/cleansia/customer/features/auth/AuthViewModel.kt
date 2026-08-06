package cz.cleansia.customer.features.auth

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.auth.AuthRepository
import cz.cleansia.customer.core.auth.AuthSuccess
import cz.cleansia.customer.core.auth.GoogleSignInController
import cz.cleansia.customer.core.auth.GoogleSignInResult
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Single ViewModel serving all four auth screens (SignIn, SignUp, EmailVerify,
 * ForgotPassword). Each screen observes [uiState] for the loading flag + the
 * last outcome, and calls the method it needs.
 *
 * Keeping it in one VM means the screens don't fight over shared side effects
 * (e.g. post-register → auto-fill email on verify screen), and there's one
 * place to reason about the auth FSM.
 */
@HiltViewModel
class AuthViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val settings: AppSettingsRepository,
    private val snackbar: SnackbarController,
    private val googleSignInController: GoogleSignInController,
    private val signupConsent: SignupConsentRepository,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AuthUiState())
    val uiState: StateFlow<AuthUiState> = _uiState.asStateFlow()

    private val _passwordResetCodeSent = MutableSharedFlow<Unit>(extraBufferCapacity = 1)

    /**
     * Emits once per password-reset code the **server actually accepted**.
     *
     * Both screens that request a reset code (ForgotPassword and profile →
     * Security) used to flip their own `isEmailSent` / `codeSent` flag inside
     * the button's `onClick`, so a 429, a 500 or airplane mode still walked the
     * user forward to a code-entry form for a code that was never sent — with
     * only a snackbar, already gone by the time they finished reading it, to
     * say otherwise. They now advance off this event instead.
     *
     * It is deliberately **not** a flag on [AuthUiState]: every path in this
     * class assigns a whole new `AuthUiState`, so a later unrelated failure
     * (a rejected code, a resend that 500s) would reset the flag and bounce the
     * user back to the email step mid-flow. An event that the screen latches
     * once cannot be un-set by an unrelated write.
     */
    val passwordResetCodeSent: SharedFlow<Unit> = _passwordResetCodeSent.asSharedFlow()

    fun signIn(email: String, password: String) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            _uiState.value = authRepository.login(email, password, LONG_LIVED_SESSION)
                .toAuthUiState(fallbackEmail = email)
        }
    }

    fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        /**
         * Loyalty Phase C — optional referral code from the signup form.
         * Trimmed/blank-coalesced to null so we don't ship empty strings on
         * the wire. Bad codes don't block submit (backend is fail-soft).
         */
        referralCode: String? = null,
        /**
         * The terms box on [SignUpScreen], whose sentence names both documents by title.
         * Carried explicitly rather than read off a form the ViewModel cannot see, and
         * deliberately without a default — a consent flag that can be omitted at a call
         * site is a consent record nobody agreed to.
         */
        acceptedTerms: Boolean,
    ) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            val language = currentLanguageCode()
            authRepository.register(
                email = email,
                password = password,
                firstName = firstName,
                lastName = lastName,
                language = language,
                referralCode = referralCode?.trim()?.uppercase()?.ifBlank { null },
            )
                .onSuccess {
                    signupConsent.recordSignupTick(email, acceptedTerms)
                    _uiState.value = AuthUiState(outcome = AuthOutcome.NeedsEmailConfirm(email))
                }
                .onError { error ->
                    // Signup is the one auth flow with nothing to hide: the form has to
                    // tell you an email is taken or it can't work. So show the server's
                    // own translated key ("user.existing_email", "auth.invalid_password_format")
                    // rather than "something went wrong", which sends users back to
                    // re-typing a password that was never the problem.
                    // The forgot-password / resend siblings below deliberately stay vague —
                    // there a precise "no such account" is an enumeration oracle.
                    snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, error))
                    _uiState.value = AuthUiState()
                }
        }
    }

    fun confirmEmail(email: String?, code: String) {
        if (email.isNullOrBlank()) {
            // The 6-digit code only proves possession relative to the account it was issued to —
            // without the email there is nothing to verify against (reachable only via a nav bug).
            snackbar.showErrorKey(R.string.error_generic_unknown)
            return
        }
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            _uiState.value = when (val result = authRepository.confirmEmail(email, code)) {
                is ApiResult.Success -> when (result.data) {
                    is AuthSuccess.Authenticated -> AuthUiState(outcome = AuthOutcome.SignedIn)
                    is AuthSuccess.EmailUnconfirmed -> {
                        // Unexpected but harmless — server said not confirmed after a successful confirm call.
                        snackbar.showWarningKey(R.string.error_auth_invalid_confirmation_code)
                        AuthUiState()
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, result.error))
                    AuthUiState()
                }
            }
        }
    }

    fun resendConfirmationEmail(email: String) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            val language = currentLanguageCode()
            authRepository.resendConfirmationEmail(email, language)
                .onSuccess {
                    snackbar.showSuccessKey(R.string.auth_resend_success)
                    _uiState.value = AuthUiState()
                }
                .onError {
                    snackbar.showErrorKey(R.string.error_email_sending_failed)
                    _uiState.value = AuthUiState()
                }
        }
    }

    fun requestPasswordChange(email: String) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            val language = currentLanguageCode()
            authRepository.requestPasswordChange(email, language)
                .onSuccess {
                    snackbar.showSuccessKey(R.string.forgot_code_sent)
                    // Only now is there a code to type. See [passwordResetCodeSent].
                    _passwordResetCodeSent.tryEmit(Unit)
                    _uiState.value = AuthUiState()
                }
                .onError {
                    snackbar.showErrorKey(R.string.forgot_send_failed)
                    _uiState.value = AuthUiState()
                }
        }
    }

    /**
     * Launches the Google Account picker via Credential Manager, then sends the
     * resulting ID token to the backend's GoogleAuth handler. Caller passes an
     * Activity-scoped Context (typically from `LocalContext.current` in a Composable)
     * so the bottom-sheet attaches correctly.
     */
    fun signInWithGoogle(activityContext: Context) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            when (val pick = googleSignInController.signIn(activityContext)) {
                is GoogleSignInResult.Success -> {
                    _uiState.value = authRepository.googleAuth(
                        googleIdToken = pick.idToken,
                        googleId = pick.googleId,
                        email = pick.email,
                        firstName = pick.firstName,
                        lastName = pick.lastName,
                    ).toAuthUiState(fallbackEmail = pick.email)
                }
                GoogleSignInResult.Cancelled -> {
                    // User dismissed the picker — no toast, just clear loading.
                    _uiState.value = AuthUiState()
                }
                GoogleSignInResult.NoAccount -> {
                    snackbar.showWarningKey(R.string.auth_google_no_account)
                    _uiState.value = AuthUiState()
                }
                GoogleSignInResult.NotConfigured -> {
                    snackbar.showErrorKey(R.string.auth_google_not_configured)
                    _uiState.value = AuthUiState()
                }
                GoogleSignInResult.Failure -> {
                    snackbar.showErrorKey(R.string.auth_google_failed)
                    _uiState.value = AuthUiState()
                }
            }
        }
    }

    fun changePassword(email: String, code: String, newPassword: String) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            authRepository.changePassword(email, code, newPassword)
                .onSuccess {
                    snackbar.showSuccessKey(R.string.forgot_password_changed)
                    _uiState.value = AuthUiState(outcome = AuthOutcome.PasswordReset)
                }
                .onError {
                    snackbar.showErrorKey(R.string.forgot_change_failed)
                    _uiState.value = AuthUiState()
                }
        }
    }

    /** User tapped the screen-level "dismiss" / after navigation handled an outcome, UI clears it. */
    fun clearState() {
        _uiState.value = AuthUiState()
    }

    private fun ApiResult<AuthSuccess>.toAuthUiState(fallbackEmail: String): AuthUiState = when (this) {
        is ApiResult.Success -> when (val outcome = data) {
            is AuthSuccess.Authenticated -> AuthUiState(outcome = AuthOutcome.SignedIn)
            is AuthSuccess.EmailUnconfirmed ->
                AuthUiState(outcome = AuthOutcome.NeedsEmailConfirm(outcome.email ?: fallbackEmail))
        }
        is ApiResult.Error -> {
            snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, error))
            AuthUiState()
        }
    }

    /**
     * The language code the backend expects on emails sent to this user.
     *
     * This used to be `settings.first().language.tag ?: "en"` inline. The default
     * preference is `System`, whose tag is null, so the fallback fired for every
     * fresh install and confirmation / reset emails always arrived in English.
     * [AppSettingsRepository.emailLanguageTag] resolves the device's own
     * languages instead, clamped to the five the backend accepts.
     */
    private suspend fun currentLanguageCode(): String = settings.emailLanguageTag()

    private companion object {
        /**
         * The `rememberMe` flag on `Auth/Login`, pinned to the 30-day refresh lifetime.
         *
         * The sign-in screen used to offer this as a checkbox that defaulted to *unchecked*,
         * so the common case asked for the 24-hour token. It is now one value for every
         * mobile surface (iOS customer, iOS partner, Android partner all send `true` too).
         * A handset is a personal device: the short token only bought a forced re-login after
         * a day away, and the security it implied is already carried by single-use rotating
         * refresh tokens, EncryptedSharedPreferences, and per-device revocation.
         *
         * Kept on the wire rather than dropped because the command still declares it and the
         * web keeps its own checkbox — no server change. Note this app's server side
         * (`MobileLogin`) already discarded the flag and forced the long lifetime, so nothing
         * observable changes for a customer; the value is aligned so the client stops
         * claiming something the server ignores.
         */
        const val LONG_LIVED_SESSION = true
    }
}

data class AuthUiState(
    val loading: Boolean = false,
    val outcome: AuthOutcome? = null,
)

sealed class AuthOutcome {
    /** Logged in with a valid confirmed-email session. Navigate to Home. */
    data object SignedIn : AuthOutcome()

    /** Login / register succeeded but user needs to verify email. Email pre-filled for the next screen. */
    data class NeedsEmailConfirm(val email: String) : AuthOutcome()

    /** Forgot-password flow completed (new password accepted). Navigate back to SignIn. */
    data object PasswordReset : AuthOutcome()
}
