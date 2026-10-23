package com.remito.core.network

import com.squareup.moshi.Moshi
import okhttp3.Authenticator
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route
import retrofit2.Call
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import retrofit2.http.Body
import retrofit2.http.POST
import java.io.IOException
import java.util.concurrent.locks.ReentrantLock
import kotlin.concurrent.withLock

private interface RefreshEndpoint {
    @POST("auth/refresh")
    fun refreshToken(
        @Body request: RefreshRequest,
    ): Call<TokenResponse>
}

class AccessTokenInterceptor(
    private val tokenStore: TokenStore,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val token = tokenStore.accessToken()
        val request =
            if (token.isNullOrBlank() ||
                chain
                    .request()
                    .url.encodedPath
                    .endsWith("/auth/refresh")
            ) {
                chain.request()
            } else {
                chain
                    .request()
                    .newBuilder()
                    .header("Authorization", "Bearer $token")
                    .build()
            }
        return chain.proceed(request)
    }
}

class TokenRefreshAuthenticator(
    private val baseUrl: String,
    private val tokenStore: TokenStore,
    private val client: OkHttpClient,
    private val moshi: Moshi,
    private val onRefreshFailure: () -> Unit,
) : Authenticator {
    private val refreshLock = ReentrantLock()

    @Suppress("SwallowedException")
    override fun authenticate(
        route: Route?,
        response: Response,
    ): Request? {
        if (responseCount(response) >= 2 ||
            response.request.url.encodedPath
                .endsWith("/auth/refresh")
        ) {
            return null
        }
        val failedToken = response.request.header("Authorization")?.removePrefix("Bearer ")
        return refreshLock.withLock {
            val currentToken = tokenStore.accessToken()
            if (!currentToken.isNullOrBlank() && currentToken != failedToken) {
                return@withLock response.request
                    .newBuilder()
                    .header("Authorization", "Bearer $currentToken")
                    .build()
            }
            val refreshToken = tokenStore.refreshToken()
            if (refreshToken.isNullOrBlank()) {
                onRefreshFailure()
                return@withLock null
            }
            val api =
                Retrofit
                    .Builder()
                    .baseUrl(baseUrl)
                    .client(client)
                    .addConverterFactory(MoshiConverterFactory.create(moshi))
                    .build()
                    .create(RefreshEndpoint::class.java)
            val refreshed =
                try {
                    api.refreshToken(RefreshRequest(refreshToken)).execute()
                } catch (error: IOException) {
                    onRefreshFailure()
                    return@withLock null
                }

            try {
                val body = refreshed.body()
                if (!refreshed.isSuccessful || body == null) {
                    onRefreshFailure()
                    null
                } else {
                    tokenStore.save(body.accessToken, body.refreshToken)
                    response.request
                        .newBuilder()
                        .header("Authorization", "Bearer ${body.accessToken}")
                        .build()
                }
            } finally {
                refreshed.raw().close()
            }
        }
    }

    private fun responseCount(response: Response): Int {
        var count = 1
        var prior = response.priorResponse
        while (prior != null) {
            count++
            prior = prior.priorResponse
        }
        return count
    }
}

class IdempotentGetRetryInterceptor(
    private val retries: Int = 2,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.method != "GET") return chain.proceed(request)
        var attempt = 0
        var failure: IOException? = null
        while (attempt <= retries) {
            try {
                return chain.proceed(request)
            } catch (error: IOException) {
                failure = error
                if (attempt == retries) throw error
                attempt++
            }
        }
        throw checkNotNull(failure)
    }
}
