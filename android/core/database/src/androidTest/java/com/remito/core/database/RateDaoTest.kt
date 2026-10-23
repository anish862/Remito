package com.remito.core.database

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import java.math.BigDecimal

@RunWith(AndroidJUnit4::class)
class RateDaoTest {
    private lateinit var database: RemitoDatabase

    @Before
    fun createDatabase() {
        database =
            Room
                .inMemoryDatabaseBuilder(
                    ApplicationProvider.getApplicationContext<Context>(),
                    RemitoDatabase::class.java,
                ).build()
    }

    @After
    fun closeDatabase() {
        database.close()
    }

    @Test
    fun cachedRateCanBeReadAndMarkedStale() =
        runBlocking {
            val rate = CachedRateEntity("OMR/INR", BigDecimal("216.7").toPlainString(), "0.4", "2025-01-01T00:00:00Z", false)
            database.rateDao().upsertRates(listOf(rate))
            database.rateDao().markRatesStale()

            assertEquals(true, database.rateDao().getRate("OMR/INR")?.isStale)
        }

    @Test
    fun comparisonOffersAreCachedAndMarkedStale() =
        runBlocking {
            val dao = database.rateDao()
            dao.upsertComparisonOffers(
                listOf(
                    ComparisonOfferEntity(
                        corridor = "OMR-INR",
                        amount = "100",
                        exchangeId = "exchange-1",
                        name = "Exchange",
                        logo = null,
                        rate = "216.7",
                        fee = null,
                        amountReceived = "21670",
                        feeDisclosure = "Fee not provided",
                        lastUpdated = "2026-10-23T10:00:00Z",
                        isStale = false,
                        hasApp = true,
                        isSponsored = false,
                        isBestRate = true,
                    ),
                ),
            )
            dao.markComparisonOffersStale("OMR-INR", "100")

            val offers = dao.observeComparisonOffers("OMR-INR", "100").first()
            assertEquals(1, offers.size)
            assertEquals(true, offers.single().isStale)
            assertEquals(true, offers.single().isBestRate)
        }
}
