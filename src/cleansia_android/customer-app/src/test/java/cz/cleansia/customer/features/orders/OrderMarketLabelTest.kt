package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.catalog.TranslationDto
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketState
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class OrderMarketLabelTest {
    private val cz = MarketListItem("cz", "CZE", "CZ", "Czechia", mapOf("cs" to TranslationDto("Česko")), "czk", "CZK", "Kč", true)
    private val sk = MarketListItem("sk", "SVK", "SK", "Slovakia", mapOf("cs" to TranslationDto("Slovensko")), "eur", "EUR", "€", false)
    private val directory = listOf(cz, sk)

    @Test
    fun `historical markets remain independent of the browsing selection`() {
        val browsingCz = MarketState.Resolved(directory, cz)
        val browsingSk = MarketState.Resolved(directory, sk)
        assertEquals("Slovakia", orderMarketName("sk", browsingCz, "en"))
        assertEquals("Slovakia", orderMarketName("sk", browsingSk, "en"))
        assertEquals("Czechia", orderMarketName("cz", browsingSk, "en"))
        assertEquals("Česko", orderMarketName("cz", browsingSk, "cs"))
    }

    @Test
    fun `missing countries never borrow the selected market`() {
        val markets = MarketState.Resolved(directory, cz)
        assertNull(orderMarketName("retired", markets, "en"))
        assertNull(orderMarketName(null, markets, "en"))
        assertNull(orderMarketName("cz", MarketState.Unavailable, "en"))
    }

    @Test
    fun `list and detail use each orders currency and observe the market directory`() {
        for (file in listOf("OrdersTab.kt", "OrderDetailScreen.kt")) {
            val source = File(moduleDir, "src/main/java/cz/cleansia/customer/features/orders/$file").readText()
            assertTrue(source.contains("viewModel.markets.collectAsStateWithLifecycle()"))
            assertTrue(source.contains("orderMarketLabel(order.countryId, order.currency?.code, markets)"))
        }
        val label = File(moduleDir, "src/main/java/cz/cleansia/customer/features/orders/OrderMarketLabel.kt").readText()
        assertTrue(label.contains("R.string.order_market_label, country, currencyCode"))
    }

    @Test
    fun `market labels and the unavailable state are present in every locale`() {
        for (folder in listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")) {
            val xml = File(moduleDir, "src/main/res/$folder/strings.xml").readText()
            assertTrue(xml.contains("name=\"order_market_unknown\""))
            assertTrue(xml.contains("<string name=\"order_market_label\">%1\$s · %2\$s</string>"))
        }
    }

    private val moduleDir: File
        get() = if (File("src/main").isDirectory) File(".") else File("customer-app")
}
