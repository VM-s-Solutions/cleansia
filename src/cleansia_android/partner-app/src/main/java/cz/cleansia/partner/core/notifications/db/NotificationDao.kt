package cz.cleansia.partner.core.notifications.db

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface NotificationDao {

    /** Feed source — newest first. Hot Flow so the screen recomposes on insert. */
    @Query("SELECT * FROM notification_records ORDER BY timestamp DESC")
    fun observeAll(): Flow<List<NotificationRecord>>

    /** Bell-badge source — count of unread, observed live. */
    @Query("SELECT COUNT(*) FROM notification_records WHERE isRead = 0")
    fun observeUnreadCount(): Flow<Int>

    @Insert
    suspend fun insert(record: NotificationRecord)

    /** Called when the feed opens — everything visible is now seen. */
    @Query("UPDATE notification_records SET isRead = 1 WHERE isRead = 0")
    suspend fun markAllRead()
}
