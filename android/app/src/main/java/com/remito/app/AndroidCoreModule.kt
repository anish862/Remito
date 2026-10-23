package com.remito.app

import android.content.Context
import androidx.room.Room
import com.remito.core.common.UserSettings
import com.remito.core.common.UserSettingsStore
import com.remito.core.database.RateDao
import com.remito.core.database.RemitoDatabase
import com.remito.core.network.EncryptedTokenStore
import com.remito.core.network.EngagementRepository
import com.remito.core.network.NetworkClient
import com.remito.core.network.RatesRepository
import com.remito.core.network.RemitoApi
import com.remito.core.network.TokenStore
import com.remito.feature.compare.ComparisonRepository
import com.remito.feature.compare.RedirectAnalytics
import com.remito.feature.compare.RedirectEvent
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object AndroidCoreModule {
    @Provides
    @Singleton
    fun userSettings(
        @ApplicationContext context: Context,
    ): UserSettings = UserSettingsStore(context)

    @Provides
    @Singleton
    fun database(
        @ApplicationContext context: Context,
    ): RemitoDatabase =
        Room
            .databaseBuilder(context, RemitoDatabase::class.java, "remito.db")
            .addMigrations(RemitoDatabase.MIGRATION_1_2)
            .addMigrations(RemitoDatabase.MIGRATION_2_3)
            .build()

    @Provides
    fun rateDao(database: RemitoDatabase): RateDao = database.rateDao()

    @Provides
    @Singleton
    fun tokenStore(
        @ApplicationContext context: Context,
    ): TokenStore = EncryptedTokenStore(context)

    @Provides
    @Singleton
    fun api(tokenStore: TokenStore): RemitoApi {
        val pins =
            BuildConfig.CERTIFICATE_PINS
                .split(',')
                .map(String::trim)
                .filter(String::isNotEmpty)
                .toSet()
        val retrofit =
            NetworkClient.create(
                baseUrl = BuildConfig.API_BASE_URL,
                tokenStore = tokenStore,
                isDebug = BuildConfig.DEBUG,
                certificatePins = pins,
            )
        return retrofit.create(RemitoApi::class.java)
    }

    @Provides
    @Singleton
    fun ratesRepository(
        api: RemitoApi,
        rateDao: RateDao,
    ): RatesRepository = RatesRepository(api, rateDao)

    @Provides
    @Singleton
    fun engagementRepository(
        api: RemitoApi,
        rateDao: RateDao,
    ): EngagementRepository = EngagementRepository(api, rateDao)

    @Provides
    @Singleton
    fun comparisonRepository(
        api: RemitoApi,
        rateDao: RateDao,
    ): ComparisonRepository = ComparisonRepository(api, rateDao)

    @Provides
    @Singleton
    fun redirectAnalytics(): RedirectAnalytics =
        RedirectAnalytics { event ->
            if (BuildConfig.DEBUG) {
                android.util.Log.d(
                    "RemitoAnalytics",
                    when (event) {
                        RedirectEvent.STARTED -> "redirect_started"
                        RedirectEvent.RETURNED -> "redirect_returned"
                    },
                )
            }
        }
}
