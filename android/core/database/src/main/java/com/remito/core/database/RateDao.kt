package com.remito.core.database

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
@Suppress("TooManyFunctions")
interface RateDao {
    @Query("SELECT * FROM cached_rates ORDER BY pair")
    fun observeRates(): Flow<List<CachedRateEntity>>

    @Query("SELECT * FROM cached_rates WHERE pair = :pair")
    suspend fun getRate(pair: String): CachedRateEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertRates(rates: List<CachedRateEntity>)

    @Query("UPDATE cached_rates SET isStale = 1")
    suspend fun markRatesStale()

    @Query("SELECT * FROM rate_history WHERE pair = :pair AND `range` = :range ORDER BY timestamp")
    fun observeHistory(
        pair: String,
        range: String,
    ): Flow<List<RateHistoryEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertHistory(points: List<RateHistoryEntity>)

    @Query("SELECT * FROM exchanges ORDER BY name")
    fun observeExchanges(): Flow<List<ExchangeEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertExchanges(exchanges: List<ExchangeEntity>)

    @Query("SELECT * FROM favourites ORDER BY targetId")
    fun observeFavourites(): Flow<List<FavouriteEntity>>

    @Query("SELECT * FROM favourites WHERE targetId = :targetId")
    suspend fun getFavourite(targetId: String): FavouriteEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertFavourite(favourite: FavouriteEntity)

    @Query("DELETE FROM favourites WHERE targetId = :targetId")
    suspend fun removeFavourite(targetId: String)

    @Query("SELECT * FROM alerts ORDER BY id")
    fun observeAlerts(): Flow<List<AlertEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAlerts(alerts: List<AlertEntity>)
}
