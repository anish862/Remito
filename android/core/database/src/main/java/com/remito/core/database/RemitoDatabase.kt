package com.remito.core.database

import androidx.room.Database
import androidx.room.RoomDatabase

@Database(
    entities = [
        CachedRateEntity::class,
        RateHistoryEntity::class,
        ExchangeEntity::class,
        FavouriteEntity::class,
        AlertEntity::class,
    ],
    version = 1,
    exportSchema = true,
)
abstract class RemitoDatabase : RoomDatabase() {
    abstract fun rateDao(): RateDao
}
