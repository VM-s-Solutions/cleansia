package cz.cleansia.customer.features.auth
import cz.cleansia.core.auth.AuthInterceptor
import cz.cleansia.core.auth.TokenStore

import androidx.lifecycle.ViewModel
import cz.cleansia.customer.core.referral.ReferralRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for [SignUpScreen]. Currently exposes the singleton
 * [ReferralRepository] so the screen can validate referral codes during
 * sign-up without reaching into the Application via EntryPointAccessors.
 *
 * Validate is safe without a token — AuthInterceptor skips the Authorization
 * header when TokenStore is empty, and the backend endpoint is
 * [AllowAnonymous].
 */
@HiltViewModel
class SignUpViewModel @Inject constructor(
    val referralRepository: ReferralRepository,
) : ViewModel()
