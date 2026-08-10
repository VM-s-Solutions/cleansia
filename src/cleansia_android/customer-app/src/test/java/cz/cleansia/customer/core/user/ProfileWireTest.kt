package cz.cleansia.customer.core.user

import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import cz.cleansia.customer.api.model.MyProfileDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * `MyProfileDto` is the only customer wire this ticket touches that is decoded straight into the app
 * type with no `Response` wrapper in between, so the payload is decoded here rather than served over a
 * socket; the field-name contract is pinned the same way. It declares no `required` array, so the
 * generator types every property optional-with-null regardless of `nullable: false` — and
 * `totalSavings` defaulting to zero told a customer who had saved 4 820 Kč that they had saved
 * nothing, on the one screen that exists to show it.
 */
class ProfileWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private fun profile(body: String): CurrentUser? =
        json.decodeFromString(MyProfileDto.serializer(), body).toCurrentUser(USER_ID)

    private fun loaded(body: String): CurrentUser {
        val user = profile(body)
        assertNotNull("expected the captured payload to map", user)
        return user!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun profileDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(PROFILE_SPEC_PROPERTIES, serialNames(MyProfileDto.serializer().descriptor))
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun theSavingsFigureArrivesWithItsLiteralValue() {
        val user = loaded(CAPTURED_PROFILE)

        assertEquals(4820.50, user.totalSavings, 0.0)
        assertEquals(17, user.totalBookings)
    }

    @Test
    fun aMissingSavingsFigureRefusesTheProfileRatherThanClaimingNothingWasSaved() {
        assertNull(profile(withoutKey(CAPTURED_PROFILE, "totalSavings")))
    }

    @Test
    fun aMissingBookingCountRefusesTheProfileRatherThanErasingTheHistory() {
        assertNull(profile(withoutKey(CAPTURED_PROFILE, "totalBookings")))
    }

    @Test
    fun anExplicitNullSavingsFigureRefusesTheProfileToo() {
        assertNull(profile(withKey(CAPTURED_PROFILE, "totalSavings", JsonNull)))
    }

    // --- rule 2: booleans follow the money rule ---------------------------------
    //
    // `isEmailConfirmed` is `nullable: false` but no field on [CurrentUser] carries it; the rule is
    // exercised on the quote, order and membership wires.

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * The profile carries no id of its own — the caller passes the one it read from the JWT — so there
     * is nothing here to synthesize and nothing to drop.
     */
    @Test
    fun theUserIdIsTheCallersAndNeverInventedFromTheBody() {
        assertEquals(USER_ID, loaded(CAPTURED_PROFILE).id)
    }

    // --- rule 4: collections do default -----------------------------------------
    //
    // `MyProfileDto` carries no collection.

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `savingsCurrencyCode` is `nullable: true`: a customer who has saved nothing on a fresh account
     * has no currency to render it in. Blank name and email are the existing skeleton treatment and
     * are left alone — they are `nullable: true` too, and the screen renders their absence.
     */
    @Test
    fun aProfileWithNoCurrencyOrPhotoStillLoads() {
        val user = loaded(
            PROFILE_NULLABLE_FIELDS.fold(CAPTURED_PROFILE) { body, key -> withoutKey(body, key) },
        )

        assertNull(user.savingsCurrencyCode)
        assertNull(user.phoneNumber)
        assertNull(user.birthDate)
        assertEquals(4820.50, user.totalSavings, 0.0)
    }

    @Test
    fun aBlankAvatarNameIsTreatedAsNoAvatarRatherThanAnEmptyUrl() {
        val user = loaded(
            mutating(CAPTURED_PROFILE) { root ->
                root + ("profilePhoto" to (root["profilePhoto"]!!.jsonObject + ("fileName" to jsonOf(""))))
            },
        )

        assertNull(user.avatarFileName)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun jsonOf(value: String): JsonElement = kotlinx.serialization.json.JsonPrimitive(value)

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        const val USER_ID = "user-7"

        /** Every member non-zero and non-default. */
        val CAPTURED_PROFILE = """
            {
              "email": "ada@example.com",
              "firstName": "Ada",
              "lastName": "Lovelace",
              "phoneNumber": "+420600000000",
              "birthDate": "1990-12-10",
              "preferredLanguageCode": "cs",
              "preferredLanguageName": "Cestina",
              "profile": { "type": "Profile", "name": "Customer", "value": 1 },
              "authenticationType": { "type": "AuthenticationType", "name": "Password", "value": 1 },
              "isEmailConfirmed": true,
              "profilePhoto": {
                "fileName": "avatar.jpg",
                "blobUrl": "https://blob.example/avatar.jpg?sig=abc"
              },
              "memberSince": "2025-02-03T10:00:00Z",
              "totalBookings": 17,
              "totalSavings": 4820.50,
              "savingsCurrencyCode": "CZK"
            }
        """.trimIndent()

        val PROFILE_SPEC_PROPERTIES = setOf(
            "email",
            "firstName",
            "lastName",
            "phoneNumber",
            "birthDate",
            "preferredLanguageCode",
            "preferredLanguageName",
            "profile",
            "authenticationType",
            "isEmailConfirmed",
            "profilePhoto",
            "memberSince",
            "totalBookings",
            "totalSavings",
            "savingsCurrencyCode",
        )

        val PROFILE_NULLABLE_FIELDS = listOf(
            "phoneNumber",
            "birthDate",
            "preferredLanguageCode",
            "preferredLanguageName",
            "savingsCurrencyCode",
            "profilePhoto",
        )
    }
}
