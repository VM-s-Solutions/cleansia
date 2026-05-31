package cz.cleansia.core.servicearea

/**
 * Core-owned slim shape for a serviced city. Same rationale as
 * [ServicedCountry] — keeps the shared provider free of any
 * NSwag-generated type dependencies.
 */
data class ServicedCity(
    val id: String,
    val countryId: String,
    val name: String,
)
