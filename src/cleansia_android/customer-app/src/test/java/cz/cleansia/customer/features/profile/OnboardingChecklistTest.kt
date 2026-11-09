package cz.cleansia.customer.features.profile

import cz.cleansia.customer.core.user.CurrentUser
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class OnboardingChecklistTest {

    private fun user(firstName: String = "Jana", lastName: String = "Dvořáková", email: String = "jana.dvorakova@gmail.com") =
        CurrentUser(
            id = "user-1",
            email = email,
            firstName = firstName,
            lastName = lastName,
            phoneNumber = null,
            birthDate = null,
            preferredLanguageCode = "cs",
        )

    @Test
    fun `a phone is done only when its country allows its length`() {
        assertFalse(OnboardingChecklist.of(user(), "+42077712").phone)
        assertTrue(OnboardingChecklist.of(user(), "+420777123456").phone)
    }

    @Test
    fun `the count and readiness come from one value`() {
        for (name in listOf(false, true)) for (email in listOf(false, true)) for (phone in listOf(false, true)) {
            val checklist = OnboardingChecklist.of(
                user(firstName = if (name) "Jana" else "", email = if (email) "jana@gmail.com" else ""),
                if (phone) "+420777123456" else "",
            )
            val case = "name $name, e-mail $email, phone $phone"
            assertEquals(case, listOf(name, email, phone).count { it }, checklist.doneCount)
            assertEquals(case, name && email && phone, checklist.isReady)
        }
    }

    @Test
    fun `a blank name part is not done`() {
        assertFalse(OnboardingChecklist.of(user(firstName = " "), "").name)
        assertFalse(OnboardingChecklist.of(user(lastName = ""), "").name)
    }

    @Test
    fun `only Apple's relay reads hidden`() {
        listOf("x7k2p9q8r4@privaterelay.appleid.com", "X@PrivateRelay.AppleID.com").forEach {
            assertTrue(it, isAppleRelayEmail(it))
        }
        listOf("jana@gmail.com", "privaterelay.appleid.com@gmail.com", "a@private.icloud.com").forEach {
            assertFalse(it, isAppleRelayEmail(it))
        }
    }

    @Test
    fun `the e-mail splits at its last at sign`() {
        assertEquals(
            "jana.dvorakova.vinohrady.praha" to "@gmail.com",
            emailParts("jana.dvorakova.vinohrady.praha@gmail.com"),
        )
        assertEquals("a@b" to "@c.cz", emailParts("a@b@c.cz"))
        assertEquals("noat" to "", emailParts("noat"))
    }
}
