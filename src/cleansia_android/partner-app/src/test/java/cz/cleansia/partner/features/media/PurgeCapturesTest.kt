package cz.cleansia.partner.features.media

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

class PurgeCapturesTest {

    @get:Rule
    val temp = TemporaryFolder()

    @Test
    fun `every leftover capture is deleted and the folder is kept for the next one`() {
        val cacheDir = temp.newFolder("cache")
        val camera = File(cacheDir, "camera").apply { mkdirs() }
        File(camera, "capture-1.jpg").writeText("photo")
        File(camera, "capture-2.jpg").createNewFile()

        purgeCaptures(cacheDir)

        assertTrue(camera.isDirectory)
        assertEquals(emptyList<String>(), camera.list()!!.toList())
    }

    @Test
    fun `the rest of the cache is left alone`() {
        val cacheDir = temp.newFolder("cache")
        val invoice = File(cacheDir, "invoices/invoice.pdf").apply {
            parentFile!!.mkdirs()
            writeText("pdf")
        }
        File(cacheDir, "camera").mkdirs()

        purgeCaptures(cacheDir)

        assertTrue(invoice.isFile)
    }

    @Test
    fun `a start before any capture was taken does nothing`() {
        val cacheDir = temp.newFolder("cache")

        purgeCaptures(cacheDir)

        assertFalse(File(cacheDir, "camera").exists())
    }
}
