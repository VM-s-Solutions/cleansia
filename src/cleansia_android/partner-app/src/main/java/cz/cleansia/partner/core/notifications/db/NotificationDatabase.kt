package cz.cleansia.partner.core.notifications.db

import androidx.room.Database
import androidx.room.RoomDatabase

@Database(
    entities = [NotificationRecord::class],
    version = 1,
    exportSchema = false,
)
abstract class NotificationDatabase : RoomDatabase() {
    abstract fun notificationDao(): NotificationDao
}
