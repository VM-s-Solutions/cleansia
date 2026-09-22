package cz.cleansia.customer.features.disputes

sealed interface EvidenceUploadState {
    data object Pending : EvidenceUploadState
    data object Uploading : EvidenceUploadState
    data object Uploaded : EvidenceUploadState
    data object Failed : EvidenceUploadState
}

/**
 * A file the customer attached before the dispute exists. Held in memory until submit, when it is
 * uploaded to the dispute the create call just returned.
 *
 * A plain class, not a `data class`: a generated `equals` over [bytes] would compare identity.
 */
class PickedEvidence(
    val key: String,
    val fileName: String,
    val mimeType: String,
    val bytes: ByteArray,
    val upload: EvidenceUploadState = EvidenceUploadState.Pending,
) {
    val isPdf: Boolean get() = mimeType.equals("application/pdf", ignoreCase = true)

    fun withUpload(upload: EvidenceUploadState): PickedEvidence =
        PickedEvidence(key, fileName, mimeType, bytes, upload)
}
