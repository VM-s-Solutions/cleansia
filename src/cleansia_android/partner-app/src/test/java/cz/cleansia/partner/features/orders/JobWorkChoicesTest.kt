package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** The work step's choices: the record buttons' tones, and when the cleaner moves. */
class JobWorkChoicesTest {

    @Test
    fun `Note is the tonal primary and Issue the error tone, in that order`() {
        assertEquals(listOf(RecordKind.Note, RecordKind.Issue), RecordKind.entries.toList())
        assertEquals(ChipTone.Primary, RecordKind.Note.tone)
        assertEquals(ChipTone.Error, RecordKind.Issue.tone)
        assertEquals(R.string.add_note, RecordKind.Note.label)
        assertEquals(R.string.report_issue, RecordKind.Issue.label)
    }

    @Test
    fun `the cleaning mascot plays only on the cleaning step`() {
        assertTrue(cleaningAnimated(RunStep.Clean))
        RunStep.entries.filter { it != RunStep.Clean }.forEach { assertFalse(it.name, cleaningAnimated(it)) }
    }
}
