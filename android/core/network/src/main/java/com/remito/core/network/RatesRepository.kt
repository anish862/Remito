package com.remito.core.network

import com.remito.core.database.CachedRateEntity
import com.remito.core.database.RateDao
import com.remito.core.database.RateHistoryEntity
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import java.io.IOException
import java.math.BigDecimal
import java.math.RoundingMode

class ApiRequestException(
    val statusCode: Int,
    message: String,
) : IOException(message)

class RatesRepository(
    private val api: RemitoApi,
    private val dao: RateDao,
) {
    private companion object {
        const val RATE_SCALE = 8
    }

    private val problemDetailsMapper = ProblemDetailsMapper()

    fun observeRates(base: String): Flow<List<CachedRateEntity>> =
        dao.observeRates().map { rates -> rates.filter { it.pair.startsWith("$base/") } }

    suspend fun refreshRates(
        base: String,
        symbols: List<String>,
    ) {
        val response = api.latestRate(base, symbols.joinToString(","))
        if (!response.isSuccessful) {
            throw failure(response)
        }
        val result = checkNotNull(response.body()) { "The rates response was empty." }
        dao.upsertRates(
            result.rates.map { rate ->
                CachedRateEntity(
                    pair = "${result.base}/${rate.currency}",
                    rate = rate.rate.toPlainString(),
                    change24HoursPercent = rate.change24HoursPercent.toPlainString(),
                    lastUpdated = rate.lastUpdated,
                    isStale = rate.isStale,
                )
            },
        )
    }

    fun observeHistory(
        pair: String,
        range: String,
    ): Flow<List<RateHistoryEntity>> = dao.observeHistory(pair, range)

    suspend fun refreshHistory(
        pair: String,
        range: String,
    ): RateHistoryResponseDto {
        val response = api.rateHistory(pair, range)
        if (!response.isSuccessful) {
            throw failure(response)
        }
        val result = checkNotNull(response.body()) { "The history response was empty." }
        dao.upsertHistory(
            result.points.map { point ->
                RateHistoryEntity(pair, range, point.timestamp, point.rate.toPlainString())
            },
        )
        return result
    }

    suspend fun convert(
        amount: BigDecimal,
        from: String,
        to: String,
    ): ConversionQuoteDto {
        val response = api.convert(ConvertRateRequestDto(amount, from, to))
        if (!response.isSuccessful) {
            throw failure(response)
        }
        return checkNotNull(response.body()) { "The conversion response was empty." }
    }

    suspend fun cachedConversion(
        amount: BigDecimal,
        from: String,
        to: String,
    ): BigDecimal? = cachedConversionQuote(amount, from, to)?.convertedAmount

    suspend fun cachedConversionQuote(
        amount: BigDecimal,
        from: String,
        to: String,
    ): ConversionQuoteDto? {
        val direct = dao.getRate("$from/$to")
        return if (direct != null) {
            val rate = BigDecimal(direct.rate)
            ConversionQuoteDto(amount, from, to, rate, amount.multiply(rate), direct.lastUpdated, true)
        } else {
            dao.getRate("$to/$from")?.let { reverse ->
                val rate = BigDecimal.ONE.divide(BigDecimal(reverse.rate), RATE_SCALE, RoundingMode.HALF_EVEN)
                ConversionQuoteDto(amount, from, to, rate, amount.multiply(rate), reverse.lastUpdated, true)
            }
        }
    }

    suspend fun markCachedRatesStale() = dao.markRatesStale()

    private fun failure(response: retrofit2.Response<*>): ApiRequestException {
        val problem = problemDetailsMapper.map(response)
        return ApiRequestException(
            problem.status,
            problem.detail?.takeIf(String::isNotBlank) ?: problem.title,
        )
    }
}
