package cz.cleansia.partner.core.market

/**
 * A market the register picker can list and send: [countryId] is what `RegisterEmployee` carries,
 * [isDefault] decides the pre-selection, the rest is the label. The currency and copy figures the
 * wire also carries are the customer app's business and never reach the picker.
 * -> /decisions/adr-0058
 */
data class Market(
    val countryId: String,
    val isoCode: String,
    val name: String,
    val translatedNames: Map<String, String>,
    val isDefault: Boolean,
)

/** ADR-0058 D2: the flagged default, else the first listed; null only for an empty directory. */
fun List<Market>.defaultOrFirst(): Market? = firstOrNull { it.isDefault } ?: firstOrNull()
