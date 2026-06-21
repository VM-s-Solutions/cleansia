package cz.cleansia.partner.features.notifications

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.notifications.db.NotificationDao
import cz.cleansia.partner.core.notifications.db.NotificationRecord
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Backs the notifications feed. Reads the Room-backed list as a hot StateFlow
 * and exposes a one-shot [markAllRead] the screen fires on entry so the bell
 * badge clears once the cleaner has seen the feed.
 */
@HiltViewModel
class NotificationsViewModel @Inject constructor(
    private val notificationDao: NotificationDao,
) : ViewModel() {

    val records: StateFlow<List<NotificationRecord>> = notificationDao.observeAll()
        .stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = emptyList(),
        )

    fun markAllRead() {
        viewModelScope.launch { notificationDao.markAllRead() }
    }
}
