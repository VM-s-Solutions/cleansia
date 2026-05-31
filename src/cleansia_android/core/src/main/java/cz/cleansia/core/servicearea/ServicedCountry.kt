package cz.cleansia.core.servicearea

/**
 * Core-owned slim shape for a serviced country. Each consumer app maps
 * its NSwag-generated CountryListItem into this on its way out of the
 * adapter, so the shared `ServiceAreaProvider` doesn't carry a
 * dependency on either app's generated client package.
 *
 * [isoCode] is normalised to lowercase by the provider before exposure
 * (Mapbox's bias parameter expects `cz`, not `CZE` or `CZ`).
 */
data class ServicedCountry(
    val id: String,
    val isoCode: String,
    val name: String,
)
