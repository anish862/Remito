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

enum class OtpChannel {
    @Json(name = "email")
    EMAIL,

    @Json(name = "phone")
    PHONE,
}

@JsonClass(generateAdapter = false)
data class OtpRequest(
    val channel: OtpChannel,
    val destination: String,
)

@JsonClass(generateAdapter = false)
data class OtpVerificationRequest(
    val channel: OtpChannel,
    val destination: String,
    val code: String,
)

@JsonClass(generateAdapter = false)
data class OtpRequestResponse(
    val accepted: Boolean,
)

@JsonClass(generateAdapter = false)
data class RefreshRequest(
    val refreshToken: String,
)

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
data class DeviceRegistrationRequest(
    val fcmToken: String,
    val platform: DevicePlatform,
)

@JsonClass(generateAdapter = false)
data class UserProfileDto(
    val id: String,
    val email: String? = null,
    val phoneNumber: String? = null,
    val displayName: String? = null,
    val roles: List<String>,
    val createdAt: String,
)

@JsonClass(generateAdapter = false)
data class ProfileUpdateRequest(
    val displayName: String,
)

enum class ConsentType {
    @Json(name = "terms")
    TERMS,

    @Json(name = "privacy")
    PRIVACY,

    @Json(name = "marketing")
    MARKETING,
}

@JsonClass(generateAdapter = false)
data class ConsentRequest(
    val isGranted: Boolean,
    val version: String,
)

@JsonClass(generateAdapter = false)
data class ConsentStateDto(
    val type: ConsentType,
    val version: String,
    val isGranted: Boolean,
    val updatedAt: String,
)

@JsonClass(generateAdapter = false)
data class DeviceDto(
    val id: String,
    val platform: DevicePlatform,
    val createdAt: String,
)

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

@JsonClass(generateAdapter = false)
data class LatestRateItemDto(
    val currency: String,
    val rate: BigDecimal,
    val change24HoursPercent: BigDecimal,
    val lastUpdated: String,
    val isStale: Boolean,
)

@JsonClass(generateAdapter = false)
data class LatestRatesResponseDto(
    val base: String,
    val rates: List<LatestRateItemDto>,
)

@JsonClass(generateAdapter = false)
data class RateHistoryPointDto(
    val timestamp: String,
    val rate: BigDecimal,
)

@JsonClass(generateAdapter = false)
data class RateHistoryResponseDto(
    val pair: String,
    val range: String,
    val points: List<RateHistoryPointDto>,
    val minimum: BigDecimal,
    val maximum: BigDecimal,
    val open: BigDecimal,
    val close: BigDecimal,
)

@JsonClass(generateAdapter = false)
data class ConvertRateRequestDto(
    val amount: BigDecimal,
    @field:Json(name = "from")
    val fromCurrency: String,
    @field:Json(name = "to")
    val toCurrency: String,
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
    val amount: BigDecimal,
    @field:Json(name = "from")
    val fromCurrency: String,
    @field:Json(name = "to")
    val toCurrency: String,
    val rate: BigDecimal,
    @field:Json(name = "result")
    val convertedAmount: BigDecimal,
    val timestamp: String,
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
    val id: String,
    val name: String,
    val logo: String? = null,
    val rate: BigDecimal,
    val fee: BigDecimal? = null,
    val amountReceived: BigDecimal?,
    val feeDisclosure: String,
    val lastUpdated: String,
    val isStale: Boolean,
    val hasApp: Boolean,
    val isSponsored: Boolean,
    val isBestRate: Boolean,
)

@JsonClass(generateAdapter = false)
data class ExchangeComparisonResponseDto(
    val from: String,
    val to: String,
    val amount: BigDecimal,
    val exchanges: List<ExchangeOfferDto>,
    val sponsoredSlot: SponsoredExchangeSlotDto? = null,
    val disclaimer: String,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
)

@JsonClass(generateAdapter = false)
data class SponsoredExchangeSlotDto(
    val label: String,
    val exchanges: List<ExchangeOfferDto>,
)

@JsonClass(generateAdapter = false)
data class RedirectRequest(
    val exchangeId: String,
    val corridor: String,
    val amount: BigDecimal,
    val appInstalledHint: Boolean,
)

@JsonClass(generateAdapter = false)
data class RedirectResponseDto(
    val destinationUrl: String,
    val fallbackUrl: String? = null,
    val gatewayUrl: String? = null,
    val clickId: String,
    val disclosureText: String,
)

enum class FavouriteType {
    @Json(name = "currency")
    CURRENCY,

    @Json(name = "exchange")
    EXCHANGE,

    @Json(name = "corridor")
    CORRIDOR,
}

@JsonClass(generateAdapter = false)
data class FavouriteRequest(
    val type: FavouriteType,
    val targetId: String? = null,
    val currencyCode: String? = null,
)

@JsonClass(generateAdapter = false)
data class FavouriteDto(
    val id: String,
    val type: FavouriteType,
    val targetId: String,
    val createdAt: String,
    val targetCode: String? = null,
)

enum class AlertDirection {
    @Json(name = "above")
    ABOVE,

    @Json(name = "below")
    BELOW,
}

@JsonClass(generateAdapter = false)
data class AlertRequest(
    val pair: String,
    val direction: AlertDirection,
    val target: BigDecimal,
    val isActive: Boolean = true,
    val isOneTime: Boolean = false,
    val dailySummary: Boolean = false,
)

@JsonClass(generateAdapter = false)
data class AlertDto(
    val id: String,
    val pair: String,
    val direction: AlertDirection,
    val target: BigDecimal,
    val isActive: Boolean,
    val lastTriggeredAt: String? = null,
    val isOneTime: Boolean = false,
    val dailySummary: Boolean = false,
)

@JsonClass(generateAdapter = false)
data class NotificationPreferencesDto(
    val rateAlertsEnabled: Boolean,
    val dailySummaryEnabled: Boolean,
    val quietHoursStartMinute: Int? = null,
    val quietHoursEndMinute: Int? = null,
)

@JsonClass(generateAdapter = false)
data class NotificationPreferencesUpdate(
    val rateAlertsEnabled: Boolean,
    val dailySummaryEnabled: Boolean,
    val quietHoursStartMinute: Int? = null,
    val quietHoursEndMinute: Int? = null,
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
