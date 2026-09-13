package cz.cleansia.customer.core.market

import cz.cleansia.customer.api.client.MarketApi as GenMarketApi
import cz.cleansia.customer.api.model.MarketListItem as GenMarketListItem
import cz.cleansia.customer.api.model.Translation as GenTranslation
import cz.cleansia.customer.core.catalog.TranslationDto
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the generated anonymous `Market/GetOverview`.
 *
 * Refuses the page where the orders list drops the row: the markets are alternatives to each other, a
 * silently missing one is a country the customer never learns they can browse in, and the default
 * market is picked off this very list. The body is refused for the same reason — an absent list is
 * not "no market is open", it is a read this app cannot act on.
 */
class MarketApi(
    private val marketApi: GenMarketApi,
) {
    suspend fun getMarkets(): Response<List<MarketListItem>> {
        val raw = marketApi.marketGetOverview()
        return raw.mapWire { items -> items.required("MarketListItem[]").map { it.toAppDto() } }
    }
}

/**
 * Every identity and label field refuses: the id is what readers send, the codes are what the chip
 * and the rows print, and `isDefault` decides the pre-selection. The two copy figures are nullable by
 * design (ADR-0060) and stay so.
 */
private fun GenMarketListItem.toAppDto(): MarketListItem =
    MarketListItem(
        countryId = countryId.required("countryId"),
        isoCode = isoCode.required("isoCode"),
        isoAlpha2 = isoAlpha2.required("isoAlpha2"),
        name = name.required("name"),
        translations = translations?.mapValues { it.value.toAppDto() },
        currencyId = currencyId.required("currencyId"),
        currencyCode = currencyCode.required("currencyCode"),
        currencySymbol = currencySymbol.required("currencySymbol"),
        isDefault = isDefault.required("isDefault"),
        noShowCredit = noShowCredit,
        insuranceCoverageAmount = insuranceCoverageAmount,
    )

private fun GenTranslation.toAppDto(): TranslationDto =
    TranslationDto(name = name.orEmpty(), description = description)
