package com.remito.core.network

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass
import java.math.BigDecimal

@JsonClass(generateAdapter = false)
data class ProblemDetailsDto(
    val type: String? = null,
    val title: String? = null,
    val status: Int? = null,
    val detail: String? = null,
    val instance: String? = null,
    val traceId: String? = null,
)

@JsonClass(generateAdapter = false)
data class TokenRequest(val idToken: String)

@JsonClass(generateAdapter = false)
data class RefreshRequest(val refreshToken: String)

@JsonClass(generateAdapter = false)
data class TokenResponse(
    val accessToken: String,
    val refreshToken: String,
    val tokenType: String,
    val expiresIn: Int,
)

enum class DevicePlatform {
    @Json(name = "android")
    ANDROID,

    @Json(name = "ios")
    IOS,
}

@JsonClass(generateAdapter = false)
data class DeviceRegistration(val fcmToken: String, val platform: DevicePlatform)

@JsonClass(generateAdapter = false)
data class DeviceDto(val id: String, val platform: DevicePlatform, val createdAt: String)

@JsonClass(generateAdapter = false)
data class CurrencyDto(
    val code: String,
    val name: String,
    val symbol: String,
    val decimalPlaces: Int,
)

@JsonClass(generateAdapter = false)
data class MarketRateDto(
    val pair: String,
    val bid: BigDecimal,
    val ask: BigDecimal,
    val mid: BigDecimal,
    val source: String,
    val fetchedAt: String,
    val isStale: Boolean,
)

enum class HistoryBucket {
    @Json(name = "1D")
    ONE_DAY,

    @Json(name = "1W")
    ONE_WEEK,

    @Json(name = "1M")
    ONE_MONTH,

    @Json(name = "6M")
    SIX_MONTHS,

    @Json(name = "1Y")
    ONE_YEAR,
}

@JsonClass(generateAdapter = false)
data class RateHistoryDto(
    val pair: String,
    val bucket: HistoryBucket,
    val timestamp: String,
    val value: BigDecimal,
)

@JsonClass(generateAdapter = false)
data class ConversionQuoteDto(
    val fromCurrency: String,
    val toCurrency: String,
    val amount: BigDecimal,
    val rate: BigDecimal,
    val convertedAmount: BigDecimal,
    val fetchedAt: String,
    val isStale: Boolean,
)

enum class ExchangeStatus {
    @Json(name = "pending")
    PENDING,

    @Json(name = "active")
    ACTIVE,

    @Json(name = "suspended")
    SUSPENDED,

    @Json(name = "inactive")
    INACTIVE,
}

enum class ExchangeTier {
    @Json(name = "standard")
    STANDARD,

    @Json(name = "featured")
    FEATURED,

    @Json(name = "sponsored")
    SPONSORED,
}

@JsonClass(generateAdapter = false)
data class ExchangeDto(
    val id: String,
    val name: String,
    val slug: String,
    val logoUrl: String? = null,
    val hasApp: Boolean,
    val appDeepLink: String? = null,
    val playStoreUrl: String? = null,
    val gatewayUrl: String? = null,
    val status: ExchangeStatus,
    val tier: ExchangeTier,
)

@JsonClass(generateAdapter = false)
data class ExchangeWrite(
    val name: String,
    val slug: String,
    val hasApp: Boolean,
    val status: ExchangeStatus,
    val tier: ExchangeTier,
    val logoUrl: String? = null,
    val appDeepLink: String? = null,
    val playStoreUrl: String? = null,
    val gatewayUrl: String? = null,
)

@JsonClass(generateAdapter = false)
data class ExchangeFeeDto(
    val minAmount: BigDecimal? = null,
    val maxAmount: BigDecimal? = null,
    val flatFee: BigDecimal? = null,
    val percentFee: BigDecimal? = null,
)

@JsonClass(generateAdapter = false)
data class ExchangeOfferDto(
    val exchange: ExchangeDto,
    val rate: BigDecimal,
    val fee: ExchangeFeeDto? = null,
    val amountReceived: BigDecimal,
    val fetchedAt: String,
    val isStale: Boolean,
)

@JsonClass(generateAdapter = false)
data class RedirectRequest(val corridorId: String, val amount: BigDecimal)

enum class FavouriteType {
    @Json(name = "exchange")
    EXCHANGE,

    @Json(name = "corridor")
    CORRIDOR,
}

@JsonClass(generateAdapter = false)
data class FavouriteRequest(val type: FavouriteType, val targetId: String)

@JsonClass(generateAdapter = false)
data class FavouriteDto(
    val id: String,
    val type: FavouriteType,
    val targetId: String,
    val createdAt: String,
)

enum class AlertDirection {
    @Json(name = "above")
    ABOVE,

    @Json(name = "below")
    BELOW,
}

@JsonClass(generateAdapter = false)
data class AlertRequest(val pair: String, val direction: AlertDirection, val target: BigDecimal)

@JsonClass(generateAdapter = false)
data class AlertUpdate(val target: BigDecimal? = null, val isActive: Boolean? = null)

@JsonClass(generateAdapter = false)
data class AlertDto(
    val id: String,
    val pair: String,
    val direction: AlertDirection,
    val target: BigDecimal,
    val isActive: Boolean,
    val lastTriggeredAt: String? = null,
)

@JsonClass(generateAdapter = false)
data class NewsItemDto(
    val id: String,
    val title: String,
    val slug: String,
    val summary: String,
    val body: String,
    val sourceUrl: String? = null,
    val publishedAt: String? = null,
)

@JsonClass(generateAdapter = false)
data class NewsWrite(
    val title: String,
    val slug: String,
    val summary: String,
    val body: String,
    val isPublished: Boolean,
    val sourceUrl: String? = null,
)

@JsonClass(generateAdapter = false)
data class AdCreativeDto(
    val id: String,
    val slot: String,
    val imageUrl: String,
    val targetUrl: String,
    val altText: String,
    val sponsored: Boolean,
)

@JsonClass(generateAdapter = false)
data class AdCampaignWrite(
    val name: String,
    val startsAt: String,
    val endsAt: String,
    val isActive: Boolean,
)

@JsonClass(generateAdapter = false)
data class AdCampaignDto(
    val id: String,
    val name: String,
    val startsAt: String,
    val endsAt: String,
    val isActive: Boolean,
)

enum class RateSourceType {
    @Json(name = "provider")
    PROVIDER,

    @Json(name = "exchange")
    EXCHANGE,

    @Json(name = "manual")
    MANUAL,
}

@JsonClass(generateAdapter = false)
data class RateSourceWrite(
    val name: String,
    val type: RateSourceType,
    val priority: Int,
    val isActive: Boolean,
)

@JsonClass(generateAdapter = false)
data class RateSourceDto(
    val id: String,
    val name: String,
    val type: RateSourceType,
    val priority: Int,
    val isActive: Boolean,
)

@JsonClass(generateAdapter = false)
data class MarketRateWrite(
    val pair: String,
    val bid: BigDecimal,
    val ask: BigDecimal,
    val mid: BigDecimal,
    val sourceId: String,
    val fetchedAt: String,
    val isStale: Boolean,
)

@JsonClass(generateAdapter = false)
data class AuditLogDto(
    val id: String,
    val userId: String? = null,
    val action: String,
    val entityType: String,
    val entityId: String? = null,
    val createdAt: String,
    val details: String? = null,
)

@JsonClass(generateAdapter = false)
data class PagedResponse<T>(
    val items: List<T>,
    val page: Int,
    val pageSize: Int,
    val totalItems: Int,
    val totalPages: Int,
)
