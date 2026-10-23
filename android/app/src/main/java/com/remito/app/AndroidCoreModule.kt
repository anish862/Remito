package com.remito.app

import android.content.Context
import androidx.room.Room
import com.remito.core.database.RateDao
import com.remito.core.database.RemitoDatabase
import com.remito.core.network.EncryptedTokenStore
import com.remito.core.network.NetworkClient
import com.remito.core.network.RatesRepository
import com.remito.core.network.RemitoApi
import com.remito.core.network.TokenStore
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
    fun database(
        @ApplicationContext context: Context,
    ): RemitoDatabase = Room.databaseBuilder(context, RemitoDatabase::class.java, "remito.db").build()

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
}
