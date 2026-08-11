package cz.cleansia.core.ui.theme

import cz.cleansia.core.ui.theme.BundledFontFiles.codePoints
import cz.cleansia.core.ui.theme.BundledFontFiles.cyrillic
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The premise the Cyrillic fallback rests on, measured from the shipped binaries.
 *
 * Poppins is a Latin+Devanagari cut: it draws no Cyrillic at all, so every `ru`/`uk` string in a
 * Poppins slot is drawn by whatever the platform substitutes. Nunito, already bundled as the body
 * face, draws all of it. If a later Poppins build ever gains Cyrillic these assertions fail and say
 * the fallback has become unnecessary, instead of leaving that as folklore.
 */
class BundledFontCoverageTest {

    private companion object {
        val LATIN: Set<Int> = (('A'..'Z') + ('a'..'z') + ('0'..'9')).map { it.code }.toSet()
    }

    private fun bundle(prefix: String) =
        BundledFontFiles.byResId.values.filter { it.name.startsWith(prefix) }

    @Test
    fun `the font bundle on disk is what R names, and every file parses`() {
        val ids = BundledFontFiles.byResId
        assertEquals(
            "the bundle is no longer the six brand weights, or R.font ids collided and the id to " +
                "file map lost entries — either way the fallback pairing needs re-reading",
            6,
            ids.size,
        )
        ids.values.forEach { file ->
            assertTrue("$file is declared in R.font but not present", file.isFile)
            assertTrue("$file parsed to an empty cmap", codePoints(file).isNotEmpty())
        }
    }

    @Test
    fun `every Poppins weight draws Latin and not one Cyrillic code point`() {
        val weights = bundle("poppins")
        assertEquals("expected three Poppins weights", 3, weights.size)
        weights.forEach { file ->
            val covered = codePoints(file)
            assertTrue(
                "${file.name} is missing basic Latin, so this measurement is not trustworthy",
                covered.containsAll(LATIN),
            )
            assertEquals(
                "${file.name} now draws Cyrillic — the Nunito fallback is no longer load-bearing",
                emptySet<Int>(),
                covered intersect cyrillic,
            )
        }
    }

    @Test
    fun `every Nunito weight draws the whole Cyrillic set the apps can be asked to render`() {
        val weights = bundle("nunito")
        assertEquals("expected three Nunito weights", 3, weights.size)
        weights.forEach { file ->
            assertEquals(
                "${file.name} cannot draw every Cyrillic code point, so it is not a safe fallback",
                emptySet<Int>(),
                cyrillic - codePoints(file),
            )
        }
    }
}
