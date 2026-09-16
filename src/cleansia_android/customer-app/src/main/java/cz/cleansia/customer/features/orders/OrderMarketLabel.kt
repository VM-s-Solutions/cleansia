package cz.cleansia.customer.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import cz.cleansia.customer.R
import cz.cleansia.customer.core.market.MarketState

internal fun orderMarketName(countryId: String?, markets: MarketState, language: String?): String? {
    val market = (markets as? MarketState.Resolved)?.markets
        ?.firstOrNull { countryId != null && it.countryId == countryId } ?: return null
    return language?.let { market.translations?.get(it)?.name }?.takeIf { it.isNotBlank() }
        ?: market.name.takeIf { it.isNotBlank() }
}

@Composable
internal fun orderMarketLabel(countryId: String?, currencyCode: String?, markets: MarketState): String {
    val country = orderMarketName(countryId, markets, LocalConfiguration.current.locales.get(0)?.language)
        ?: stringResource(R.string.order_market_unknown)
    return stringResource(R.string.order_market_label, country, currencyCode?.takeIf { it.isNotBlank() } ?: "—")
}
