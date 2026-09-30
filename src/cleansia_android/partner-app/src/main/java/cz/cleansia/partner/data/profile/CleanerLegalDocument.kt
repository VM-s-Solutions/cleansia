package cz.cleansia.partner.data.profile

import cz.cleansia.core.network.required
import cz.cleansia.partner.api.model.CleanerLegalDocumentDto
import cz.cleansia.partner.api.model.LegalDocumentType

/**
 * One contract document a cleaner works under — the framework contract, the self-billing agreement,
 * the data processing agreement — as it is in force for their market: the text row the acceptance
 * echoes, and whether its current version is the one they accepted.
 */
data class CleanerLegalDocument(
    val type: LegalDocumentType,
    val legalDocumentTextId: String,
    val version: String,
    val title: String,
    val contentHtml: String,
    val isAccepted: Boolean,
    val acceptedVersion: String?,
    val acceptedAt: String?,
)

/**
 * Refuses rather than defaults: the text id is what the acceptance echoes, the HTML is what is
 * accepted, and [CleanerLegalDocument.isAccepted] decides whether the cleaner may be approved and take
 * work — a `false` made up from a null asks again for an acceptance already given, a `true` hides one
 * still owed. What they last accepted stays nullable: a cleaner who never accepted has neither.
 */
internal fun CleanerLegalDocumentDto.toDomain(): CleanerLegalDocument = CleanerLegalDocument(
    type = type.required("type"),
    legalDocumentTextId = legalDocumentTextId.required("legalDocumentTextId"),
    version = version.required("version"),
    title = title.required("title"),
    contentHtml = contentHtml.required("contentHtml"),
    isAccepted = isAccepted.required("isAccepted"),
    acceptedVersion = acceptedVersion,
    acceptedAt = acceptedAt,
)
