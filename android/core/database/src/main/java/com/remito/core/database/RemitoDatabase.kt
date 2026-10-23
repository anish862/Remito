package com.remito.core.database

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

private const val VERSION_2 = 2
private const val VERSION_3 = 3

@Database(
    entities = [
        CachedRateEntity::class,
        RateHistoryEntity::class,
        ExchangeEntity::class,
        ComparisonOfferEntity::class,
        FavouriteEntity::class,
        AlertEntity::class,
        CachedNewsEntity::class,
    ],
    version = 3,
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

        val MIGRATION_2_3 =
            object : Migration(VERSION_2, VERSION_3) {
                override fun migrate(db: SupportSQLiteDatabase) {
                    db.execSQL("ALTER TABLE favourites ADD COLUMN remoteId TEXT")
                    db.execSQL("ALTER TABLE alerts ADD COLUMN isOneTime INTEGER NOT NULL DEFAULT 0")
                    db.execSQL("ALTER TABLE alerts ADD COLUMN dailySummary INTEGER NOT NULL DEFAULT 0")
                    db.execSQL(
                        """
                        CREATE TABLE IF NOT EXISTS cached_news (
                            slug TEXT NOT NULL,
                            id TEXT NOT NULL,
                            title TEXT NOT NULL,
                            summary TEXT NOT NULL,
                            body TEXT NOT NULL,
                            sourceUrl TEXT,
                            publishedAt TEXT,
                            PRIMARY KEY(slug)
                        )
                        """.trimIndent(),
                    )
                }
            }
    }
}
