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
    val reward: Double,
    val currencyCode: String?,
    val locationApproximate: String?,
    val rooms: Int,
    val bathrooms: Int,
    val services: List<String>,
    val packages: List<String>,
    val extraSlugs: List<String>,
    /** The id of `services[i]` at index i, so the take sheet can show the row's translated name for it. */
    val serviceIds: List<String> = emptyList(),
    /** The id of `packages[i]` at index i. */
    val packageIds: List<String> = emptyList(),
)

data class WorkContractAcceptanceFacts(
    val acceptedOn: String,
    val documentVersion: String,
    val acceptedLanguage: String?,
)

/**
 * Refuses rather than defaults: the text id is what the take echoes, the HTML is what is accepted,
 * and the reward, window and scope are what the acceptance binds — a null in any of them is a broken
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
    reward = totalPrice.required("facts.totalPrice"),
    currencyCode = currencyCode,
    locationApproximate = locationApproximate,
    rooms = rooms.required("facts.rooms"),
    bathrooms = bathrooms.required("facts.bathrooms"),
    services = services.orEmpty().named().map { it.second },
    packages = packages.orEmpty().named().map { it.second },
    extraSlugs = extraSlugs.orEmpty(),
    serviceIds = services.orEmpty().named().map { it.first },
    packageIds = packages.orEmpty().named().map { it.first },
)

/** The lines with a name, as (id, name): one pass keeps index i of the ids on name i. */
private fun List<WorkContractFactsLine>.named(): List<Pair<String, String>> =
    mapNotNull { line -> line.name?.takeIf(String::isNotBlank)?.let { line.id.orEmpty() to it } }

private fun WorkContractAcceptanceDetails.toDomain(): WorkContractAcceptanceFacts = WorkContractAcceptanceFacts(
    acceptedOn = acceptedOn.required("acceptance.acceptedOn"),
    documentVersion = documentVersion.required("acceptance.documentVersion"),
    acceptedLanguage = acceptedLanguage,
)
