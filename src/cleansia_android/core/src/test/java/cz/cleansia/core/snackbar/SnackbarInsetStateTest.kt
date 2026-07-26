package cz.cleansia.core.snackbar

import androidx.compose.ui.unit.dp
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Pure-JVM tests for [SnackbarInsetState]. `Dp` is a Kotlin value class with no
 * Android runtime dependency and every assertion below is a synchronous read of
 * `insetDp.value`, so no Robolectric and no test dispatcher are needed.
 *
 * [SnackbarInsetState] is a process-wide `object`, so state leaks between test
 * methods. Every token pushed by a test MUST be recorded via [push] so that
 * [tearDown] can pop it — a test that pushes directly and forgets to pop would
 * pin the inset and fail whichever test JUnit happens to run next.
 */
class SnackbarInsetStateTest {

    private val pushed = mutableListOf<Long>()

    private fun push(value: Int): Long =
        SnackbarInsetState.push(value.dp).also { pushed += it }

    private fun pop(token: Long) {
        SnackbarInsetState.pop(token)
        pushed -= token
    }

    private fun current() = SnackbarInsetState.insetDp.value

    @After
    fun tearDown() {
        pushed.toList().forEach { pop(it) }
    }

    @Test
    fun `reports the default when no scope is active`() {
        assertEquals(SnackbarInsetState.DEFAULT_INSET, current())

        val token = push(88)
        pop(token)

        assertEquals(SnackbarInsetState.DEFAULT_INSET, current())
    }

    @Test
    fun `nested push then pop restores the outer value, not the default`() {
        // The exact production repro: MainShell pushes 88 for the bottom nav and
        // stays composed; BookingBottomSheet pushes 120 while it is visible. The
        // old reset() slammed the value to 96 when the sheet closed, and
        // MainShell's DisposableEffect — keyed on a constant 88.dp — never
        // re-ran, so every home-tab snackbar sat 8dp wrong for the rest of the
        // process lifetime. This assertion is the regression guard.
        val shell = push(88)
        val sheet = push(120)
        assertEquals(120.dp, current())

        pop(sheet)

        assertEquals(88.dp, current())
        pop(shell)
    }

    @Test
    fun `out-of-order disposal removes the right entry`() {
        // A NavHost transition composes the incoming destination before it
        // disposes the outgoing one, so the outgoing scope's onDispose fires
        // while the incoming scope's entry is already on top. Removal is by
        // token, so the live entry survives; a positional pop would kill it.
        val outgoing = push(88)
        val incoming = push(120)

        pop(outgoing)

        assertEquals(120.dp, current())
        pop(incoming)
        assertEquals(SnackbarInsetState.DEFAULT_INSET, current())
    }

    @Test
    fun `popping an unknown or already-popped token is a no-op`() {
        val shell = push(88)

        SnackbarInsetState.pop(9999L)
        assertEquals(88.dp, current())

        val sheet = push(120)
        pop(sheet)
        SnackbarInsetState.pop(sheet)

        assertEquals(88.dp, current())
        pop(shell)
    }
}
