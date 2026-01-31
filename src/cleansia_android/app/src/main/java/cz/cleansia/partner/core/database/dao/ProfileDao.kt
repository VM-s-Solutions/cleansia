package cz.cleansia.partner.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import cz.cleansia.partner.core.database.entities.CachedProfile
import kotlinx.coroutines.flow.Flow

@Dao
interface ProfileDao {

    @Query("SELECT * FROM cached_profile LIMIT 1")
    fun getProfile(): Flow<CachedProfile?>

    @Query("SELECT * FROM cached_profile LIMIT 1")
    suspend fun getProfileSync(): CachedProfile?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertProfile(profile: CachedProfile)

    @Query("DELETE FROM cached_profile")
    suspend fun deleteProfile()

    @Query("SELECT MAX(cachedAt) FROM cached_profile")
    suspend fun getLastCacheTime(): Long?
}
