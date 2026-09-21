package cz.cleansia.customer.features.disputes

object DisputeFormConstants {
    const val DESCRIPTION_MIN_LENGTH = 10
    const val DESCRIPTION_MAX_LENGTH = 2000

    /**
     * Mirrors the backend evidence validator, so a doomed upload never reaches the network — and if
     * the whitelist grows there it must grow here. -> /flows/cancellation-refund-dispute
     */
    const val EVIDENCE_MAX_BYTES: Int = 10 * 1024 * 1024
    val EVIDENCE_ALLOWED_MIME_TYPES: Set<String> = setOf(
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "application/pdf",
    )
}
