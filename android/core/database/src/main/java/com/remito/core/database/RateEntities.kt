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

@Entity(tableName = "favourites")
data class FavouriteEntity(
    @PrimaryKey val targetId: String,
    val type: String,
)

@Entity(tableName = "alerts")
data class AlertEntity(
    @PrimaryKey val id: String,
    val pair: String,
    val direction: String,
    val target: String,
    val isActive: Boolean,
)
