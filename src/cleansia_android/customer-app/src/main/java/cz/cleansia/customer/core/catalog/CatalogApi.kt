package cz.cleansia.customer.core.catalog

import cz.cleansia.customer.api.client.ExtraApi as GenExtraApi
import cz.cleansia.customer.api.client.PackageApi as GenPackageApi
import cz.cleansia.customer.api.client.ServiceApi as GenServiceApi
import cz.cleansia.customer.api.model.CategoryDto as GenCategoryDto
import cz.cleansia.customer.api.model.ExtraListItem as GenExtraListItem
import cz.cleansia.customer.api.model.PackageListItem as GenPackageListItem
import cz.cleansia.customer.api.model.PackageServiceSummary as GenPackageServiceSummary
import cz.cleansia.customer.api.model.ServiceListItem as GenServiceListItem
import cz.cleansia.customer.api.model.Translation as GenTranslation
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the generated catalog clients.
 *
 * **The repository layer keeps strict hand-written DTOs**; this seam calls the all-nullable generated
 * client and fills the defaults, so nullability the server never sends stops at the boundary.
 * -> /mobile-app/api-integration
 */
class CatalogApi(
    private val serviceApi: GenServiceApi,
    private val packageApi: GenPackageApi,
    private val extraApi: GenExtraApi,
) {
    /**
     * The body is refused, not defaulted to empty, and the three conditions a collection payload has
     * to clear to default (ADR-0048 amendment B1) all fail here. Absence and empty are **not** the
     * same product decision — an empty service list says "nothing is bookable today", which is a
     * sentence about the business rather than about the request. Something **does** sum it:
     * `ConfirmStep.kt:103` renders the pre-quote subtotal from these rows. And the affordance read
     * off its emptiness — a booking flow with no service to pick — is one the customer takes as fact.
     */
    suspend fun getServices(): Response<List<ServiceListItem>> {
        val raw = serviceApi.serviceGetOverview()
        return raw.mapWire { items -> items.required("ServiceListItem[]").map { it.toAppDto() } }
    }

    /** Same three failures as [getServices]; `ConfirmStep.kt:104` sums these rows beside those. */
    suspend fun getPackages(): Response<List<PackageListItem>> {
        val raw = packageApi.packageGetOverview()
        return raw.mapWire { items -> items.required("PackageListItem[]").map { it.toAppDto() } }
    }

    /**
     * The one surface in this file that DOES default its payload, and it clears all three of B1's
     * conditions where services and packages fail every one. Absence and empty are the same product
     * decision: both mean no add-on section, which is what a customer sees whenever the endpoint is
     * down. Nothing sums, counts or paginates extras — `selectedExtraSlugs` goes to the server and
     * the price comes back on the quote, so no client figure moves. And an empty add-on card is not
     * a claim a customer reads as a fact, where an empty catalogue is.
     *
     * So a broken extras row degrades to no card rather than refusing, and the endpoint's own
     * failure is already handled that way at `CatalogRepository.kt:87`. Never a wrong add-on price.
     */
    suspend fun getExtras(): Response<List<ExtraListItem>> {
        val raw = extraApi.extraGetOverview()
        return raw.degrading page@{ items -> items.orEmpty().map { it.toAppDto() ?: return@page null } }
    }
}

/**
 * Re-wrap a [Response] preserving status + headers but mapping the parsed body. A `null` from
 * [transform] is a page this app chose to lose rather than refuse; only [getExtras] may use it, and
 * only because it clears B1's three conditions.
 */
private inline fun <T, R> Response<T>.degrading(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → hand-written mappers ───
//
// The catalog refuses the page where the orders list drops the row, and the difference is that the
// catalog *is* the addends. ConfirmStep renders the pre-quote subtotal as
// `services.filter { it.id in state.selectedServiceIds }.sumOf { … }` — the selection is a set of ids
// held in BookingState, not a slice of this list — so a dropped row leaves its id selected, still
// priced by the server on Create, and silently missing from the figure the customer reads before
// agreeing to it. A smaller, plausible, unmarked total is the exact failure this exists to prevent,
// and marking the row still leaves the sum undefined. That inverts the drop half of the identity rule
// too: a service with no id fails the page rather than being dropped.
//
// Extras take the same ruling on the ROW — `selectedExtraSlugs` is held outside this list too, so a
// dropped extra is charged on the quote while the picker shows it unselected — and the opposite one
// on the PAYLOAD, which is where the two surfaces genuinely differ rather than diverge. See
// [getExtras] for the three conditions that decide it.

private fun GenServiceListItem.toAppDto(): ServiceListItem =
    ServiceListItem(
        id = id.required("id"),
        name = name.required("name"),
        description = description,
        basePrice = basePrice.required("basePrice"),
        perRoomPrice = perRoomPrice.required("perRoomPrice"),
        category = category.required("category").toAppDto(),
        translations = translations?.mapValues { it.value.toAppDto() },
    )

private fun GenPackageListItem.toAppDto(): PackageListItem =
    PackageListItem(
        id = id.required("id"),
        name = name.required("name"),
        description = description,
        price = price.required("price"),
        translations = translations?.mapValues { it.value.toAppDto() },
        includedServices = includedServices?.map { it.toAppDto() },
    )

private fun GenExtraListItem.toAppDto(): ExtraListItem? {
    val id = id ?: return null
    val slug = slug ?: return null
    val name = name ?: return null
    return ExtraListItem(
        id = id,
        slug = slug,
        name = name,
        description = description,
        price = price ?: return null,
        displayOrder = displayOrder ?: return null,
        translations = translations?.mapValues { it.value.toAppDto() },
    )
}

private fun GenCategoryDto.toAppDto(): CategoryDto =
    CategoryDto(
        id = id.required("id"),
        slug = slug.required("slug"),
        name = name.required("name"),
        description = description,
        displayOrder = displayOrder.required("displayOrder"),
        translations = translations?.mapValues { it.value.toAppDto() },
    )

/**
 * Dropped rather than refused was the wrong half of the identity rule here: this list is the
 * "includes" line a customer reads before buying a fixed-price package, so a lost row understates
 * what they are getting for a price that does not move.
 */
private fun GenPackageServiceSummary.toAppDto(): PackageServiceSummary =
    PackageServiceSummary(
        name = name.required("name"),
        translations = translations?.mapValues { it.value.toAppDto() },
    )

private fun GenTranslation.toAppDto(): TranslationDto =
    TranslationDto(name = name.orEmpty(), description = description)
