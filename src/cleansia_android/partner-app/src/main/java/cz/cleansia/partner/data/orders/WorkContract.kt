package cz.cleansia.partner.data.orders

import cz.cleansia.core.network.required
import cz.cleansia.partner.api.model.WorkContractAcceptanceDetails
import cz.cleansia.partner.api.model.WorkContractDto
import cz.cleansia.partner.api.model.WorkContractFacts
import cz.cleansia.partner.api.model.WorkContractFactsLine

/**
 * A contract for work rendered for one job: the text row the take echoes, the facts the acceptance
 * binds, and — on a read of an accepted contract — the acceptance behind it. A preview carries no
 * [acceptance]; a read carries the facts as they were stored, never the live order.
 */
data class WorkContract(
    val legalDocumentTextId: String,
    val version: String,
    val language: String?,
    val title: String?,
    val contentHtml: String,
    val facts: WorkContractJobFacts,
    val acceptance: WorkContractAcceptanceFacts?,
)

data class WorkContractJobFacts(
    val orderNumber: String?,
    val cleaningDateTimeUtc: String,
    val estimatedMinutes: Int,
    val totalPrice: Double,
    val currencyCode: String?,
    val locationApproximate: String?,
    val rooms: Int,
    val bathrooms: Int,
    val services: List<String>,
    val packages: List<String>,
    val extraSlugs: List<String>,
)

data class WorkContractAcceptanceFacts(
    val acceptedOn: String,
    val documentVersion: String,
    val acceptedLanguage: String?,
)

/**
 * Refuses rather than defaults: the text id is what the take echoes, the HTML is what is accepted,
 * and the price, window and scope are what the acceptance binds — a null in any of them is a broken
 * wire, and a sheet that showed "0 Kč" over an empty text would record an acceptance of nothing.
 * The labels (number, currency code, location) stay nullable and render as absent.
 */
internal fun WorkContractDto.toDomain(): WorkContract = WorkContract(
    legalDocumentTextId = legalDocumentTextId.required("legalDocumentTextId"),
    version = version.required("version"),
    language = language,
    title = title,
    contentHtml = contentHtml.required("contentHtml"),
    facts = facts.required("facts").toDomain(),
    acceptance = acceptance?.toDomain(),
)

private fun WorkContractFacts.toDomain(): WorkContractJobFacts = WorkContractJobFacts(
    orderNumber = orderNumber,
    cleaningDateTimeUtc = cleaningDateTimeUtc.required("facts.cleaningDateTimeUtc"),
    estimatedMinutes = estimatedMinutes.required("facts.estimatedMinutes"),
    totalPrice = totalPrice.required("facts.totalPrice"),
    currencyCode = currencyCode,
    locationApproximate = locationApproximate,
    rooms = rooms.required("facts.rooms"),
    bathrooms = bathrooms.required("facts.bathrooms"),
    services = services.orEmpty().names(),
    packages = packages.orEmpty().names(),
    extraSlugs = extraSlugs.orEmpty(),
)

private fun List<WorkContractFactsLine>.names(): List<String> = mapNotNull { it.name?.takeIf(String::isNotBlank) }

private fun WorkContractAcceptanceDetails.toDomain(): WorkContractAcceptanceFacts = WorkContractAcceptanceFacts(
    acceptedOn = acceptedOn.required("acceptance.acceptedOn"),
    documentVersion = documentVersion.required("acceptance.documentVersion"),
    acceptedLanguage = acceptedLanguage,
)
