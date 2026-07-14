package cz.cleansia.partner.features.auth

import app.cash.turbine.test
import cz.cleansia.core.auth.ForcedSignOutReason
import cz.cleansia.core.auth.SessionEvent
import cz.cleansia.core.auth.SessionManager
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

class SessionViewModelTest {

    @Test
    fun `re-emits forced sign-out from the session manager`() = runTest {
        val sessionManager = SessionManager()
        val viewModel = SessionViewModel(sessionManager)

        viewModel.events.test {
            sessionManager.emitForcedSignOut(ForcedSignOutReason.SessionExpired)
            assertEquals(
                SessionEvent.ForcedSignOut(ForcedSignOutReason.SessionExpired),
                awaitItem(),
            )
        }
    }

    @Test
    fun `preserves the sign-out reason`() = runTest {
        val sessionManager = SessionManager()
        val viewModel = SessionViewModel(sessionManager)

        viewModel.events.test {
            sessionManager.emitForcedSignOut(ForcedSignOutReason.Compromised)
            assertEquals(
                SessionEvent.ForcedSignOut(ForcedSignOutReason.Compromised),
                awaitItem(),
            )
        }
    }
}
