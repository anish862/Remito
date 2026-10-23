package com.remito.core.database

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "cached_rates")
data class CachedRateEntity(
    @PrimaryKey val pair: String,
    val rate: String,
    val change24HoursPercent: String,
    val lastUpdated: String,
    val isStale: Boolean,
)

@Entity(tableName = "rate_history", primaryKeys = ["pair", "range", "timestamp"])
data class RateHistoryEntity(
    val pair: String,
    val range: String,
    val timestamp: String,
    val rate: String,
)

@Entity(tableName = "exchanges")
data class ExchangeEntity(
    @PrimaryKey val id: String,
    val name: String,
    val slug: String,
    val tier: String,
)

@Entity(tableName = "comparison_offers", primaryKeys = ["corridor", "amount", "exchangeId"])
data class ComparisonOfferEntity(
    val corridor: String,
    val amount: String,
    val exchangeId: String,
    val name: String,
    val logo: String?,
    val rate: String,
    val fee: String?,
    val amountReceived: String?,
    val feeDisclosure: String,
    val lastUpdated: String,
    val isStale: Boolean,
    val hasApp: Boolean,
    val isSponsored: Boolean,
    val isBestRate: Boolean,
)

@Entity(tableName = "favourites")
data class FavouriteEntity(
    @PrimaryKey val targetId: String,
    val type: String,
    val remoteId: String? = null,
)

@Entity(tableName = "alerts")
data class AlertEntity(
    @PrimaryKey val id: String,
    val pair: String,
    val direction: String,
    val target: String,
    val isActive: Boolean,
    val isOneTime: Boolean = false,
    val dailySummary: Boolean = false,
)

@Entity(tableName = "cached_news")
data class CachedNewsEntity(
    @PrimaryKey val slug: String,
    val id: String,
    val title: String,
    val summary: String,
    val body: String,
    val sourceUrl: String?,
    val publishedAt: String?,
)
