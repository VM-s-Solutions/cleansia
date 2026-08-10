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
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated Service/Package/Extra clients. The
 * repository layer keeps the strict (non-null where it should be) hand-written
 * DTOs in [CatalogDto.kt]; this adapter calls the generated all-nullable
 * client and fills defaults at the seam.
 *
 * Why an adapter instead of swapping the DTO outright: the call sites
 * (repository, view-models, screens) all read `serviceListItem.id` etc. as
 * non-null. Changing them to `?` would propagate through dozens of files and
 * lose the "this field is always present" assumption. The pinch point is
 * here — wire-shape on one side, app-shape on the other.
 */
class CatalogApi(
    private val serviceApi: GenServiceApi,
    private val packageApi: GenPackageApi,
    private val extraApi: GenExtraApi,
) {
    suspend fun getServices(): Response<List<ServiceListItem>> {
        val raw = serviceApi.serviceGetOverview()
        return raw.map page@{ items -> items.orEmpty().map { it.toAppDto() ?: return@page null } }
    }

    suspend fun getPackages(): Response<List<PackageListItem>> {
        val raw = packageApi.packageGetOverview()
        return raw.map page@{ items -> items.orEmpty().map { it.toAppDto() ?: return@page null } }
    }

    suspend fun getExtras(): Response<List<ExtraListItem>> {
        val raw = extraApi.extraGetOverview()
        return raw.map page@{ items -> items.orEmpty().map { it.toAppDto() ?: return@page null } }
    }
}

/**
 * Re-wrap a [Response] preserving status + headers but mapping the parsed body. A `null` from
 * [transform] is a refused page, and surfaces as a 200-with-null-body that [CatalogRepository] turns
 * into an error rather than an empty catalog.
 */
private inline fun <T, R> Response<T>.map(transform: (T?) -> R?): Response<R> =
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
// Extras go the same way rather than a third way: `selectedExtraSlugs` is held outside this list too,
// so a dropped extra is charged on the quote while the picker shows it unselected. A refusal there
// degrades to no add-on section, which is what CatalogRepository already shows when the endpoint is
// down, and never a wrong add-on price.

private fun GenServiceListItem.toAppDto(): ServiceListItem? {
    val id = id ?: return null
    val name = name ?: return null
    return ServiceListItem(
        id = id,
        name = name,
        description = description,
        basePrice = basePrice ?: return null,
        perRoomPrice = perRoomPrice ?: return null,
        category = category?.toAppDto() ?: return null,
        translations = translations?.mapValues { it.value.toAppDto() },
    )
}

private fun GenPackageListItem.toAppDto(): PackageListItem? {
    val id = id ?: return null
    val name = name ?: return null
    return PackageListItem(
        id = id,
        name = name,
        description = description,
        price = price ?: return null,
        translations = translations?.mapValues { it.value.toAppDto() },
        includedServices = includedServices?.mapNotNull { it.toAppDto() },
    )
}

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

private fun GenCategoryDto.toAppDto(): CategoryDto? {
    val id = id ?: return null
    val slug = slug ?: return null
    val name = name ?: return null
    return CategoryDto(
        id = id,
        slug = slug,
        name = name,
        description = description,
        displayOrder = displayOrder ?: return null,
        translations = translations?.mapValues { it.value.toAppDto() },
    )
}

private fun GenPackageServiceSummary.toAppDto(): PackageServiceSummary? {
    val name = name ?: return null
    return PackageServiceSummary(
        name = name,
        translations = translations?.mapValues { it.value.toAppDto() },
    )
}

private fun GenTranslation.toAppDto(): TranslationDto =
    TranslationDto(name = name.orEmpty(), description = description)
