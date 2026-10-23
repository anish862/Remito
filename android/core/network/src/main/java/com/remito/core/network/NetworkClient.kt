package com.remito.core.network

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import okhttp3.CertificatePinner
import okhttp3.ConnectionSpec
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import java.util.concurrent.TimeUnit

object NetworkClient {
    private const val CONNECT_TIMEOUT_SECONDS = 15L
    private const val READ_TIMEOUT_SECONDS = 20L
    private const val WRITE_TIMEOUT_SECONDS = 20L
    private const val CALL_TIMEOUT_SECONDS = 30L

    fun create(
        baseUrl: String,
        tokenStore: TokenStore? = null,
        logoutOnRefreshFailure: (() -> Unit)? = null,
        isDebug: Boolean = false,
        certificatePins: Set<String> = emptySet(),
    ): Retrofit {
        val host = requireNotNull(java.net.URL(baseUrl).host)
        val refreshClient =
            OkHttpClient
                .Builder()
                .apply {
                    configureSecurity(isDebug, certificatePins, host)
                }.build()
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val client =
            OkHttpClient
                .Builder()
                .connectTimeout(CONNECT_TIMEOUT_SECONDS, TimeUnit.SECONDS)
                .readTimeout(READ_TIMEOUT_SECONDS, TimeUnit.SECONDS)
                .writeTimeout(WRITE_TIMEOUT_SECONDS, TimeUnit.SECONDS)
                .callTimeout(CALL_TIMEOUT_SECONDS, TimeUnit.SECONDS)
                .addInterceptor(IdempotentGetRetryInterceptor())
                .apply {
                    configureSecurity(isDebug, certificatePins, host)
                    if (tokenStore != null) {
                        addInterceptor(AccessTokenInterceptor(tokenStore))
                        authenticator(
                            TokenRefreshAuthenticator(
                                baseUrl = baseUrl,
                                tokenStore = tokenStore,
                                client = refreshClient,
                                moshi = moshi,
                                onRefreshFailure = logoutOnRefreshFailure ?: { tokenStore.clear() },
                            ),
                        )
                    }
                }.build()
        return Retrofit
            .Builder()
            .baseUrl(baseUrl)
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()
    }

    fun createApi(baseUrl: String): RemitoApi = create(baseUrl).create(RemitoApi::class.java)

    private fun OkHttpClient.Builder.configureSecurity(
        isDebug: Boolean,
        pins: Set<String>,
        host: String,
    ) {
        connectionSpecs(listOf(ConnectionSpec.MODERN_TLS))
        if (!isDebug) {
            require(pins.isNotEmpty()) { "Release builds must provide certificate pins." }
            val certificatePinner = CertificatePinner.Builder()
            pins.forEach { pin -> certificatePinner.add(host, pin) }
            certificatePinner(certificatePinner.build())
        }
        if (isDebug) {
            addInterceptor(
                HttpLoggingInterceptor().apply {
                    level = HttpLoggingInterceptor.Level.BASIC
                },
            )
        }
    }
}
