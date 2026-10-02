package cz.cleansia.customer.features.home

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The Home notifications slide raises the system dialog while it can still appear, and the settings
 * page otherwise. Android shows no rationale both for a permission never asked and for one refused
 * for good, so a recorded refusal tells the two apart.
 */
class NotificationPermissionAskTest {

    private fun asks(sdkInt: Int = 33, granted: Boolean = false, rationale: Boolean = false, refused: Boolean = false) =
        asksForNotificationPermission(sdkInt, granted, rationale, refused)

    @Test
    fun `a permission never refused raises the dialog`() {
        assertTrue(asks(rationale = false, refused = false))
    }

    @Test
    fun `a permission refused once raises the dialog again`() {
        assertTrue(asks(rationale = true, refused = true))
    }

    @Test
    fun `a permission refused for good opens the settings page`() {
        assertFalse(asks(rationale = false, refused = true))
    }

    /** Granted but switched off in settings: no dialog can switch it back on. */
    @Test
    fun `notifications switched off with the permission granted open the settings page`() {
        assertFalse(asks(granted = true))
    }

    @Test
    fun `below API 33 there is no permission to ask`() {
        assertFalse(asks(sdkInt = 32))
    }
}
