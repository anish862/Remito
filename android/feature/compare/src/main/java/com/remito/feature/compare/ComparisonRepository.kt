package com.remito.feature.compare

import com.remito.core.database.ComparisonOfferEntity
import com.remito.core.database.RateDao
import com.remito.core.network.ApiRequestException
import com.remito.core.network.ExchangeComparisonResponseDto
import com.remito.core.network.ExchangeOfferDto
import com.remito.core.network.ProblemDetailsMapper
import com.remito.core.network.RedirectRequest
import com.remito.core.network.RedirectResponseDto
import com.remito.core.network.RemitoApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import retrofit2.Response
import java.io.IOException
import java.math.BigDecimal

class ComparisonRepository(
    private val api: RemitoApi,
    private val dao: RateDao,
) {
    fun observeCached(
        corridor: String,
        amount: String,
    ): Flow<List<ExchangeOfferDto>> =
        dao.observeComparisonOffers(corridor, amount).map { cached ->
            cached.map { it.toOffer() }
        }

    suspend fun refresh(
        from: String,
        to: String,
        amount: BigDecimal,
    ): ExchangeComparisonResponseDto {
        val response = api.compare(from, to, amount, sort = "best", pageSize = 100)
        if (!response.isSuccessful) {
            throw failure(response)
        }
        val comparison = checkNotNull(response.body()) { "The comparison response was empty." }
        val corridor = "$from-$to"
        val amountText = amount.toPlainString()
        val offers = comparison.exchanges + comparison.sponsoredSlot?.exchanges.orEmpty()
        dao.replaceComparisonOffers(
            corridor,
            amountText,
            offers.map { it.toEntity(corridor, amountText) },
        )
        return comparison
    }

    suspend fun redirect(
        offer: ExchangeOfferDto,
        from: String,
        to: String,
        amount: BigDecimal,
    ): RedirectResponseDto {
        val response =
            api.redirect(
                RedirectRequest(
                    exchangeId = offer.id,
                    corridor = "$from-$to",
                    amount = amount,
                    appInstalledHint = offer.hasApp,
                ),
            )
        if (!response.isSuccessful) {
            throw failure(response)
        }
        return checkNotNull(response.body()) { "The redirect response was empty." }
    }

    suspend fun markCachedStale(
        corridor: String,
        amount: String,
    ) = dao.markComparisonOffersStale(corridor, amount)

    private fun failure(response: Response<*>): IOException {
        val problem = ProblemDetailsMapper().map(response)
        return ApiRequestException(
            problem.status,
            problem.detail?.takeIf(String::isNotBlank) ?: problem.title,
        )
    }

    private fun ExchangeOfferDto.toEntity(
        corridor: String,
        amount: String,
    ) = ComparisonOfferEntity(
        corridor = corridor,
        amount = amount,
        exchangeId = id,
        name = name,
        logo = logo,
        rate = rate.toPlainString(),
        fee = fee?.toPlainString(),
        amountReceived = amountReceived?.toPlainString(),
        feeDisclosure = feeDisclosure,
        lastUpdated = lastUpdated,
        isStale = isStale,
        hasApp = hasApp,
        isSponsored = isSponsored,
        isBestRate = isBestRate,
    )

    private fun ComparisonOfferEntity.toOffer() =
        ExchangeOfferDto(
            id = exchangeId,
            name = name,
            logo = logo,
            rate = BigDecimal(rate),
            fee = fee?.let(::BigDecimal),
            amountReceived = amountReceived?.let(::BigDecimal),
            feeDisclosure = feeDisclosure,
            lastUpdated = lastUpdated,
            isStale = isStale,
            hasApp = hasApp,
            isSponsored = isSponsored,
            isBestRate = isBestRate,
        )
}
