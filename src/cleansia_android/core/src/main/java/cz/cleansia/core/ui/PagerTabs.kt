package cz.cleansia.core.ui

import androidx.compose.foundation.pager.PagerState
import kotlin.math.abs

/**
 * Shows the tab at [page]: the next tab slides in, a farther one appears at once. An animated jump of two or more
 * pages passes through a tab on the way, which the pager builds from scratch only to throw it away.
 */
suspend fun PagerState.showTab(page: Int) {
    if (abs(page - currentPage) > 1) scrollToPage(page) else animateScrollToPage(page)
}
