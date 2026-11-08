package cz.cleansia.customer.features.splash

import androidx.lifecycle.ViewModelStore
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.delay
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.currentTime
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class SplashViewModelTest {

    @get:Rule val mainRule = MainDispatcherRule()

    private val tokenStore: TokenStore = mockk()
    private val orders: OrderRepository = mockk(relaxed = true)
    private val membership: MembershipRepository = mockk(relaxed = true)

    private fun tokens(refreshExpiresAt: Long) = TokenStore.Tokens(
        accessToken = "access",
        accessTokenExpiresAt = 0L,
        refreshToken = "refresh",
        refreshTokenExpiresAt = refreshExpiresAt,
    )

    private fun splash() = SplashViewModel(tokenStore, orders, membership)

    @Test
    fun `no stored session is not a valid one`() {
        every { tokenStore.current() } returns null
        assertEquals(false, splash().hasValidSession())
    }

    @Test
    fun `an expired refresh token is not a valid session`() {
        every { tokenStore.current() } returns tokens(refreshExpiresAt = System.currentTimeMillis() - 1_000L)
        assertEquals(false, splash().hasValidSession())
    }

    @Test
    fun `a live refresh token is a valid session even with the access token expired`() {
        every { tokenStore.current() } returns tokens(refreshExpiresAt = System.currentTimeMillis() + 60_000L)
        assertEquals(true, splash().hasValidSession())
    }

    @Test
    fun `Home's orders and membership load during the hold and land after the splash is gone`() = runTest {
        val hold = 1_800L
        val slowAnswer = 2_500L
        var ordersLandedAt = -1L
        var membershipLandedAt = -1L
        every { tokenStore.current() } returns tokens(refreshExpiresAt = System.currentTimeMillis() + 60_000L)
        coEvery { orders.refresh() } coAnswers {
            delay(slowAnswer)
            ordersLandedAt = currentTime
            ApiResult.Success(Unit)
        }
        coEvery { membership.refresh() } coAnswers {
            delay(slowAnswer)
            membershipLandedAt = currentTime
            ApiResult.Success(mockk())
        }
        val store = ViewModelStore().apply { put("splash", splash()) }

        delay(hold)
        store.clear()
        advanceUntilIdle()

        assertEquals(slowAnswer, ordersLandedAt)
        assertEquals(slowAnswer, membershipLandedAt)
    }
}
