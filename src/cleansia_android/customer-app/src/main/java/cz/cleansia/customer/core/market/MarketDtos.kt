package cz.cleansia.customer.core.market

import cz.cleansia.customer.core.catalog.TranslationDto
import kotlinx.serialization.Serializable

/**
 * Mirrors backend `MarketListItem`: a serviced country with a configured, active currency. [isoCode]
 * (alpha-3) is what the preference stores; [countryId] is what every reader sends; [isoAlpha2] is what
 * the chip prints.
 * -> /decisions/adr-0058
 */
@Serializable
data class MarketListItem(
    val countryId: String,
    val isoCode: String,
    val isoAlpha2: String,
    val name: String,
    val translations: Map<String, TranslationDto>? = null,
    val currencyId: String,
    val currencyCode: String,
    val currencySymbol: String,
    val isDefault: Boolean,
    /** Apology credit paid on a no-show in this market's currency; null = none authored. */
    val noShowCredit: Double? = null,
    /** Insurance ceiling stated in customer copy, in [currencyCode]; null = the no-figure copy renders. */
    val insuranceCoverageAmount: Double? = null,
)

/**
 * What the app knows about the market directory. [Unavailable] is both "not read yet" and "the read
 * failed with nothing known before": readers send no `countryId`, nothing is rendered that names a
 * market, and the next refresh retries.
 */
sealed interface MarketState {
    data object Unavailable : MarketState

    data class Resolved(val markets: List<MarketListItem>, val selected: MarketListItem) : MarketState
}

val MarketState.selectedOrNull: MarketListItem?
    get() = (this as? MarketState.Resolved)?.selected

/** What every pre-address reader sends; null asks the server for the platform default. */
val MarketState.countryId: String?
    get() = selectedOrNull?.countryId

/** The chip and the selector render only when there is a choice to make. */
val MarketState.offersAChoice: Boolean
    get() = this is MarketState.Resolved && markets.size >= 2

/** The platform-default currency's code, as the directory states it; null when no listed market carries it. */
val MarketState.defaultCurrencyCode: String?
    get() = (this as? MarketState.Resolved)?.markets?.firstOrNull { it.isDefault }?.currencyCode

/** An insurance ceiling with the unit it is stated in (ADR-0060 D2); formatted on device, never a literal. */
data class InsuranceCoverage(val amount: Double, val currencyCode: String)

val MarketListItem.insuranceCoverage: InsuranceCoverage?
    get() = insuranceCoverageAmount?.let { InsuranceCoverage(it, currencyCode) }
