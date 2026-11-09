package cz.cleansia.core.ui

import androidx.compose.foundation.pager.PagerState
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Test

/** A bottom-bar jump never animates through the tabs between: only the neighbouring tab slides in. */
class PagerTabsTest {

    private val pager = mockk<PagerState>(relaxed = true).also { every { it.currentPage } returns 1 }

    @Test
    fun `a neighbouring tab slides in and a farther one is shown at once`() = runTest {
        pager.showTab(0)
        pager.showTab(2)
        pager.showTab(3)

        coVerify(exactly = 1) { pager.animateScrollToPage(0, any(), any()) }
        coVerify(exactly = 1) { pager.animateScrollToPage(2, any(), any()) }
        coVerify(exactly = 1) { pager.scrollToPage(3, any()) }
        coVerify(exactly = 0) { pager.animateScrollToPage(3, any(), any()) }
    }
}
