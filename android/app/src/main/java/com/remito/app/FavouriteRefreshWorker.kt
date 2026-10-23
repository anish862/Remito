package com.remito.app

import android.content.Context
import androidx.room.Room
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.remito.core.database.CachedRateEntity
import com.remito.core.database.RemitoDatabase
import com.remito.core.network.ApiRequestException
import com.remito.core.network.EncryptedTokenStore
import com.remito.core.network.FavouriteType
import com.remito.core.network.NetworkClient
import com.remito.core.network.RemitoApi
import com.remito.core.network.TokenStore
import retrofit2.Response
import java.io.IOException
import java.util.concurrent.TimeUnit

private const val HTTP_SERVER_ERROR_MIN = 500
private const val HTTP_SERVER_ERROR_MAX = 599
private const val REFRESH_INTERVAL_HOURS = 6L
private const val BASE_CURRENCY = "OMR"

class FavouriteRefreshWorker(
    context: Context,
    workerParameters: WorkerParameters,
) : CoroutineWorker(context, workerParameters) {
    override suspend fun doWork(): Result {
        val tokenStore: TokenStore = EncryptedTokenStore(applicationContext)
        val pins =
            BuildConfig.CERTIFICATE_PINS
                .split(',')
                .map(String::trim)
                .filter(String::isNotEmpty)
                .toSet()
        val api =
            NetworkClient
                .create(
                    baseUrl = BuildConfig.API_BASE_URL,
                    tokenStore = tokenStore,
                    isDebug = BuildConfig.DEBUG,
                    certificatePins = pins,
                ).create(com.remito.core.network.RemitoApi::class.java)
        val database =
            Room
                .databaseBuilder(applicationContext, RemitoDatabase::class.java, "remito.db")
                .addMigrations(RemitoDatabase.MIGRATION_1_2, RemitoDatabase.MIGRATION_2_3)
                .build()
        return try {
            if (tokenStore.accessToken().isNullOrBlank()) {
                Result.success()
            } else {
                refresh(api, database)
            }
        } catch (error: ApiRequestException) {
            if (BuildConfig.DEBUG) {
                android.util.Log.w("RemitoRates", "Favourite refresh request failed.", error)
            }
            if (error.statusCode in HTTP_SERVER_ERROR_MIN..HTTP_SERVER_ERROR_MAX) Result.retry() else Result.failure()
        } catch (error: IOException) {
            if (BuildConfig.DEBUG) {
                android.util.Log.w("RemitoRates", "Favourite refresh is temporarily unavailable.", error)
            }
            Result.retry()
        } finally {
            database.close()
        }
    }

    private suspend fun refresh(
        api: RemitoApi,
        database: RemitoDatabase,
    ): Result {
        val favouritesResponse = api.favourites()
        val favourites = favouritesResponse.requireBody("Could not fetch favourites.")
        val currencies =
            favourites
                .filter { it.type == FavouriteType.CURRENCY }
                .mapNotNull { it.targetCode }
                .distinct()
                .filter { it != BASE_CURRENCY }
        if (currencies.isEmpty()) return Result.success()

        val ratesResponse = api.latestRate(BASE_CURRENCY, currencies.joinToString(","))
        val rates = ratesResponse.requireBody("Could not refresh favourite rates.")
        database.rateDao().upsertRates(
            rates.rates.map { rate ->
                CachedRateEntity(
                    pair = "${rates.base}/${rate.currency}",
                    rate = rate.rate.toPlainString(),
                    change24HoursPercent = rate.change24HoursPercent.toPlainString(),
                    lastUpdated = rate.lastUpdated,
                    isStale = rate.isStale,
                )
            },
        )
        return Result.success()
    }
}

private fun <T : Any> Response<T>.requireBody(errorMessage: String): T {
    if (!isSuccessful) throw ApiRequestException(code(), errorMessage)
    return body() ?: throw IOException("$errorMessage The response body was empty.")
}

fun scheduleFavouriteRefresh(context: Context) {
    val constraints =
        androidx.work.Constraints
            .Builder()
            .setRequiredNetworkType(androidx.work.NetworkType.UNMETERED)
            .setRequiresBatteryNotLow(true)
            .build()
    val request =
        androidx.work
            .PeriodicWorkRequestBuilder<FavouriteRefreshWorker>(REFRESH_INTERVAL_HOURS, TimeUnit.HOURS)
            .setConstraints(constraints)
            .build()
    androidx.work.WorkManager.getInstance(context).enqueueUniquePeriodicWork(
        "favourite-rate-refresh",
        androidx.work.ExistingPeriodicWorkPolicy.KEEP,
        request,
    )
}
