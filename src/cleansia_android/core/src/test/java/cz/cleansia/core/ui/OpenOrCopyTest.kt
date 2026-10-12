package cz.cleansia.core.ui

import android.content.ActivityNotFoundException
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** A contact intent no app on the phone takes copies the address or number instead of doing nothing. */
class OpenOrCopyTest {

    private val clipboard = mockk<ClipboardManager>(relaxed = true)
    private val context = mockk<Context>(relaxed = true).also {
        every { it.getSystemService(Context.CLIPBOARD_SERVICE) } returns clipboard
    }
    private val intent = mockk<Intent>()

    @Test
    fun `an intent an app takes is started and nothing is copied`() {
        assertTrue(context.openOrCopy(intent, "support@cleansia.cz"))

        verify(exactly = 1) { context.startActivity(intent) }
        verify(exactly = 0) { clipboard.setPrimaryClip(any()) }
    }

    @Test
    fun `an intent no app takes copies the value and reports it`() {
        every { context.startActivity(intent) } throws ActivityNotFoundException()

        assertFalse(context.openOrCopy(intent, "support@cleansia.cz"))

        verify(exactly = 1) { clipboard.setPrimaryClip(any()) }
    }
}
