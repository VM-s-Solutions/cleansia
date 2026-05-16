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
        return raw.map { items -> items?.mapNotNull { it.toAppDto() }.orEmpty() }
    }

    suspend fun getPackages(): Response<List<PackageListItem>> {
        val raw = packageApi.packageGetOverview()
        return raw.map { items -> items?.mapNotNull { it.toAppDto() }.orEmpty() }
    }

    suspend fun getExtras(): Response<List<ExtraListItem>> {
        val raw = extraApi.extraGetOverview()
        return raw.map { items -> items?.mapNotNull { it.toAppDto() }.orEmpty() }
    }
}

/**
 * Re-wrap a [Response] preserving status + headers but mapping the parsed
 * body. Skips mapping on error responses (the body is null / error-shaped
 * anyway).
 */
private inline fun <T, R> Response<T>.map(transform: (T?) -> R): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → hand-written mappers ───
//
// Drop items missing a required field (id, name) — these are server bugs we
// shouldn't render. Better than fabricating placeholder UI from null data.

private fun GenServiceListItem.toAppDto(): ServiceListItem? {
    val id = id ?: return null
    val name = name ?: return null
    return ServiceListItem(
        id = id,
        name = name,
        description = description,
        basePrice = basePrice ?: 0.0,
        perRoomPrice = perRoomPrice ?: 0.0,
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
        price = price ?: 0.0,
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
        price = price ?: 0.0,
        displayOrder = displayOrder ?: 0,
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
        displayOrder = displayOrder ?: 0,
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
