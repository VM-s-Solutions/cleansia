package cz.cleansia.partner.data.profile

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.api.model.GetMyDocumentsMyDocumentDto
import cz.cleansia.partner.api.model.MyPayoutDetails
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * This repository has no mapper: it hands the generated DTOs to the UI as they arrive, so the
 * `@SerialName` set **is** the whole contract and a renamed field lands as a silent null on a screen
 * that gates the cleaner's access to work. `jobRadiusKm` and the three registration flags are the
 * sharp ones — the flags decide whether the Orders tab unlocks at all.
 */
class EmployeeProfileWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = ProfileRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(EmployeeApi::class.java),
        json,
    )

    private suspend fun <T> serving(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (ProfileRepositoryImpl) -> ApiResult<T>,
    ): ApiResult<T> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(code)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            call(repo(server)).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private fun <T> loaded(result: ApiResult<T>): T {
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun employeeDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(EMPLOYEE_SPEC_PROPERTIES, serialNames(EmployeeItem.serializer().descriptor))
    }

    @Test
    fun registrationStatusSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(
            REGISTRATION_SPEC_PROPERTIES,
            serialNames(RegistrationCompletionStatus.serializer().descriptor),
        )
    }

    @Test
    fun payoutDetailsSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(PAYOUT_SPEC_PROPERTIES, serialNames(MyPayoutDetails.serializer().descriptor))
    }

    @Test
    fun documentSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(
            DOCUMENT_SPEC_PROPERTIES,
            serialNames(GetMyDocumentsMyDocumentDto.serializer().descriptor),
        )
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_EMPLOYEE, onRequest = { path = it.path }) { it.getCurrentEmployee() }
        assertEquals("/api/Employee/GetCurrentEmployee", path)

        serving(CAPTURED_REGISTRATION, onRequest = { path = it.path }) { it.getRegistrationStatus() }
        assertEquals("/api/Employee/CheckCurrentEmployee", path)

        serving(CAPTURED_PAYOUT, onRequest = { path = it.path }) { it.getPayoutDetails() }
        assertEquals("/api/Employee/GetMyPayoutDetails", path)

        serving(CAPTURED_DOCUMENTS, onRequest = { path = it.path }) { it.getMyDocuments() }
        assertEquals("/api/Employee/GetMyDocuments", path)
    }

    // --- every field the screens read arrives with its literal value ---------------

    @Test
    fun everyEmployeeFieldArrivesWithItsLiteralValue() = runTest {
        val employee = loaded(serving(CAPTURED_EMPLOYEE) { it.getCurrentEmployee() })

        assertEquals("emp-7", employee.id)
        assertEquals("Jana", employee.firstName)
        assertEquals("Praha", employee.city)
        assertEquals(25, employee.jobRadiusKm)
        assertEquals(listOf("09:00" to "13:00"), employee.availability?.get("Monday")?.map { it.start to it.end })
    }

    /**
     * `null` is a live value here — it means the country-wide digest, a choice rather than an
     * omission — so a renamed key is indistinguishable from the cleaner having asked for it.
     */
    @Test
    fun aRenamedJobRadiusKeyIsIndistinguishableFromTheCountryWideChoice() = runTest {
        val employee = loaded(
            serving(withoutKey(CAPTURED_EMPLOYEE, "jobRadiusKm")) { it.getCurrentEmployee() },
        )

        assertNull(employee.jobRadiusKm)
    }

    @Test
    fun everyRegistrationFlagArrivesWithItsLiteralValue() = runTest {
        val status = loaded(serving(CAPTURED_REGISTRATION) { it.getRegistrationStatus() })

        assertEquals(true, status.areDocumentsUploaded)
        assertEquals(true, status.hasCompletedProfile)
        assertEquals(true, status.hasSetAvailability)
        assertEquals(listOf("passportId"), status.missingFields)
        assertEquals("not this time", status.rejectionReason)
    }

    @Test
    fun everyPayoutIdentifierArrivesWithItsLiteralValue() = runTest {
        val payout = loaded(serving(CAPTURED_PAYOUT) { it.getPayoutDetails() })

        assertEquals("19", payout?.accountPrefix)
        assertEquals("2000145399", payout?.accountNumber)
        assertEquals("0800", payout?.bankCode)
        assertEquals("CZ6508000000192000145399", payout?.iban)
        assertEquals("GIBACZPX", payout?.swift)
    }

    @Test
    fun everyDocumentFieldArrivesWithItsLiteralValue() = runTest {
        val document = loaded(serving(CAPTURED_DOCUMENTS) { it.getMyDocuments() }).first()

        assertEquals("doc-3", document.documentId)
        assertEquals("passport.pdf", document.fileName)
        assertEquals(2, document.version)
        assertEquals(348_112L, document.fileSizeBytes)
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aCleanerWithNoDocumentsGetsAnEmptyListRatherThanARefusal() = runTest {
        assertEquals(emptyList<GetMyDocumentsMyDocumentDto>(), loaded(serving("{}") { it.getMyDocuments() }))
    }

    // --- the refused body ---------------------------------------------------------

    @Test
    fun aBodylessSuccessIsRefusedRatherThanReadAsABlankEmployee() = runTest {
        val result = serving("", code = 204) { it.getCurrentEmployee() }

        assertTrue("a 2xx with no employee in it is not an employee; got $result", result is ApiResult.Error)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun withoutKey(body: String, key: String): String =
        JsonObject(Json.parseToJsonElement(body).jsonObject.toMutableMap().apply { remove(key) })
            .toString()

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-default, so a forgotten field cannot pass as a mapped one. */
        val CAPTURED_EMPLOYEE = """
            {
              "id": "emp-7",
              "email": "jana@cleansia.cz",
              "firstName": "Jana",
              "lastName": "Novak",
              "phoneNumber": "+420777123456",
              "birthDate": "1990-04-17",
              "street": "Krátká 4",
              "city": "Praha",
              "zipCode": "11000",
              "countryId": "cnt-cz",
              "state": "Praha",
              "nationalityId": "cnt-cz",
              "passportId": "AB1234567",
              "entityType": 2,
              "registrationNumber": "12345678",
              "vatNumber": "CZ12345678",
              "legalEntityName": "Jana Novak s.r.o.",
              "emergencyContactName": "Petr Novak",
              "emergencyContactPhone": "+420777654321",
              "profilePhoto": {
                "fileName": "avatar-9.jpg",
                "base64Content": "AAA=",
                "contentType": "image/jpeg",
                "blobUrl": "https://blob.example/avatar-9.jpg?sig=x"
              },
              "profile": { "type": "Profile", "name": "Employee", "value": 2 },
              "authenticationType": { "type": "AuthenticationType", "name": "Internal", "value": 1 },
              "availability": { "Monday": [ { "start": "09:00", "end": "13:00" } ] },
              "jobRadiusKm": 25
            }
        """.trimIndent()

        val CAPTURED_REGISTRATION = """
            {
              "areDocumentsUploaded": true,
              "hasCompletedProfile": true,
              "hasSetAvailability": true,
              "missingFields": ["passportId"],
              "contractStatus": 4,
              "rejectionReason": "not this time"
            }
        """.trimIndent()

        val CAPTURED_PAYOUT = """
            {
              "scheme": 1,
              "status": 2,
              "bankCountryId": "cnt-cz",
              "accountPrefix": "19",
              "accountNumber": "2000145399",
              "bankCode": "0800",
              "iban": "CZ6508000000192000145399",
              "swift": "GIBACZPX",
              "bankName": "Česká spořitelna",
              "holderName": "Jana Novak",
              "confirmedAt": "2026-07-02T10:00:00Z"
            }
        """.trimIndent()

        val CAPTURED_DOCUMENTS = """
            {
              "documents": [
                {
                  "documentId": "doc-3",
                  "fileName": "passport.pdf",
                  "blobUrl": "https://blob.example/passport.pdf?sig=x",
                  "documentType": 1,
                  "status": 2,
                  "version": 2,
                  "fileSizeBytes": 348112,
                  "contentType": "application/pdf",
                  "uploadedAt": "2026-07-02T10:00:00Z",
                  "description": "Passport, page 1",
                  "reviewNotes": "Legible"
                }
              ]
            }
        """.trimIndent()

        val EMPLOYEE_SPEC_PROPERTIES = setOf(
            "id",
            "email",
            "firstName",
            "lastName",
            "phoneNumber",
            "birthDate",
            "street",
            "city",
            "zipCode",
            "countryId",
            "state",
            "nationalityId",
            "passportId",
            "entityType",
            "registrationNumber",
            "vatNumber",
            "legalEntityName",
            "emergencyContactName",
            "emergencyContactPhone",
            "profilePhoto",
            "profile",
            "authenticationType",
            "availability",
            "jobRadiusKm",
        )

        val REGISTRATION_SPEC_PROPERTIES = setOf(
            "areDocumentsUploaded",
            "hasCompletedProfile",
            "hasSetAvailability",
            "missingFields",
            "contractStatus",
            "rejectionReason",
        )

        val PAYOUT_SPEC_PROPERTIES = setOf(
            "scheme",
            "status",
            "bankCountryId",
            "accountPrefix",
            "accountNumber",
            "bankCode",
            "iban",
            "swift",
            "bankName",
            "holderName",
            "confirmedAt",
        )

        val DOCUMENT_SPEC_PROPERTIES = setOf(
            "documentId",
            "fileName",
            "blobUrl",
            "documentType",
            "status",
            "version",
            "fileSizeBytes",
            "contentType",
            "uploadedAt",
            "description",
            "reviewNotes",
        )
    }
}
