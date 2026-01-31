package cz.cleansia.partner.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import cz.cleansia.partner.core.database.entities.CachedOrder
import kotlinx.coroutines.flow.Flow

@Dao
interface OrderDao {

    @Query("SELECT * FROM cached_orders ORDER BY cleaningDateTime DESC")
    fun getAllOrders(): Flow<List<CachedOrder>>

    @Query("SELECT * FROM cached_orders ORDER BY cleaningDateTime DESC LIMIT :limit OFFSET :offset")
    suspend fun getOrdersPaged(limit: Int, offset: Int): List<CachedOrder>

    @Query("SELECT * FROM cached_orders WHERE id = :orderId")
    suspend fun getOrderById(orderId: String): CachedOrder?

    @Query("SELECT * FROM cached_orders WHERE id = :orderId")
    fun getOrderByIdFlow(orderId: String): Flow<CachedOrder?>

    @Query("SELECT * FROM cached_orders WHERE orderStatusValue = :statusValue ORDER BY cleaningDateTime DESC")
    fun getOrdersByStatus(statusValue: Int): Flow<List<CachedOrder>>

    @Query("""
        SELECT * FROM cached_orders
        WHERE displayOrderNumber LIKE '%' || :searchTerm || '%'
           OR customerName LIKE '%' || :searchTerm || '%'
           OR customerAddress LIKE '%' || :searchTerm || '%'
        ORDER BY cleaningDateTime DESC
    """)
    fun searchOrders(searchTerm: String): Flow<List<CachedOrder>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrders(orders: List<CachedOrder>)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrder(order: CachedOrder)

    @Query("DELETE FROM cached_orders WHERE id = :orderId")
    suspend fun deleteOrder(orderId: String)

    @Query("DELETE FROM cached_orders")
    suspend fun deleteAllOrders()

    @Query("SELECT COUNT(*) FROM cached_orders")
    suspend fun getOrderCount(): Int

    @Query("SELECT MAX(cachedAt) FROM cached_orders")
    suspend fun getLastCacheTime(): Long?

    /**
     * Delete orders cached before a certain time (for cache invalidation)
     */
    @Query("DELETE FROM cached_orders WHERE cachedAt < :timestamp")
    suspend fun deleteOldOrders(timestamp: Long)
}
