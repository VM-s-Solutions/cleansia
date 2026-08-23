package cz.cleansia.customer.core.disputes

import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * What actually goes on the wire when a customer attaches evidence to a dispute.
 *
 * **Why this test is over a socket and not a mock.** Every existing test on this path stubs the
 * boundary — `DisputeRepositoryTest` stubs `api.uploadEvidence`, `DisputeDetailViewModelTest` stubs the
 * repository — so all of them stayed green while the feature returned 400 on every single attempt, and
 * would stay green if the method body were deleted. The defect lived in the encoding Retrofit chose,
 * which no mock can observe. Only bytes can.
 *
 * **The defect.** The generated client types the id part as `String`, and the only converter factory
 * this app registers is kotlinx for `application/json`. Retrofit therefore serialised the id as JSON:
 * the part went out as `application/json` carrying `"01H8…"` **with quotes**, the server bound
 * `[FromForm] string disputeId` including them, and no dispute ever matched.
 */
class DisputeEvidenceUploadWireTest {

    private val json = Json { ignoreUnknownKeys = true }

    @Test
    fun `the dispute id part is sent as plain text, unquoted`() = runTest {
        val server = MockWebServer()
        server.start()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setHeader("Content-Type", "application/json")
                .setBody("""{"id":"ev-1","fileName":"a.jpg","blobName":"b","contentType":"image/jpeg","sizeBytes":3}"""),
        )

        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(DisputeEvidenceUploadApi::class.java)

        api.uploadEvidence(
            disputeId = "01H8XYZ".toRequestBody("text/plain".toMediaTypeOrNull()),
            file = MultipartBody.Part.createFormData(
                "file",
                "a.jpg",
                byteArrayOf(1, 2, 3).toRequestBody("image/jpeg".toMediaTypeOrNull()),
            ),
        )

        val body = server.takeRequest().body.readUtf8()
        server.shutdown()

        // The id, bare. A JSON-encoded part would carry `"01H8XYZ"` instead — which is what shipped.
        assertTrue("the disputeId part is missing entirely:\n$body", body.contains("01H8XYZ"))
        assertFalse(
            "the disputeId part is JSON-quoted — the server binds the quotes and the id matches nothing:\n$body",
            body.contains("\"01H8XYZ\""),
        )
        assertFalse(
            "the disputeId part was sent as application/json rather than plain text:\n$body",
            body.substringBefore("--", "").contains("application/json"),
        )

        // Both parts must survive, named as the server binds them.
        assertTrue("the disputeId part name is wrong:\n$body", body.contains("name=\"disputeId\""))
        assertTrue("the file part name is wrong:\n$body", body.contains("name=\"file\""))
    }
}
