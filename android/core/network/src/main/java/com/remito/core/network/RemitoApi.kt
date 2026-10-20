package com.remito.core.network

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.PATCH
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query
import java.math.BigDecimal

interface RemitoApi {
    @POST("auth/token")
    suspend fun createToken(@Body request: TokenRequest): Response<TokenResponse>

    @POST("auth/refresh")
    suspend fun refreshToken(@Body request: RefreshRequest): Response<TokenResponse>

    @POST("auth/logout")
    suspend fun logout(@Body request: RefreshRequest): Response<Unit>

    @GET("devices")
    suspend fun listDevices(): Response<List<DeviceDto>>

    @POST("devices")
    suspend fun registerDevice(@Body request: DeviceRegistration): Response<DeviceDto>

    @DELETE("devices/{id}")
    suspend fun removeDevice(@Path("id") id: String): Response<Unit>

    @GET("currencies")
    suspend fun listCurrencies(): Response<List<CurrencyDto>>

    @GET("currencies/{code}")
    suspend fun getCurrency(@Path("code") code: String): Response<CurrencyDto>

    @GET("rates/latest")
    suspend fun latestRate(@Query("pair") pair: String): Response<MarketRateDto>

    @GET("rates/history")
    suspend fun rateHistory(
        @Query("pair") pair: String,
        @Query("bucket") bucket: HistoryBucket,
        @Query("from") from: String? = null,
        @Query("to") to: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 50,
    ): Response<PagedResponse<RateHistoryDto>>

    @GET("convert")
    suspend fun convert(
        @Query("from") from: String,
        @Query("to") to: String,
        @Query("amount") amount: BigDecimal,
    ): Response<ConversionQuoteDto>

    @GET("exchanges")
    suspend fun exchanges(
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<ExchangeDto>>

    @GET("exchanges/{slug}")
    suspend fun exchange(@Path("slug") slug: String): Response<ExchangeDto>

    @GET("compare")
    suspend fun compare(
        @Query("from") from: String,
        @Query("to") to: String,
        @Query("amount") amount: BigDecimal,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<ExchangeOfferDto>>

    @POST("redirect/{exchangeId}")
    suspend fun redirect(
        @Path("exchangeId") exchangeId: String,
        @Body request: RedirectRequest,
    ): Response<Unit>

    @GET("favourites")
    suspend fun favourites(
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<FavouriteDto>>

    @POST("favourites")
    suspend fun addFavourite(@Body request: FavouriteRequest): Response<FavouriteDto>

    @DELETE("favourites/{id}")
    suspend fun removeFavourite(@Path("id") id: String): Response<Unit>

    @GET("alerts")
    suspend fun alerts(
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<AlertDto>>

    @POST("alerts")
    suspend fun createAlert(@Body request: AlertRequest): Response<AlertDto>

    @PATCH("alerts/{id}")
    suspend fun updateAlert(
        @Path("id") id: String,
        @Body request: AlertUpdate,
    ): Response<AlertDto>

    @DELETE("alerts/{id}")
    suspend fun deleteAlert(@Path("id") id: String): Response<Unit>

    @GET("news")
    suspend fun news(
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<NewsItemDto>>

    @GET("news/{slug}")
    suspend fun newsItem(@Path("slug") slug: String): Response<NewsItemDto>

    @GET("ads/slots/{slot}")
    suspend fun adSlot(@Path("slot") slot: String): Response<List<AdCreativeDto>>

    @GET("admin/exchanges")
    suspend fun adminExchanges(
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<ExchangeDto>>

    @POST("admin/exchanges")
    suspend fun adminCreateExchange(@Body request: ExchangeWrite): Response<ExchangeDto>

    @PATCH("admin/exchanges/{id}")
    suspend fun adminUpdateExchange(
        @Path("id") id: String,
        @Body request: ExchangeWrite,
    ): Response<ExchangeDto>

    @GET("admin/rate-sources")
    suspend fun adminRateSources(): Response<List<RateSourceDto>>

    @POST("admin/rate-sources")
    suspend fun adminCreateRateSource(@Body request: RateSourceWrite): Response<RateSourceDto>

    @POST("admin/rates")
    suspend fun adminIngestRate(@Body request: MarketRateWrite): Response<MarketRateDto>

    @GET("admin/ad-campaigns")
    suspend fun adminAdCampaigns(): Response<List<AdCampaignDto>>

    @POST("admin/ad-campaigns")
    suspend fun adminCreateAdCampaign(@Body request: AdCampaignWrite): Response<AdCampaignDto>

    @GET("admin/news")
    suspend fun adminNews(
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): Response<PagedResponse<NewsItemDto>>

    @POST("admin/news")
    suspend fun adminCreateNews(@Body request: NewsWrite): Response<NewsItemDto>

    @PATCH("admin/news/{id}")
    suspend fun adminUpdateNews(
        @Path("id") id: String,
        @Body request: NewsWrite,
    ): Response<NewsItemDto>

    @GET("admin/audit-logs")
    suspend fun adminAuditLogs(
        @Query("entityType") entityType: String? = null,
        @Query("entityId") entityId: String? = null,
        @Query("from") from: String? = null,
        @Query("to") to: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 50,
    ): Response<PagedResponse<AuditLogDto>>
}
