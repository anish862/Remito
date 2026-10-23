package com.remito.core.database

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Database(
    entities = [
        CachedRateEntity::class,
        RateHistoryEntity::class,
        ExchangeEntity::class,
        ComparisonOfferEntity::class,
        FavouriteEntity::class,
        AlertEntity::class,
    ],
    version = 2,
    exportSchema = true,
)
abstract class RemitoDatabase : RoomDatabase() {
    abstract fun rateDao(): RateDao

    companion object {
        val MIGRATION_1_2 =
            object : Migration(1, 2) {
                override fun migrate(db: SupportSQLiteDatabase) {
                    db.execSQL(
                        """
                        CREATE TABLE IF NOT EXISTS comparison_offers (
                            corridor TEXT NOT NULL,
                            amount TEXT NOT NULL,
                            exchangeId TEXT NOT NULL,
                            name TEXT NOT NULL,
                            logo TEXT,
                            rate TEXT NOT NULL,
                            fee TEXT,
                            amountReceived TEXT,
                            feeDisclosure TEXT NOT NULL,
                            lastUpdated TEXT NOT NULL,
                            isStale INTEGER NOT NULL,
                            hasApp INTEGER NOT NULL,
                            isSponsored INTEGER NOT NULL,
                            isBestRate INTEGER NOT NULL,
                            PRIMARY KEY(corridor, amount, exchangeId)
                        )
                        """.trimIndent(),
                    )
                }
            }
    }
}
