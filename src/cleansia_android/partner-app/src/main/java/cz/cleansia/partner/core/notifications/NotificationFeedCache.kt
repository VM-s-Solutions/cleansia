package cz.cleansia.partner.core.notifications

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.core.notifications.db.NotificationDao
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Session seam for the Room-backed notification feed. The feed (and its
 * unread badge) is per-account state, so it joins the [SessionScopedCache]
 * multibinding and is wiped on every sign-out — voluntary or forced.
 */
@Singleton
class NotificationFeedCache @Inject constructor(
    private val notificationDao: NotificationDao,
) : SessionScopedCache {

    override suspend fun clear() {
        notificationDao.clearAll()
    }
}
