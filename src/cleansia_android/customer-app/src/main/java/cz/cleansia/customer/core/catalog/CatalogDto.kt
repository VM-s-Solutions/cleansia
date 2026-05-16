package cz.cleansia.customer.core.catalog

import kotlinx.serialization.Serializable

@Serializable
data class TranslationDto(
    val name: String,
    val description: String? = null,
)

@Serializable
data class CategoryDto(
    val id: String,
    val slug: String,
    val name: String,
    val description: String? = null,
    val displayOrder: Int = 0,
    val translations: Map<String, TranslationDto>? = null,
)

@Serializable
data class ServiceListItem(
    val id: String,
    val name: String,
    val description: String? = null,
    val basePrice: Double,
    val perRoomPrice: Double,
    val category: CategoryDto,
    val translations: Map<String, TranslationDto>? = null,
)

@Serializable
data class PackageServiceSummary(
    val name: String,
    val translations: Map<String, TranslationDto>? = null,
)

@Serializable
data class PackageListItem(
    val id: String,
    val name: String,
    val description: String? = null,
    val price: Double,
    val translations: Map<String, TranslationDto>? = null,
    val includedServices: List<PackageServiceSummary>? = null,
)

/**
 * Bookable add-on (inside-oven, inside-fridge, etc.). Slug is the stable
 * per-tenant identifier the client uses for icon lookups + the value the
 * pricing payload submits in `selectedExtraSlugs`.
 */
@Serializable
data class ExtraListItem(
    val id: String,
    val slug: String,
    val name: String,
    val description: String? = null,
    val price: Double,
    val displayOrder: Int = 0,
    val translations: Map<String, TranslationDto>? = null,
)
