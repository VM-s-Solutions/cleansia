package cz.cleansia.partner.features.orders

import android.content.ClipData
import android.content.ClipDescription
import android.os.PersistableBundle
import io.mockk.Runs
import io.mockk.every
import io.mockk.just
import io.mockk.mockk
import io.mockk.mockkConstructor
import io.mockk.mockkStatic
import io.mockk.unmockkAll
import io.mockk.verify
import org.junit.After
import org.junit.Assert.assertSame
import org.junit.Test

/**
 * The customer's entry instructions are a key to a home: copied, they must not outlive the session in the keyboard's
 * clipboard history or show in the system's clipboard preview.
 */
class AccessCopyTest {

    @After
    fun tearDown() = unmockkAll()

    @Test
    fun `the access text is copied marked sensitive`() {
        val description = mockk<ClipDescription>(relaxed = true)
        val clip = mockk<ClipData> { every { this@mockk.description } returns description }
        mockkStatic(ClipData::class)
        every { ClipData.newPlainText(any(), any()) } returns clip
        mockkConstructor(PersistableBundle::class)
        every { anyConstructed<PersistableBundle>().putBoolean(any(), any()) } just Runs

        assertSame(clip, sensitiveClip("Copied", "Kód 4512, zvonek Nováková"))

        verify(exactly = 1) { ClipData.newPlainText("Copied", "Kód 4512, zvonek Nováková") }
        verify(exactly = 1) { anyConstructed<PersistableBundle>().putBoolean("android.content.extra.IS_SENSITIVE", true) }
        verify(exactly = 1) { description.setExtras(any()) }
    }
}
