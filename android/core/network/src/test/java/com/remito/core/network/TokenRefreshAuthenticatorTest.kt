package com.remito.core.network

import kotlinx.coroutines.async
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.util.concurrent.atomic.AtomicInteger

@Suppress("MagicNumber")
class TokenRefreshAuthenticatorTest {
    @Test
    fun concurrentUnauthorizedCallsShareSingleRefresh() =
        runBlocking {
            val refreshCount = AtomicInteger()
            val server = MockWebServer()
            server.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(request: RecordedRequest): MockResponse =
                        when {
                            request.path?.endsWith("/auth/refresh") == true -> {
                                refreshCount.incrementAndGet()
                                MockResponse()
                                    .setResponseCode(200)
                                    .setBody(
                                        """
                                        {"accessToken":"new-access","refreshToken":"new-refresh","tokenType":"Bearer","expiresIn":900}
                                        """.trimIndent(),
                                    )
                            }
                            request.path?.endsWith("/me") == true &&
                                request.getHeader("Authorization") == "Bearer new-access" ->
                                MockResponse()
                                    .setResponseCode(200)
                                    .setBody(
                                        """{"id":"user-1","roles":[],"createdAt":"2025-01-01T00:00:00Z"}""",
                                    )
                            else -> MockResponse().setResponseCode(401)
                        }
                }
            server.start()
            try {
                val tokenStore = FakeTokenStore("old-access", "old-refresh")
                val api =
                    NetworkClient
                        .create(
                            server.url("/api/v1/").toString(),
                            tokenStore = tokenStore,
                            isDebug = true,
                        ).create(RemitoApi::class.java)
                val first = async { api.getMe() }
                val second = async { api.getMe() }

                assertTrue(first.await().isSuccessful)
                assertTrue(second.await().isSuccessful)
                assertEquals(1, refreshCount.get())
                assertEquals("new-access", tokenStore.accessToken())
            } finally {
                server.shutdown()
            }
        }
}

private class FakeTokenStore(
    @Volatile private var access: String?,
    @Volatile private var refresh: String?,
) : TokenStore {
    override fun accessToken(): String? = access

    override fun refreshToken(): String? = refresh

    override fun save(
        accessToken: String,
        refreshToken: String,
    ) {
        access = accessToken
        refresh = refreshToken
    }

    override fun clear() {
        access = null
        refresh = null
    }
}
