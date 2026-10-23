package com.remito.core.network

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.remito.core.database.RemitoDatabase
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class RatesRepositoryTest {
    private lateinit var database: RemitoDatabase
    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        database =
            Room
                .inMemoryDatabaseBuilder(
                    ApplicationProvider.getApplicationContext<Context>(),
                    RemitoDatabase::class.java,
                ).build()
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        database.close()
        server.shutdown()
    }

    @Test
    fun refreshStoresApiRatesForImmediateOfflineReads() =
        runBlocking {
            server.enqueue(
                MockResponse().setResponseCode(200).setBody(
                    """{"base":"OMR","rates":[{"currency":"INR","rate":216.7,"change24HoursPercent":0.4,"lastUpdated":"2025-01-01T00:00:00Z","isStale":false}]}""",
                ),
            )
            val repository =
                RatesRepository(
                    NetworkClient
                        .create(server.url("/api/v1/").toString(), isDebug = true)
                        .create(RemitoApi::class.java),
                    database.rateDao(),
                )

            repository.refreshRates("OMR", listOf("INR"))

            assertEquals("216.7", database.rateDao().getRate("OMR/INR")?.rate)
        }
}
