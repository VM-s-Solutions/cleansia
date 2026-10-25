package cz.cleansia.partner.features.media

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The rows of the source sheet. A device without a camera gets no camera row rather than one that
 * fails, and the sheet only opens when there is a choice to make.
 */
class PhotoSourceOptionsTest {

    @Test
    fun `the avatar offers the camera and the gallery`() {
        assertEquals(listOf(PhotoSource.Camera, PhotoSource.Gallery), photoSourceOptions(hasCamera = true, offersFile = false))
    }

    @Test
    fun `without a camera the avatar has the gallery alone, which opens without a sheet`() {
        assertEquals(listOf(PhotoSource.Gallery), photoSourceOptions(hasCamera = false, offersFile = false))
    }

    @Test
    fun `a document can be photographed, picked from the gallery or chosen as a file`() {
        assertEquals(
            listOf(PhotoSource.Camera, PhotoSource.Gallery, PhotoSource.File),
            photoSourceOptions(hasCamera = true, offersFile = true),
        )
    }

    @Test
    fun `without a camera a document still offers the gallery and a file`() {
        assertEquals(listOf(PhotoSource.Gallery, PhotoSource.File), photoSourceOptions(hasCamera = false, offersFile = true))
    }
}
