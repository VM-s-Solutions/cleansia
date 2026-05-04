package cz.cleansia.customer.features.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.auth.AuthRepository
import cz.cleansia.customer.core.auth.SessionEvent
import cz.cleansia.customer.core.auth.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.launch

/**
 * Observes app-wide session events (forced sign-out from the Authenticator,
 * user-initiated logout) and exposes them as a flow that the root nav host
 * subscribes to. Also hosts the user-initiated `logout()` entry point so
 * screens don't need to touch [AuthRepository] directly.
 */
@HiltViewModel
class SessionViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    sessionManager: SessionManager,
) : ViewModel() {

    /** Re-emitted session events — nav host collects to drive route transitions. */
    val events: SharedFlow<SessionEvent> = sessionManager.events

    fun logout() {
        viewModelScope.launch {
            authRepository.logout()
        }
    }
}
