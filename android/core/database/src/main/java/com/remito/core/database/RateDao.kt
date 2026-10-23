package com.remito.core.database

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
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

    @Query(
        "SELECT * FROM comparison_offers WHERE corridor = :corridor AND amount = :amount " +
            "ORDER BY isSponsored DESC, amountReceived DESC",
    )
    fun observeComparisonOffers(
        corridor: String,
        amount: String,
    ): Flow<List<ComparisonOfferEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertComparisonOffers(offers: List<ComparisonOfferEntity>)

    @Transaction
    suspend fun replaceComparisonOffers(
        corridor: String,
        amount: String,
        offers: List<ComparisonOfferEntity>,
    ) {
        clearComparisonOffers(corridor, amount)
        if (offers.isNotEmpty()) upsertComparisonOffers(offers)
    }

    @Query("DELETE FROM comparison_offers WHERE corridor = :corridor AND amount = :amount")
    suspend fun clearComparisonOffers(
        corridor: String,
        amount: String,
    )

    @Query("UPDATE comparison_offers SET isStale = 1 WHERE corridor = :corridor AND amount = :amount")
    suspend fun markComparisonOffersStale(
        corridor: String,
        amount: String,
    )

    @Query("SELECT * FROM favourites ORDER BY targetId")
    fun observeFavourites(): Flow<List<FavouriteEntity>>

    @Query("SELECT * FROM favourites WHERE targetId = :targetId")
    suspend fun getFavourite(targetId: String): FavouriteEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertFavourite(favourite: FavouriteEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertFavourites(favourites: List<FavouriteEntity>)

    @Query("DELETE FROM favourites WHERE targetId = :targetId")
    suspend fun removeFavourite(targetId: String)

    @Query("DELETE FROM favourites")
    suspend fun clearFavourites()

    @Query("SELECT * FROM alerts ORDER BY id")
    fun observeAlerts(): Flow<List<AlertEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAlerts(alerts: List<AlertEntity>)

    @Query("DELETE FROM alerts WHERE id = :id")
    suspend fun deleteAlert(id: String)

    @Query("DELETE FROM alerts")
    suspend fun clearAlerts()

    @Query("SELECT * FROM cached_news ORDER BY publishedAt DESC")
    fun observeNews(): Flow<List<CachedNewsEntity>>

    @Query("SELECT * FROM cached_news WHERE slug = :slug")
    suspend fun getNews(slug: String): CachedNewsEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertNews(items: List<CachedNewsEntity>)
}
