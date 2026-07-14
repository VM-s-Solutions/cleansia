package cz.cleansia.partner.features.auth

import androidx.lifecycle.ViewModel
import cz.cleansia.core.auth.SessionEvent
import cz.cleansia.core.auth.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.SharedFlow

/**
 * Observes app-wide session events (forced sign-out from the Authenticator on
 * refresh failure) and exposes them as a flow the root nav host subscribes to.
 * User-initiated logout stays on the per-screen ViewModels, which navigate via
 * explicit onSignedOut callbacks.
 */
@HiltViewModel
class SessionViewModel @Inject constructor(
    sessionManager: SessionManager,
) : ViewModel() {

    /** Re-emitted session events — nav host collects to drive route transitions. */
    val events: SharedFlow<SessionEvent> = sessionManager.events
}
