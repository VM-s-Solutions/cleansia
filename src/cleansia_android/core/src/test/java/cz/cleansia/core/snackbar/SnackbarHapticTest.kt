package cz.cleansia.core.snackbar

import android.view.HapticFeedbackConstants
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The haptic each shown message plays. iOS `SnackbarController.show` plays error, success and warning
 * and keeps info silent; these hold Android to the same three, and to a constant the device has.
 */
class SnackbarHapticTest {

    @Test
    fun `an outcome plays the matching haptic from API 30`() {
        assertEquals(HapticFeedbackConstants.CONFIRM, snackbarHaptic(Severity.Success, sdkInt = 30))
        assertEquals(HapticFeedbackConstants.REJECT, snackbarHaptic(Severity.Error, sdkInt = 30))
        assertEquals(HapticFeedbackConstants.REJECT, snackbarHaptic(Severity.Warning, sdkInt = 34))
    }

    /** CONFIRM and REJECT are API 30; on 26-29 the system would ignore them and play nothing. */
    @Test
    fun `below API 30 every outcome falls back to the long-press haptic`() {
        listOf(Severity.Success, Severity.Error, Severity.Warning).forEach { severity ->
            assertEquals(severity.name, HapticFeedbackConstants.LONG_PRESS, snackbarHaptic(severity, sdkInt = 29))
            assertEquals(severity.name, HapticFeedbackConstants.LONG_PRESS, snackbarHaptic(severity, sdkInt = 26))
        }
    }

    @Test
    fun `info reports no outcome and stays silent`() {
        assertNull(snackbarHaptic(Severity.Info, sdkInt = 30))
        assertNull(snackbarHaptic(Severity.Info, sdkInt = 26))
    }
}
