package cz.cleansia.partner.core.notifications

import cz.cleansia.partner.core.notifications.db.NotificationDao
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Test

class NotificationFeedCacheTest {

    @Test
    fun clear_deletesAllNotificationRecords() = runTest {
        val dao = mockk<NotificationDao>(relaxed = true)

        NotificationFeedCache(dao).clear()

        coVerify(exactly = 1) { dao.clearAll() }
    }
}
