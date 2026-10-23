package com.remito.feature.auth

import com.remito.core.network.OtpChannel
import com.remito.core.network.OtpRequest
import com.remito.core.network.OtpVerificationRequest
import com.remito.core.network.ProblemDetailsMapper
import com.remito.core.network.RemitoApi
import com.remito.core.network.TokenStore
import javax.inject.Inject

class AuthRepository
    @Inject
    constructor(
        private val api: RemitoApi,
        private val tokenStore: TokenStore,
    ) {
        private val problemDetailsMapper = ProblemDetailsMapper()

        suspend fun requestCode(
            channel: OtpChannel,
            destination: String,
        ) {
            val response = api.requestOtp(OtpRequest(channel, destination))
            if (!response.isSuccessful) {
                val problem = problemDetailsMapper.map(response)
                throw IllegalStateException(problem.detail?.takeIf(String::isNotBlank) ?: problem.title)
            }
        }

        suspend fun verifyCode(
            channel: OtpChannel,
            destination: String,
            code: String,
        ) {
            val response = api.verifyOtp(OtpVerificationRequest(channel, destination, code))
            if (!response.isSuccessful) {
                val problem = problemDetailsMapper.map(response)
                throw IllegalStateException(problem.detail?.takeIf(String::isNotBlank) ?: problem.title)
            }
            val token = checkNotNull(response.body()) { "The authentication response was empty." }
            tokenStore.save(token.accessToken, token.refreshToken)
        }

        suspend fun logout() {
            val refresh = tokenStore.refreshToken()
            try {
                if (!refresh.isNullOrBlank()) {
                    val response =
                        api.logout(
                            com.remito.core.network
                                .RefreshRequest(refresh),
                        )
                    if (!response.isSuccessful) {
                        val problem = problemDetailsMapper.map(response)
                        throw IllegalStateException(problem.detail?.takeIf(String::isNotBlank) ?: problem.title)
                    }
                }
            } finally {
                tokenStore.clear()
            }
        }
    }
