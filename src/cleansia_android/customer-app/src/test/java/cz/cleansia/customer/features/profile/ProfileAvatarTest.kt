package cz.cleansia.customer.features.profile

import android.net.Uri
import cz.cleansia.core.media.Base64Image
import cz.cleansia.customer.core.user.CurrentUser
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The saved profile and the pending edit disagree for as long as an edit is
 * unsaved, and which one wins is what the user sees. Pure, so it is pinned here
 * rather than through a Compose test the customer app cannot run.
 */
class ProfileAvatarTest {

    private val pickedUri = mockk<Uri>()

    private val userWithPhoto = CurrentUser(
        id = "user-1",
        email = "a@b.com",
        firstName = "Ann",
        lastName = "Brown",
        phoneNumber = null,
        birthDate = null,
        preferredLanguageCode = "en",
        avatarFileName = "9f1c-guid",
        avatarUrl = "https://blob.example/9f1c-guid?sig=abc",
    )

    @Test
    fun `a saved photo renders remotely, keyed on the blob name and not the SAS`() {
        val photo = avatarPhotoFor(userWithPhoto, AvatarDraft.Unchanged) as AvatarPhoto.Remote

        assertEquals("https://blob.example/9f1c-guid?sig=abc", photo.url)
        assertEquals("9f1c-guid", photo.fileName)
    }

    @Test
    fun `a user with no photo renders nothing, so the initials show`() {
        assertNull(avatarPhotoFor(userWithPhoto.copy(avatarUrl = null), AvatarDraft.Unchanged))
    }

    @Test
    fun `a signed-out or still-loading profile renders nothing`() {
        assertNull(avatarPhotoFor(null, AvatarDraft.Unchanged))
    }

    @Test
    fun `a pending pick previews locally, ahead of the upload`() {
        val draft = AvatarDraft.Picked(
            previewUri = pickedUri,
            image = Base64Image(base64 = "encoded", contentType = "image/jpeg", fileName = "me.jpg"),
        )

        val photo = avatarPhotoFor(userWithPhoto, draft) as AvatarPhoto.Local

        assertEquals(pickedUri, photo.uri)
    }

    @Test
    fun `a pending removal shows the initials before the save reaches the server`() {
        assertNull(avatarPhotoFor(userWithPhoto, AvatarDraft.Removed))
    }
}
