package cz.cleansia.customer.features.orders

import org.junit.Assert.assertEquals
import org.junit.Test

class GuestOrderLinkTest {
    @Test
    fun `a pasted tracking link yields the token it carries`() {
        assertEquals(
            "P8Jw-2hQ_x",
            guestAccessTokenFrom(
                "https://cleansia.cz/track-order?orderNumber=CZ-123&email=a%40b.cz&token=P8Jw-2hQ_x",
            ),
        )
    }

    @Test
    fun `the token is taken whatever its place among the parameters`() {
        assertEquals("abc", guestAccessTokenFrom("https://cleansia.cz/track-order?token=abc&email=a%40b.cz"))
        assertEquals("abc", guestAccessTokenFrom("https://cleansia.cz/track-order?token=abc#top"))
    }

    @Test
    fun `a bare token is left alone apart from surrounding blanks`() {
        assertEquals("P8Jw-2hQ_x", guestAccessTokenFrom("  P8Jw-2hQ_x  "))
        assertEquals("", guestAccessTokenFrom("   "))
    }

    @Test
    fun `a parameter that merely ends in token is not the token`() {
        assertEquals(
            "https://cleansia.cz/track-order?accesstoken=abc",
            guestAccessTokenFrom("https://cleansia.cz/track-order?accesstoken=abc"),
        )
    }
}
