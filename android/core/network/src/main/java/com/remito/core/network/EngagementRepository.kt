package com.remito.core.network

import com.remito.core.database.CachedNewsEntity
import com.remito.core.database.FavouriteEntity
import com.remito.core.database.RateDao
import kotlinx.coroutines.flow.Flow
import retrofit2.Response
import java.io.IOException

class EngagementRepository(
    private val api: RemitoApi,
    private val dao: RateDao,
) {
    private val problemDetailsMapper = ProblemDetailsMapper()

    fun observeFavourites(): Flow<List<FavouriteEntity>> = dao.observeFavourites()

    suspend fun refreshFavourites(): List<FavouriteDto> {
        val result = api.favourites().requireBody(problemDetailsMapper)
        dao.clearFavourites()
        dao.upsertFavourites(result.map(::toEntity))
        return result
    }

    suspend fun toggleCurrencyFavourite(code: String) {
        toggleFavourite(FavouriteType.CURRENCY, code)
    }

    suspend fun toggleExchangeFavourite(exchangeId: String) {
        toggleFavourite(FavouriteType.EXCHANGE, exchangeId)
    }

    private suspend fun toggleFavourite(
        type: FavouriteType,
        key: String,
    ) {
        var cached = dao.getFavourite(key)
        if (cached == null || cached.remoteId == null) {
            refreshFavourites()
            cached = dao.getFavourite(key)
        }
        if (cached != null) {
            val remoteId = cached.remoteId ?: throw IOException("Favourite sync did not return an item identifier.")
            api.removeFavourite(remoteId).requireSuccessful(problemDetailsMapper)
            dao.removeFavourite(key)
            return
        }

        val request =
            when (type) {
                FavouriteType.CURRENCY -> FavouriteRequest(type = type, currencyCode = key)
                FavouriteType.EXCHANGE -> FavouriteRequest(type = type, targetId = key)
                FavouriteType.CORRIDOR -> throw IllegalArgumentException("Corridor favourites are not supported here.")
            }
        val favourite = api.addFavourite(request).requireBody(problemDetailsMapper)
        dao.upsertFavourite(toEntity(favourite))
    }

    fun observeNews(): Flow<List<CachedNewsEntity>> = dao.observeNews()

    suspend fun refreshNews(): List<NewsItemDto> {
        val items = api.news().requireBody(problemDetailsMapper)
        dao.upsertNews(items.map(NewsItemDto::toCachedEntity))
        return items
    }

    suspend fun getNews(slug: String): NewsItemDto {
        val cached =
            dao.getNews(slug)?.let {
                NewsItemDto(it.id, it.title, it.slug, it.summary, it.body, it.sourceUrl, it.publishedAt)
            }
        val item =
            try {
                api.newsItem(slug).requireBody(problemDetailsMapper)
            } catch (error: IOException) {
                cached ?: throw error
            }
        dao.upsertNews(listOf(item.toCachedEntity()))
        return item
    }

    private fun toEntity(favourite: FavouriteDto): FavouriteEntity {
        val key = if (favourite.type == FavouriteType.CURRENCY) favourite.targetCode else favourite.targetId
        return FavouriteEntity(
            targetId = key ?: throw IOException("The favourite response omitted its target."),
            type = favourite.type.name.lowercase(),
            remoteId = favourite.id,
        )
    }
}

private fun NewsItemDto.toCachedEntity() = CachedNewsEntity(slug, id, title, summary, body, sourceUrl, publishedAt)

private fun <T> Response<T>.requireBody(problemDetailsMapper: ProblemDetailsMapper): T {
    if (!isSuccessful) throw requestFailure(problemDetailsMapper)
    return body() ?: throw IOException("The server returned an empty response.")
}

private fun Response<*>.requireSuccessful(problemDetailsMapper: ProblemDetailsMapper) {
    if (!isSuccessful) throw requestFailure(problemDetailsMapper)
}

private fun Response<*>.requestFailure(problemDetailsMapper: ProblemDetailsMapper): ApiRequestException {
    val problem = problemDetailsMapper.map(this)
    return ApiRequestException(
        problem.status,
        problem.detail?.takeIf(String::isNotBlank) ?: problem.title,
    )
}
