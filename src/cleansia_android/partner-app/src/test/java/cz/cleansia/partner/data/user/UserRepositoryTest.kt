package cz.cleansia.partner.data.user

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.UserApi
import cz.cleansia.partner.api.model.MyProfileDto
import cz.cleansia.partner.api.model.UpdateCurrentUserCommand
import cz.cleansia.partner.api.model.UpdateCurrentUserResponse
import io.mockk.coEvery
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Response

/**
 * `UpdateCurrentUser` replaces first and last name outright, so anything that saves one field has to
 * put the rest of the profile back on the wire. The command's `PhoneNumber` is a non-nullable
 * reference type on a host with nullable reference types enabled, so an OMITTED member is refused by
 * the model binder before the handler runs — a blank string is what means "nothing to say" there.
 */
class UserRepositoryTest {

    private val userApi: UserApi = mockk()
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private fun repo() = UserRepositoryImpl(userApi, json)

    @Test
    fun `getCurrentUser maps the fields the language replay needs`() = runTest {
        coEvery { userApi.userGetCurrentUser(any()) } returns Response.success(
            MyProfileDto(
                email = "ondrej@example.com",
                firstName = "Ondrej",
                lastName = "Novak",
                phoneNumber = "+420777111222",
                birthDate = "1982-09-04",
                preferredLanguageCode = "en",
            ),
        )

        val user = (repo().getCurrentUser() as ApiResult.Success).data

        assertEquals("ondrej@example.com", user.email)
        assertEquals("Ondrej", user.firstName)
        assertEquals("Novak", user.lastName)
        assertEquals("+420777111222", user.phoneNumber)
        assertEquals("1982-09-04", user.birthDate)
        assertEquals("en", user.preferredLanguageCode)
    }

    @Test
    fun `updateCurrentUser puts the whole replay on the wire`() = runTest {
        val command = slot<UpdateCurrentUserCommand>()
        coEvery { userApi.userUpdateCurrentUser(capture(command)) } returns
            Response.success(UpdateCurrentUserResponse(id = "user-1"))

        repo().updateCurrentUser(
            firstName = "Ondrej",
            lastName = "Novak",
            phoneNumber = "+420777111222",
            birthDate = "1982-09-04",
            languageCode = "uk",
        )

        assertEquals("Ondrej", command.captured.firstName)
        assertEquals("Novak", command.captured.lastName)
        assertEquals("+420777111222", command.captured.phoneNumber)
        assertEquals("1982-09-04", command.captured.birthDate)
        assertEquals("uk", command.captured.languageCode)
    }

    /** The avatar is a three-way choice: a language save says nothing about it and must not remove it. */
    @Test
    fun `updateCurrentUser never asks for the avatar to be removed`() = runTest {
        val command = slot<UpdateCurrentUserCommand>()
        coEvery { userApi.userUpdateCurrentUser(capture(command)) } returns
            Response.success(UpdateCurrentUserResponse(id = "user-1"))

        repo().updateCurrentUser("Ondrej", "Novak", "", null, "uk")

        assertNull(command.captured.photo)
        assertEquals(false, command.captured.removePhoto)
    }

    /**
     * `explicitNulls = false` drops nulls from the body, so mapping a blank phone to null — which is
     * what the customer repository does — would omit the member and earn a model-binder 400.
     */
    @Test
    fun `a blank phone reaches the wire as a blank rather than an omitted member`() = runTest {
        val command = slot<UpdateCurrentUserCommand>()
        coEvery { userApi.userUpdateCurrentUser(capture(command)) } returns
            Response.success(UpdateCurrentUserResponse(id = "user-1"))

        repo().updateCurrentUser("Ondrej", "Novak", "", null, "uk")

        assertEquals("", command.captured.phoneNumber)
    }
}
