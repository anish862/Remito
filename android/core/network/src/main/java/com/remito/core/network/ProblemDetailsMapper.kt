package com.remito.core.network

import com.squareup.moshi.Moshi
import retrofit2.Response

data class ApiFailure(
    val status: Int,
    val title: String,
    val detail: String?,
    val traceId: String?,
)

class ProblemDetailsMapper {
    private val adapter =
        Moshi
            .Builder()
            .addLast(
                com.squareup.moshi.kotlin.reflect
                    .KotlinJsonAdapterFactory(),
            ).build()
            .adapter(ProblemDetailsDto::class.java)

    fun map(response: Response<*>): ApiFailure {
        val problem =
            response.errorBody()?.use { body ->
                runCatching { adapter.fromJson(body.string()) }.getOrNull()
            }
        return ApiFailure(
            status = problem?.status ?: response.code(),
            title = problem?.title ?: "Request failed",
            detail = problem?.detail,
            traceId = problem?.traceId,
        )
    }
}
