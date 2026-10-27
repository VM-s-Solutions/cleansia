package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A cleaner photographs an ID card as often as they pick a file, so Upload and Replace open the source
 * sheet rather than the file picker alone. The camera and the gallery never call back on a cancel, where
 * the file picker always did, so every entry point sets the Replace target itself: a target a cancelled
 * Replace left behind would turn the next Upload into a silent replace. There is no Compose harness in
 * this module, so the call sites are pinned as source.
 */
class DocumentSourcesCallSiteTest {

    private val screen: String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/profile/DocumentsSectionScreen.kt") }
        .first { it.isFile }
        .readText()
        .replace("\r\n", "\n")

    @Test
    fun `the documents screen opens the source sheet with a file row`() {
        assertTrue("the documents screen no longer uses the shared source picker", screen.contains("rememberPhotoSourcePicker("))
        assertTrue(
            "the file picker is no longer the sheet's file row",
            screen.contains("onChooseFile = { pickFile.launch(\"*/*\") }"),
        )
        assertEquals(
            "an entry point launches the file picker around the sheet",
            1,
            Regex("""pickFile\.launch\(""").findAll(screen).count(),
        )
    }

    @Test
    fun `Upload clears the Replace target and Replace sets it, both before the sheet opens`() {
        assertTrue(
            "the shared entry point no longer sets the target before opening the sheet",
            screen.contains("replacingDocumentId = documentId\n        source.open()"),
        )
        assertTrue("Upload no longer clears the Replace target", screen.contains("if (!preparing) openSourceFor(null)"))
        assertTrue("Replace no longer goes through the shared entry point", screen.contains("doc.documentId?.let(openSourceFor)"))
        assertEquals("the sheet is opened around the shared entry point", 1, Regex("""source\.open\(\)""").findAll(screen).count())
    }
}
