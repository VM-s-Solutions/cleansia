package cz.cleansia.core.consent

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.rules.TestName

/**
 * The parking rules for a signup tick that has no session to be recorded against yet.
 *
 * Everything here protects against ONE outcome — a consent record the user never gave —
 * so each rule is pinned separately rather than folded into a happy path:
 *  - an unticked box parks nothing;
 *  - a tick delivers only into a session the server named with the same address;
 *  - a type the account has already answered, granted OR withdrawn, is dropped rather
 *    than re-granted;
 *  - anything that did not land stays parked for the next session.
 *
 * The exact commands that reach the wire are pinned per app, over each app's real
 * generated `GdprApi` — see `SignupConsentFlowTest` in customer-app and partner-app.
 */
class SignupConsentRepositoryTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    @get:Rule
    val testName = TestName()

    private lateinit var client: SignupConsentClient
    private lateinit var dataStore: DataStore<Preferences>

    private val email = "Ada@Example.com"
    private val sessionEmail = "ada@example.com"

    @Before
    fun setUp() {
        client = mockk()
        coEvery { client.answeredTypes() } returns emptySet()
        coEvery { client.grant(any()) } returns ConsentGrantOutcome.Recorded

        dataStore = PreferenceDataStoreFactory.create(
            produceFile = { tempFolder.newFile("signup_consent_${testName.methodName}.preferences_pb") },
        )
    }

    private fun newRepository() = SignupConsentRepository(SignupConsentStore(dataStore), client)

    @Test
    fun deliverFor_givenTickedBox_grantsBothDocumentsAndNothingElse() = runTest {
        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)

        repo.deliverFor(sessionEmail)

        coVerify(exactly = 1) { client.grant(SignupConsentType.TermsOfService) }
        coVerify(exactly = 1) { client.grant(SignupConsentType.PrivacyPolicy) }
        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun deliverFor_givenUntickedBox_grantsNothing() = runTest {
        val repo = newRepository()
        repo.recordSignupTick(email, accepted = false)

        repo.deliverFor(sessionEmail)

        coVerify(exactly = 0) { client.answeredTypes() }
        coVerify(exactly = 0) { client.grant(any()) }
    }

    @Test
    fun deliverFor_withNothingParked_grantsNothing() = runTest {
        newRepository().deliverFor(sessionEmail)

        coVerify(exactly = 0) { client.answeredTypes() }
        coVerify(exactly = 0) { client.grant(any()) }
    }

    @Test
    fun deliverFor_givenADifferentAccountsSession_grantsNothingAndKeepsTheTick() = runTest {
        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)

        repo.deliverFor("someone.else@example.com")
        coVerify(exactly = 0) { client.grant(any()) }

        repo.deliverFor(sessionEmail)
        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun deliverFor_withoutAServerSuppliedEmail_grantsNothingAndKeepsTheTick() = runTest {
        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)

        repo.deliverFor(null)
        coVerify(exactly = 0) { client.grant(any()) }

        repo.deliverFor(sessionEmail)
        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun deliverFor_whenTheAccountAlreadyAnsweredAType_doesNotReGrantIt() = runTest {
        coEvery { client.answeredTypes() } returns setOf(SignupConsentType.TermsOfService)

        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)
        repo.deliverFor(sessionEmail)

        coVerify(exactly = 0) { client.grant(SignupConsentType.TermsOfService) }
        coVerify(exactly = 1) { client.grant(SignupConsentType.PrivacyPolicy) }
    }

    @Test
    fun deliverFor_whenTheAccountAlreadyAnsweredEverything_settlesTheTick() = runTest {
        coEvery { client.answeredTypes() } returns SIGNUP_TICK_CONSENTS.toSet()

        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)
        repo.deliverFor(sessionEmail)

        coEvery { client.answeredTypes() } returns emptySet()
        repo.deliverFor(sessionEmail)

        coVerify(exactly = 0) { client.grant(any()) }
    }

    @Test
    fun deliverFor_whenTheConsentReadFails_grantsNothingAndKeepsTheTick() = runTest {
        coEvery { client.answeredTypes() } returns null

        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)
        repo.deliverFor(sessionEmail)
        coVerify(exactly = 0) { client.grant(any()) }

        coEvery { client.answeredTypes() } returns emptySet()
        repo.deliverFor(sessionEmail)
        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun deliverFor_whenAGrantFails_keepsThatTypeParkedForTheNextSession() = runTest {
        coEvery { client.grant(SignupConsentType.TermsOfService) } returns ConsentGrantOutcome.Failed

        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)
        repo.deliverFor(sessionEmail)

        coEvery { client.grant(SignupConsentType.TermsOfService) } returns ConsentGrantOutcome.Recorded
        repo.deliverFor(sessionEmail)

        coVerify(exactly = 2) { client.grant(SignupConsentType.TermsOfService) }
        // The one that landed is settled, so it is not sent twice.
        coVerify(exactly = 1) { client.grant(SignupConsentType.PrivacyPolicy) }
    }

    @Test
    fun deliverFor_whenTheBackendRefusesADuplicate_countsAsDelivered() = runTest {
        coEvery { client.grant(any()) } returns ConsentGrantOutcome.AlreadyOnFile

        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)
        repo.deliverFor(sessionEmail)
        repo.deliverFor(sessionEmail)

        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun deliverFor_whenTheClientThrows_doesNotPropagate() = runTest {
        coEvery { client.answeredTypes() } throws java.io.IOException("network down")

        val repo = newRepository()
        repo.recordSignupTick(email, accepted = true)
        repo.deliverFor(sessionEmail)

        coEvery { client.answeredTypes() } returns emptySet()
        repo.deliverFor(sessionEmail)
        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun recordSignupTick_normalizesTheAddressItParksAgainst() = runTest {
        val repo = newRepository()
        repo.recordSignupTick("  ADA@example.COM ", accepted = true)

        repo.deliverFor("ada@example.com")

        coVerify(exactly = 2) { client.grant(any()) }
    }

    @Test
    fun signupTick_isExactlyTermsOfServiceAndPrivacyPolicy() {
        assertEquals(
            listOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy),
            SIGNUP_TICK_CONSENTS,
        )
    }

    @Test
    fun consentTypes_carryTheBackendEnumValues() {
        assertEquals(0, SignupConsentType.TermsOfService.wireValue)
        assertEquals(1, SignupConsentType.PrivacyPolicy.wireValue)
        assertEquals(2, SignupConsentType.MarketingEmails.wireValue)
        assertEquals(3, SignupConsentType.DataProcessing.wireValue)
        assertEquals(SignupConsentType.PrivacyPolicy, SignupConsentType.fromWireValue(1))
        assertEquals(null, SignupConsentType.fromWireValue(9))
    }
}
