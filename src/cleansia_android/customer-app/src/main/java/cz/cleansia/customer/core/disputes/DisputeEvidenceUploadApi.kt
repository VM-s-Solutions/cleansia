package cz.cleansia.customer.core.disputes

import cz.cleansia.customer.api.model.UploadDisputeEvidenceResponse
import okhttp3.MultipartBody
import okhttp3.RequestBody
import retrofit2.Response
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.Part

/**
 * The evidence upload, hand-written because the generated signature cannot express it correctly.
 *
 * **The defect this exists to fix.** openapi-generator types the text part as `@Part("disputeId")
 * String?`. Retrofit asks its converter factories to turn that `String` into a body, and the only
 * factory this app registers is kotlinx-serialization for `application/json`
 * (`AuthModule.kt:141`, `:285`) — Retrofit's built-in converters claim `RequestBody` and nothing else.
 * So the part went out as `Content-Type: application/json` carrying `"01H8…"` **with the quotes**,
 * the server bound `[FromForm] string disputeId` verbatim including them, and the id matched no
 * dispute. Every upload returned 400 `dispute.dispute_not_found`. It had never worked.
 *
 * **Why a local interface and not `ScalarsConverterFactory`.** Registering scalars globally would
 * change how EVERY `String` parameter in the app is encoded, to fix one part on one endpoint. This is
 * the only multipart endpoint in the tree, so the narrow fix is the honest one: take a
 * [RequestBody] the caller has already given the right media type, and let Retrofit pass it through
 * untouched.
 */
interface DisputeEvidenceUploadApi {
    @Multipart
    @POST("api/Dispute/UploadEvidence")
    suspend fun uploadEvidence(
        @Part("disputeId") disputeId: RequestBody,
        @Part file: MultipartBody.Part,
    ): Response<UploadDisputeEvidenceResponse>
}
