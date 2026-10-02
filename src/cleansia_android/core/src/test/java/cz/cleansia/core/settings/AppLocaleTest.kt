package cz.cleansia.core.settings

import android.content.Context
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import org.junit.Assert.assertSame
import org.junit.Test

/**
 * Which contexts get re-localized. The repo has no Robolectric, so what the wrapped configuration
 * carries is proven on a device (API 30 emulator, English phone, Czech in the app, app swiped away);
 * this pins the decision: wrap only below API 33, and only when the user chose a language.
 */
class AppLocaleTest {

    private val wrapped: Context = mockk(relaxed = true)
    private val base: Context = mockk(relaxed = true) {
        every { createConfigurationContext(any()) } returns wrapped
    }

    @Test
    fun `below API 33 a chosen language wraps the context`() {
        (26..32).forEach { sdk ->
            assertSame("API $sdk", wrapped, AppLocale.localizedContext(base, "cs", sdkInt = sdk))
        }
        verify(exactly = 7) { base.createConfigurationContext(any()) }
    }

    @Test
    fun `from API 33 the framework's per-app locale already covers it`() {
        listOf(33, 34, 35).forEach { sdk ->
            assertSame("API $sdk", base, AppLocale.localizedContext(base, "cs", sdkInt = sdk))
        }
        verify(exactly = 0) { base.createConfigurationContext(any()) }
    }

    @Test
    fun `following the device needs no wrap on any API level`() {
        listOf(26, 30, 32, 33, 35).forEach { sdk ->
            assertSame("API $sdk", base, AppLocale.localizedContext(base, null, sdkInt = sdk))
        }
        verify(exactly = 0) { base.createConfigurationContext(any()) }
    }
}
