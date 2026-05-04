package cz.cleansia.customer.core.auth

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

/**
 * App-wide auth signal bus. The auth interceptor emits [ForcedSignOut] when a
 * refresh attempt fails (server revoked our tokens / user was deleted / theft
 * detected / etc.). The navigation layer observes this flow and kicks the user
 * back to the SignIn screen, clearing the back stack.
 *
 * Kept separate from [TokenStore] because `TokenStore` is about persistence;
 * this is about event propagation. Overlap is tempting but mixing them makes
 * testing the interceptor harder.
 */
class SessionManager {
    private val _events = MutableSharedFlow<SessionEvent>(extraBufferCapacity = 1)
    val events: SharedFlow<SessionEvent> = _events.asSharedFlow()

    fun emitForcedSignOut(reason: ForcedSignOutReason) {
        _events.tryEmit(SessionEvent.ForcedSignOut(reason))
    }
}

sealed interface SessionEvent {
    data class ForcedSignOut(val reason: ForcedSignOutReason) : SessionEvent
}

enum class ForcedSignOutReason {
    /** Refresh token expired or was revoked by the server. Normal flow on long-idle. */
    SessionExpired,

    /** Server reported rotation-reuse (theft detection). Nuclear — all devices signed out. */
    Compromised,

    /** User tapped "Log out". Expected. */
    UserInitiated,
}
